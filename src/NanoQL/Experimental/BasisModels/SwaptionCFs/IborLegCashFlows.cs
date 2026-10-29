// SPDX-FileCopyrightText: Copyright (C) 2018 Sebastian Schlenkrich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/basismodels/swaptioncfs.hpp ql/experimental/basismodels/swaptioncfs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A floating leg mapped to deterministic cash flows under a tenor basis: the notional at the first remaining coupon's
/// start, a spread coupon per remaining coupon, and the notional back at the end: QuantLib's
/// <c>IborLegCashFlows</c>.
/// </summary>
/// <remarks>
/// <para>
/// The first remaining coupon is the first that starts on or after the discount curve's reference date; with none,
/// the flows are empty. Each spread is the coupon's forward against the discount curve's forward over its accrual,
/// continuously compounded and paid at the coupon's start, or simply compounded and paid at its payment date. The
/// times are from the reference date, Actual/365 (Fixed), and the weights the flows' amounts, read in the context.
/// </para>
/// <para>
/// An extension point, as QuantLib's: <see cref="SwapCashFlows"/> derives from it. QuantLib reads its first cash flow as
/// a coupon before it checks the leg, so an empty leg, or a first flow that isn't a coupon, is undefined there; here
/// both are refused, the latter with QuantLib's own "FloatingLeg CashFlow is no Coupon.".
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public class IborLegCashFlows
{
    /// <summary>Initializes a new instance of the <see cref="IborLegCashFlows"/> class, with no flows: QuantLib's
    /// default constructor.</summary>
    public IborLegCashFlows()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="IborLegCashFlows"/> class.</summary>
    /// <param name="iborLeg">The floating leg, its flows coupons.</param>
    /// <param name="discountCurve">The discount curve, whose reference date the times start from.</param>
    /// <param name="context">The evaluation context, whose fixings and forecasts the coupons' rates read.</param>
    /// <param name="contTenorSpread">Whether the spreads are continuously compounded and paid at each coupon's start;
    /// otherwise simply compounded and paid at its payment date.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The leg is empty.</exception>
    /// <exception cref="NanoQLException">A flow of the leg isn't a coupon, with QuantLib's text, or a rate can't be
    /// read.</exception>
    public IborLegCashFlows(
        IReadOnlyList<CashFlow> iborLeg, YieldTermStructure discountCurve, EvaluationContext context, bool contTenorSpread = true)
    {
        ArgumentNullException.ThrowIfNull(iborLeg);
        ArgumentNullException.ThrowIfNull(discountCurve);
        ArgumentNullException.ThrowIfNull(context);
        if (iborLeg.Count == 0)
        {
            throw new ArgumentException("empty floating leg", nameof(iborLeg));
        }

        RefDate = discountCurve.ReferenceDate;

        // we need to find the first coupon for initial payment
        int floatIdx = 0;
        while (floatIdx + 1 < iborLeg.Count && RefDate > CouponOf(iborLeg[floatIdx]).AccrualStartDate)
        {
            ++floatIdx;
        }

        var floatLeg = ImmutableArray.CreateBuilder<CashFlow>();
        if (RefDate <= CouponOf(iborLeg[floatIdx]).AccrualStartDate)
        {
            // otherwise there is no floating coupon left
            Coupon firstFloatCoupon = CouponOf(iborLeg[floatIdx]);
            floatLeg.Add(new SimpleCashFlow(firstFloatCoupon.Nominal(context), firstFloatCoupon.AccrualStartDate));

            // calculate spread payments
            for (int k = floatIdx; k < iborLeg.Count; ++k)
            {
                Coupon coupon = CouponOf(iborLeg[k]);
                Date startDate = coupon.AccrualStartDate;
                Date endDate = coupon.AccrualEndDate;
                double liborForwardRate = coupon.Rate(context);
                double discForwardRate =
                    ((discountCurve.Discount(startDate) / discountCurve.Discount(endDate)) - 1.0) / coupon.AccrualPeriod();
                double spread;
                Date payDate;
                if (contTenorSpread)
                {
                    // Db = (1 + Delta L^libor) / (1 + Delta L^ois)
                    // spread (Db - 1) paid at startDate
                    spread = (((1.0 + (coupon.AccrualPeriod() * liborForwardRate))
                            / (1.0 + (coupon.AccrualPeriod() * discForwardRate))) - 1.0)
                        / coupon.AccrualPeriod();
                    payDate = startDate;
                }
                else
                {
                    // spread L^libor - L^ois
                    spread = liborForwardRate - discForwardRate;
                    payDate = coupon.Date;
                }

                floatLeg.Add(new FixedRateCoupon(
                    payDate, coupon.Nominal(context), spread, coupon.DayCounter, startDate, endDate));
            }

            // finally, add the notional at the last date
            Coupon lastFloatCoupon = CouponOf(iborLeg[^1]);
            floatLeg.Add(new SimpleCashFlow(-1.0 * lastFloatCoupon.Nominal(context), lastFloatCoupon.AccrualEndDate));
        }

        // assemble raw cash flow data...
        FloatLeg = floatLeg.ToImmutable();
        FloatTimes = Times(FloatLeg);
        FloatWeights = [.. FloatLeg.Select(c => c.Amount(context))];
    }

    /// <summary>Gets the mapped floating flows: the first notional, the spread coupons and the last notional.</summary>
    public ImmutableArray<CashFlow> FloatLeg { get; } = [];

    /// <summary>Gets the floating flows' times from the reference date, Actual/365 (Fixed).</summary>
    public ImmutableArray<double> FloatTimes { get; } = [];

    /// <summary>Gets the floating flows' amounts.</summary>
    public ImmutableArray<double> FloatWeights { get; } = [];

    /// <summary>Gets the reference date the times start from, the discount curve's; <c>default(Date)</c> for no
    /// flows.</summary>
    protected Date RefDate { get; }

    /// <summary>The flows' times from the reference date, Actual/365 (Fixed), QuantLib's model time.</summary>
    /// <param name="flows">The flows.</param>
    /// <returns>The times.</returns>
    protected ImmutableArray<double> Times(IEnumerable<CashFlow> flows)
    {
        ArgumentNullException.ThrowIfNull(flows);
        var dc = new Actual365Fixed();
        return [.. flows.Select(k => dc.YearFraction(RefDate, k.Date))];
    }

    // QuantLib's coupon_cast, refused where QuantLib's would dereference a null pointer.
    private static Coupon CouponOf(CashFlow c) =>
        c as Coupon ?? throw new NanoQLException("FloatingLeg CashFlow is no Coupon.");
}
