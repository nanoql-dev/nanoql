// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/capfloor/gaussian1dcapfloorengine.hpp ql/pricingengines/capfloor/gaussian1dcapfloorengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Gaussian1d cap/floor engine: each optionlet's payoff in the model's state at its fixing, deflated by the
/// numeraire and integrated over the state grid.
/// </summary>
/// <remarks>
/// <para>
/// A static class: <c>Calculate</c> runs QuantLib's four steps (playbook §2.1) on the model, an argument where
/// QuantLib's engine holds it. The coupons must carry no spread. The results add each optionlet's price and its
/// at-the-money forward, the coupon's (<c>optionletsPrice</c>, <c>optionletsAtmForward</c>).
/// </para>
/// <para>
/// Seven of QuantLib's defects are fixed. The strike's leg is discounted on the discount curve, as the floating leg and
/// the swaption engine's are, where QuantLib's reads the model curve (<c>:86-89</c>, <c>:158-161</c>); the at-the-money
/// forwards are the coupons', where QuantLib's are left 0; a caplet fixed before today is the known payoff discounted
/// to its payment, where QuantLib's is undiscounted; a curve on another clock is read at a caplet's fixing date; a CMS
/// coupon's rate is the model's swap rate at its fixing, and a CMS spread coupon's the geared spread of its two
/// indexes' swap rates, the float-float engine's estimates, where QuantLib's prices the coupon period's simple forward;
/// flat payoff extrapolation's right tail reads the last node. And a compounded overnight coupon, which QuantLib prices
/// on its last day's rate times the whole accrual, is a Black call on its window's compounding factor on GSR: each
/// daily factor is a zero bond affine in the state, so the factor is lognormal given the state at the window's start,
/// exactly for plain compounding and an observation shift and to second order for a lookback without the shift and a
/// lockout, its log-variance the discrete sum's over the fixing dates; fixings already made come from the index's
/// history, and a payment after the window's end is handled statically, the window end's forward deflated by the
/// payment date's bond. On a Markov-functional model, which defines no short rate, such a coupon is refused.
/// </para>
/// </remarks>
public static class Gaussian1dCapFloorEngine
{
    /// <summary>Prices a cap, floor or collar on a Gaussian1d model.</summary>
    /// <param name="capFloor">The cap, floor or collar.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="integrationPoints">The state grid's points on each side.</param>
    /// <param name="stddevs">The state grid's width, in standard deviations.</param>
    /// <param name="extrapolatePayoff">Whether the payoff is extrapolated past the grid.</param>
    /// <param name="flatPayoffExtrapolation">Whether the extrapolation is flat, else the end segment's cubic.</param>
    /// <param name="discountCurve">The discount curve; <see langword="null"/> for the model's.</param>
    /// <returns>The results: the NPV and the optionlets' prices and forwards.</returns>
    /// <exception cref="ArgumentNullException">The cap/floor, the context or the model is null.</exception>
    /// <exception cref="NanoQLException">A coupon has a spread, or its index is neither an Ibor, a swap nor a swap
    /// spread index; or
    /// a compounded overnight coupon is priced on a model that defines no short rate.</exception>
    public static InstrumentResults Calculate(
        CapFloor capFloor,
        EvaluationContext context,
        Gaussian1dModel model,
        int integrationPoints = 64,
        double stddevs = 7.0,
        bool extrapolatePayoff = true,
        bool flatPayoffExtrapolation = false,
        YieldTermStructure? discountCurve = null)
    {
        ArgumentNullException.ThrowIfNull(capFloor);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        if (capFloor.IsExpired(context))
        {
            return capFloor.SetupExpired();
        }

        CapFloorArguments arguments = capFloor.SetupArguments(context);
        arguments.Validate();
        InstrumentResults results = Body(
            capFloor, arguments, context, model, integrationPoints, stddevs, extrapolatePayoff, flatPayoffExtrapolation, discountCurve);
        return capFloor.FetchResults(results);
    }

    // QuantLib's calculate().
    private static InstrumentResults Body(
        CapFloor capFloor,
        CapFloorArguments arguments,
        EvaluationContext context,
        Gaussian1dModel model,
        int integrationPoints,
        double stddevs,
        bool extrapolatePayoff,
        bool flatPayoffExtrapolation,
        YieldTermStructure? discountCurve)
    {
        foreach (double spread in arguments.Spreads)
        {
            if (!(spread == 0.0))
            {
                throw new NanoQLException($"Non zero spreads ({DataFormattersFunctions.General(spread)}) are not allowed.");
            }
        }

        int optionlets = arguments.StartDates.Length;
        var values = new double[optionlets];
        double value = 0.0;

        Date settlement = model.TermStructure.ReferenceDate;
        CapFloorType type = arguments.Type;

        var memo = new Gaussian1dMemo();
        Vector z = model.YGrid(stddevs, integrationPoints, 1.0, 0.0, 0.0, memo);
        double[] p = new double[z.Size];

        for (int i = 0; i < optionlets; ++i)
        {
            Date valueDate = arguments.StartDates[i];
            Date paymentDate = arguments.EndDates[i];
            InterestRateIndex index = arguments.Indexes[i];
            var overnight = capFloor.FloatingLeg[i] as OvernightIndexedCoupon;

            // QuantLib's simple forward of an index of another kind: refused
            if (index is not (IborIndex or SwapIndex or SwapSpreadIndex))
            {
                throw new NanoQLException($"Gaussian1dCapFloorEngine prices Ibor, overnight, CMS and CMS spread coupons only, not {index.Name}'s");
            }

            if (paymentDate > settlement)
            {
                double f = arguments.Nominals[i] * arguments.Gearings[i];
                Date fixingDate = arguments.FixingDates[i];

                if (type is CapFloorType.Cap or CapFloorType.Collar)
                {
                    double strike = arguments.CapRates[i]!.Value;
                    values[i] = Optionlet(
                        OptionType.Call, strike, f, i, valueDate, paymentDate, fixingDate, index, overnight, arguments, context, model, discountCurve,
                        z, p, extrapolatePayoff, flatPayoffExtrapolation, settlement, memo);
                }

                if (type is CapFloorType.Floor or CapFloorType.Collar)
                {
                    double strike = arguments.FloorRates[i]!.Value;
                    double floorlet = Optionlet(
                        OptionType.Put, strike, f, i, valueDate, paymentDate, fixingDate, index, overnight, arguments, context, model, discountCurve,
                        z, p, extrapolatePayoff, flatPayoffExtrapolation, settlement, memo);
                    if (type == CapFloorType.Floor)
                    {
                        values[i] = floorlet;
                    }
                    else
                    {
                        // a collar is long a cap and short a floor
                        values[i] -= floorlet;
                    }
                }

                value += values[i];
            }
        }

        var additionalResults = ImmutableSortedDictionary.CreateBuilder<string, object>();
        additionalResults["optionletsPrice"] = ImmutableArray.Create(values);

        // the coupons' forwards, as Black's engine gives them, where QuantLib's are left 0
        additionalResults["optionletsAtmForward"] = arguments.Forwards;
        return new InstrumentResults { NPV = value, AdditionalResults = additionalResults.ToImmutable() };
    }

    // A caplet (a call) or a floorlet (a put): QuantLib's two blocks, which differ only in the payoff's sign and the
    // tail extrapolated.
    private static double Optionlet(
        OptionType optionType,
        double strike,
        double f,
        int i,
        Date valueDate,
        Date paymentDate,
        Date fixingDate,
        InterestRateIndex index,
        OvernightIndexedCoupon? overnight,
        CapFloorArguments arguments,
        EvaluationContext context,
        Gaussian1dModel model,
        YieldTermStructure? discountCurve,
        Vector z,
        double[] p,
        bool extrapolatePayoff,
        bool flatPayoffExtrapolation,
        Date settlement,
        Gaussian1dMemo memo)
    {
        double sign = optionType == OptionType.Call ? 1.0 : -1.0;
        double accrual = arguments.AccrualTimes[i];

        // the caplet's state date: its window's start for a compounded overnight coupon
        Date stateDate = fixingDate;
        CompoundingWindow? window = null;
        if (overnight is not null)
        {
            window = CompoundingWindow.Of(overnight, context, model, settlement, memo);
            stateDate = window.StartDate;
        }

        if (window is null && fixingDate <= settlement)
        {
            // the known payoff, discounted to the payment
            double forward = arguments.Forwards[i] ?? throw new NanoQLException("no forward for a caplet paid before the evaluation date");
            return Math.Max(sign * (forward - strike), 0.0) * f * accrual * model.Zerobond(paymentDate, null, 0.0, discountCurve, memo);
        }

        double stateTime = model.TermStructure.TimeFromReference(stateDate);
        bool deterministic = stateDate <= settlement;
        for (int j = 0; j < z.Size; j++)
        {
            double y = deterministic ? 0.0 : z[j];
            double payoff;
            if (window is not null)
            {
                payoff = window.Value(optionType, strike, accrual, stateDate, y, paymentDate, discountCurve, memo);
            }
            else
            {
                double floatingLegNpv;
                if (index is SwapIndex swapIndex)
                {
                    // the model's swap rate at the fixing, the float-float engine's estimate
                    floatingLegNpv = accrual * model.SwapRate(fixingDate, swapIndex.Tenor, context, fixingDate, y, swapIndex, memo)
                        * model.Zerobond(paymentDate, fixingDate, y, discountCurve, memo);
                }
                else if (index is SwapSpreadIndex spreadIndex)
                {
                    // the geared spread of the model's two swap rates at the fixing, the float-float engine's estimate
                    double spreadRate = (spreadIndex.Gearing1
                            * model.SwapRate(fixingDate, spreadIndex.SwapIndex1.Tenor, context, fixingDate, y, spreadIndex.SwapIndex1, memo))
                        + (spreadIndex.Gearing2
                            * model.SwapRate(fixingDate, spreadIndex.SwapIndex2.Tenor, context, fixingDate, y, spreadIndex.SwapIndex2, memo));
                    floatingLegNpv = accrual * spreadRate * model.Zerobond(paymentDate, fixingDate, y, discountCurve, memo);
                }
                else
                {
                    floatingLegNpv = accrual * model.ForwardRate(fixingDate, context, fixingDate, y, (IborIndex)index, memo)
                        * model.Zerobond(paymentDate, fixingDate, y, discountCurve, memo);
                }

                // the strike's leg on the discount curve, as the floating leg's
                double fixedLegNpv = strike * accrual * model.Zerobond(paymentDate, fixingDate, y, discountCurve, memo);
                payoff = Math.Max(sign * (floatingLegNpv - fixedLegNpv), 0.0);
            }

            // the numeraire read at the fixing's date
            p[j] = payoff / model.NumeraireCore(stateTime, stateDate, y, discountCurve, memo);
        }

        CubicInterpolation spline = Gaussian1dSwaptionEngine.Spline(z, p);
        double price = 0.0;
        for (int j = 0; j < z.Size - 1; j++)
        {
            price += Gaussian1dModel.GaussianShiftedPolynomialIntegral(
                0.0, spline.CCoefficients()[j], spline.BCoefficients()[j], spline.ACoefficients()[j], p[j], z[j], z[j], z[j + 1]);
        }

        if (extrapolatePayoff)
        {
            if (flatPayoffExtrapolation)
            {
                // the right tail at the last node's value, where QuantLib's takes the second-last's
                price += Gaussian1dModel.GaussianShiftedPolynomialIntegral(0.0, 0.0, 0.0, 0.0, p[z.Size - 1], z[z.Size - 2], z[z.Size - 1], 100.0);
                price += Gaussian1dModel.GaussianShiftedPolynomialIntegral(0.0, 0.0, 0.0, 0.0, p[0], z[0], -100.0, z[0]);
            }
            else if (optionType == OptionType.Call)
            {
                price += Gaussian1dModel.GaussianShiftedPolynomialIntegral(
                    0.0,
                    spline.CCoefficients()[z.Size - 2],
                    spline.BCoefficients()[z.Size - 2],
                    spline.ACoefficients()[z.Size - 2],
                    p[z.Size - 2],
                    z[z.Size - 2],
                    z[z.Size - 1],
                    100.0);
            }
            else
            {
                price += Gaussian1dModel.GaussianShiftedPolynomialIntegral(
                    0.0, spline.CCoefficients()[0], spline.BCoefficients()[0], spline.ACoefficients()[0], p[0], z[0], -100.0, z[0]);
            }
        }

        return price * model.NumeraireCore(0.0, null, 0.0, discountCurve, memo) * f;
    }

    // A compounded overnight coupon's observation window on GSR: the fixings already made, from the index's history;
    // and per future fixing its rate's period and its weight in the compounding, and the factor's log-variance given
    // the state at the window's start.
    private sealed class CompoundingWindow
    {
        private readonly Gaussian1dModel model;
        private readonly OvernightIndex index;
        private readonly double knownFactor;
        private readonly ImmutableArray<(Date Start, Date End, double Weight)> future;
        private readonly double logVariance;
        private readonly double tau;

        private CompoundingWindow(
            Gaussian1dModel model,
            OvernightIndex index,
            Date startDate,
            double knownFactor,
            ImmutableArray<(Date Start, Date End, double Weight)> future,
            double logVariance,
            double tau)
        {
            this.model = model;
            this.index = index;
            StartDate = startDate;
            this.knownFactor = knownFactor;
            this.future = future;
            this.logVariance = logVariance;
            this.tau = tau;
        }

        // The state's date: the window's first fixing's, or the evaluation's once the window has started, when the
        // state is known.
        public Date StartDate { get; }

        public static CompoundingWindow Of(
            OvernightIndexedCoupon coupon, EvaluationContext context, Gaussian1dModel model, Date settlement, Gaussian1dMemo memo)
        {
            var index = (OvernightIndex)coupon.Index;
            ImmutableArray<Date> fixingDates = coupon.FixingDates();
            ImmutableArray<double> dt = coupon.Dt();
            Date today = context.EvaluationDate;

            double knownFactor = 1.0;
            var future = ImmutableArray.CreateBuilder<(Date Start, Date End, double Weight)>();
            var times = new List<double>();
            for (int i = 0; i < fixingDates.Length; i++)
            {
                Date d = fixingDates[i];
                if (d <= today)
                {
                    // as QuantLib's coupon accrues it: the history, or today's forecast
                    knownFactor *= 1.0 + (index.Fixing(d, context) * dt[i]);
                }
                else
                {
                    Date start = index.ValueDate(d);
                    Date end = index.MaturityDate(start);
                    future.Add((start, end, dt[i] / index.DayCounter.YearFraction(start, end)));
                    times.Add(model.TermStructure.TimeFromReference(d));
                }
            }

            Date startDate = fixingDates[0] > settlement ? fixingDates[0] : settlement;
            double t0 = model.TermStructure.TimeFromReference(startDate);

            double logVariance = 0.0;
            if (future.Count > 0)
            {
                // each fixing's log-factor's sensitivity to the state at its fixing, at the forward (second order for a
                // weight other than 1)
                var coefficients = new double[future.Count];
                YieldTermStructure? forwarding = index.ForwardingTermStructure;
                for (int k = 0; k < future.Count; k++)
                {
                    (Date start, Date end, double w) = future[k];
                    double g = (model.StateBondSensitivity(times[k], model.TermStructure.TimeFromReference(end), memo)
                            ?? Refuse(coupon))
                        - model.StateBondSensitivity(times[k], model.TermStructure.TimeFromReference(start), memo)!.Value;
                    double q = model.Zerobond(start, startDate, 0.0, forwarding, memo) / model.Zerobond(end, startDate, 0.0, forwarding, memo);
                    coefficients[k] = g * w * q / (1.0 + (w * (q - 1.0)));
                }

                logVariance = model.CompoundingLogVariance(t0, [.. times], coefficients, memo) ?? Refuse(coupon);
            }

            // the rate's year fraction, as the compounding pricer divides by it: the value dates' with the observation
            // shift, the interest dates' without
            ImmutableArray<Date> rateDates = coupon.ApplyObservationShift && coupon.FixingDays > 0 ? coupon.ValueDates() : coupon.InterestDates();
            double tau = index.DayCounter.YearFraction(rateDates[0], rateDates[^1]);
            return new CompoundingWindow(model, index, startDate, knownFactor, future.ToImmutable(), logVariance, tau);
        }

        // The coupon's payoff per unit of nominal and gearing at the window's start in the state y, accrual·(R − K)⁺
        // for the rate R = (F − 1)/τ: the payment date's bond times (accrual/τ) times a Black option on the compounding
        // factor F, struck at 1 + Kτ.
        public double Value(
            OptionType type, double strike, double accrual, Date stateDate, double y, Date paymentDate, YieldTermStructure? discountCurve, Gaussian1dMemo memo)
        {
            YieldTermStructure? forwarding = index.ForwardingTermStructure;
            double forward = knownFactor;
            foreach ((Date start, Date end, double w) in future)
            {
                double q = model.Zerobond(start, stateDate, y, forwarding, memo) / model.Zerobond(end, stateDate, y, forwarding, memo);
                forward *= 1.0 + (w * (q - 1.0));
            }

            return accrual / tau * BlackFormula(type, 1.0 + (strike * tau), forward, Math.Sqrt(logVariance), 1.0)
                * model.Zerobond(paymentDate, stateDate, y, discountCurve, memo);
        }

        private static double Refuse(OvernightIndexedCoupon coupon) =>
            throw new NanoQLException(
                $"the compounded overnight coupon paid on {coupon.Date} can't be priced on this model: it defines no short rate, "
                + "so the compounding factor has no distribution given its state");
    }
}
