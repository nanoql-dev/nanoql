// SPDX-FileCopyrightText: Copyright (C) 2018 Sebastian Schlenkrich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/basismodels/swaptioncfs.hpp ql/experimental/basismodels/swaptioncfs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// A fixed-versus-floating swap mapped to deterministic cash flows under a tenor basis: the floating leg's
/// (<see cref="IborLegCashFlows"/>) and the fixed coupons that start on or after the discount curve's reference date,
/// with their annuity weights: QuantLib's <c>SwapCashFlows</c>.
/// </summary>
/// <remarks>
/// An extension point, as QuantLib's: <see cref="SwaptionCashFlows"/> derives from it. QuantLib reads each fixed flow as
/// a coupon unchecked, undefined for one that isn't; here it is refused.
/// </remarks>
[Experimental("NQL0001")]
public class SwapCashFlows : IborLegCashFlows
{
    /// <summary>Initializes a new instance of the <see cref="SwapCashFlows"/> class, with no flows: QuantLib's default
    /// constructor.</summary>
    public SwapCashFlows()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SwapCashFlows"/> class.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="discountCurve">The discount curve, whose reference date the times start from.</param>
    /// <param name="context">The evaluation context, whose fixings and forecasts the floating coupons' rates
    /// read.</param>
    /// <param name="contTenorSpread">Whether the floating spreads are continuously compounded and paid at each coupon's
    /// start; otherwise simply compounded and paid at its payment date.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The floating leg is empty.</exception>
    /// <exception cref="NanoQLException">A flow isn't a coupon, or a rate can't be read.</exception>
    public SwapCashFlows(
        FixedVsFloatingSwap swap, YieldTermStructure discountCurve, EvaluationContext context, bool contTenorSpread = true)
        : base((swap ?? throw new ArgumentNullException(nameof(swap))).FloatingLeg, discountCurve, context, contTenorSpread)
    {
        // copy fixed leg coupons
        FixedLeg = [.. swap.FixedLeg.Where(k => FixedCouponOf(k).AccrualStartDate >= RefDate)];

        // ... fixed times/weights
        FixedTimes = Times(FixedLeg);
        FixedWeights = [.. FixedLeg.Select(k => k.Amount(context))];
        AnnuityWeights = [.. FixedLeg.OfType<Coupon>().Select(c => c.Nominal(context) * c.AccrualPeriod())];
    }

    /// <summary>Gets the fixed coupons that start on or after the reference date.</summary>
    public ImmutableArray<CashFlow> FixedLeg { get; } = [];

    /// <summary>Gets the fixed coupons' times from the reference date, Actual/365 (Fixed).</summary>
    public ImmutableArray<double> FixedTimes { get; } = [];

    /// <summary>Gets the fixed coupons' amounts.</summary>
    public ImmutableArray<double> FixedWeights { get; } = [];

    /// <summary>Gets the fixed coupons' annuity weights, each nominal times accrual period.</summary>
    public ImmutableArray<double> AnnuityWeights { get; } = [];

    // QuantLib's coupon_cast on the fixed leg, refused where QuantLib's would dereference a null pointer.
    private static Coupon FixedCouponOf(CashFlow c) =>
        c as Coupon ?? throw new NanoQLException("FixedLeg CashFlow is no Coupon.");
}
