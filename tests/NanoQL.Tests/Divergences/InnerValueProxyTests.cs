// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference inner value's curve (the S-sr plan's SSR4): QuantLib's <c>FdmAffineModelSwapInnerValue</c> links
/// relinkable handles, at each point of the mesh, to a new <c>FdmAffineModelTermStructure</c> at the point's state, and
/// its swap's floating coupons, cloned onto them, forecast on it (<c>fdmaffinemodelswapinnervalue.hpp</c>). nanoql has no
/// relinkable handles: the inner value holds, for each exercise date, a private proxy curve pair whose state it sets at
/// each point, its swap rebuilt on the index cloned onto the forwarding proxy. QuantLib's route, a fresh immutable
/// <see cref="FdmAffineModelTermStructure"/> pair and a swap rebuilt on it for every point, gives the same value bit for
/// bit, at five points of each of three exercise dates, on Hull-White discounting on a flat 2.7% curve and forwarding on
/// a flat 3.1% one.
/// </summary>
public class InnerValueProxyTests
{
    [Fact]
    public void TheProxyIsAFreshCurveAtEveryPoint()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var discount = new FlatForward(today, 0.027, new Actual365Fixed());
        var forecast = new FlatForward(today, 0.031, new Actual365Fixed());
        var disModel = new HullWhite(discount, 0.05, 0.01);
        var fwdModel = new HullWhite(forecast, 0.05, 0.01);
        var calendar = new TARGET();
        Date start = calendar.Advance(calendar.Advance(today, new Period(1, TimeUnit.Years)), 2, TimeUnit.Days);
        var fixedSchedule = new Schedule(
            start, start + new Period(5, TimeUnit.Years), new Period(1, TimeUnit.Years), calendar,
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        var floatSchedule = new Schedule(
            start, start + new Period(5, TimeUnit.Years), new Period(6, TimeUnit.Months), calendar,
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        var swap = new VanillaSwap(
            SwapType.Payer, 1e6, fixedSchedule, 0.031, new Thirty360(Thirty360Convention.BondBasis), floatSchedule,
            new Euribor6M(forecast), 0.0, new Actual360(), context);

        Date[] exerciseDates = [.. swap.FixedLeg.Take(3).Select(c => ((Coupon)c).AccrualStartDate)];
        var t2d = new Dictionary<double, Date>();
        foreach (Date d in exerciseDates)
        {
            t2d[discount.DayCounter.YearFraction(discount.ReferenceDate, d)] = d;
        }

        double maturity = t2d.Keys.Max();
        var mesher = new FdmMesherComposite(
            new FdmSimpleProcess1dMesher(21, new OrnsteinUhlenbeckProcess(disModel.A(), disModel.Sigma), maturity, 1, 1e-5));
        var calculator = new FdmAffineHullWhiteModelSwapInnerValue(disModel, fwdModel, swap, t2d, mesher, 0, context);

        foreach ((double t, Date exerciseDate) in t2d)
        {
            foreach (int point in new[] { 0, 5, 10, 15, 20 })
            {
                FdmLinearOpIterator iter = mesher.Layout.Begin();
                for (int i = 0; i < point; i++)
                {
                    iter.Increment();
                }

                double x = mesher.Location(iter, 0);
                double expected = QuantLibsRoute(swap, disModel, fwdModel, t, x, exerciseDate, context);
                Assert.Equal(expected, calculator.InnerValue(iter, t));
            }
        }
    }

    // QuantLib's per-point relink: a fresh curve pair at the point's state, the swap rebuilt on the index cloned onto it.
    private static double QuantLibsRoute(
        VanillaSwap swap, HullWhite disModel, HullWhite fwdModel, double t, double x, Date exerciseDate, EvaluationContext context)
    {
        YieldTermStructure dis = disModel.TermStructure, fwd = fwdModel.TermStructure;
        var disTs = new FdmAffineModelTermStructure(
            new Vector(1, disModel.Dynamics().ShortRate(t, x)), dis.Calendar, dis.DayCounter, exerciseDate, dis.ReferenceDate, disModel);
        var fwdTs = new FdmAffineModelTermStructure(
            new Vector(1, fwdModel.Dynamics().ShortRate(t, x)), fwd.Calendar, fwd.DayCounter, exerciseDate, fwd.ReferenceDate, fwdModel);
        var rebuilt = new VanillaSwap(
            swap.Type, swap.Nominal(), swap.FixedSchedule, swap.FixedRate, swap.FixedDayCount, swap.FloatingSchedule,
            swap.IborIndex.Clone(fwdTs), swap.Spread, swap.FloatingDayCount, context, swap.PaymentConvention);

        double npv = 0.0;
        for (int j = 0; j < 2; j++)
        {
            foreach (CashFlow i in rebuilt.Legs[j])
            {
                npv += ((Coupon)i).AccrualStartDate >= exerciseDate ? i.Amount(context) * disTs.Discount(i.Date) : 0.0;
            }

            if (j == 0)
            {
                npv *= -1.0;
            }
        }

        if (rebuilt.Type == SwapType.Receiver)
        {
            npv *= -1.0;
        }

        return 0.0 < npv ? npv : 0.0;
    }
}
