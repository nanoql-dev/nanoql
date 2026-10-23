// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/fixedvsfloatingswap.hpp ql/instruments/fixedvsfloatingswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// What QuantLib's <c>FixedVsFloatingSwap::setupArguments</c> computes for its model engines and discretized assets:
/// the swap's type and constant nominal, and its coupons' dates, amounts, accrual times and spreads.
/// </summary>
/// <remarks>
/// nanoql's swaps hold no arguments object: the engines that read QuantLib's (the Jamshidian engine, the discretized
/// swap and swaption) compute these from the legs with the context, as the swap's <c>setupArguments</c> and the
/// derived swap's <c>setupFloatingArguments</c> do (<c>fixedvsfloatingswap.cpp:108-140</c>,
/// <c>vanillaswap.cpp:60-80</c>, <c>overnightindexedswap.cpp:188-213</c>, which read the same members of their
/// floating coupons). A floating coupon whose amount can't be computed, as one whose fixing is missing, has a null
/// amount, as QuantLib's <c>try</c> leaves <c>Null&lt;Real&gt;()</c>. Only the members those engines read are kept,
/// with two QuantLib's arguments don't carry: the fixed coupons' accrual periods, which G2's analytic swaption reads
/// for its coupons (the S-sr plan's SSR6 (xiii)), and the floating coupons' fixing dates, by which a discretized swap
/// knows a coupon (SSR6 (xvi)).
/// </remarks>
internal sealed class FixedVsFloatingSwapArguments
{
    private FixedVsFloatingSwapArguments()
    {
    }

    /// <summary>Gets whether the fixed rate is paid or received.</summary>
    public required SwapType Type { get; init; }

    /// <summary>Gets the nominal when it is constant across the coupons of both legs, or <see langword="null"/>.</summary>
    public required double? Nominal { get; init; }

    /// <summary>Gets the fixed coupons' accrual start dates.</summary>
    public required ImmutableArray<Date> FixedResetDates { get; init; }

    /// <summary>Gets the fixed coupons' payment dates.</summary>
    public required ImmutableArray<Date> FixedPayDates { get; init; }

    /// <summary>Gets the fixed coupons' amounts.</summary>
    public required ImmutableArray<double> FixedCoupons { get; init; }

    /// <summary>Gets the fixed coupons' accrual periods.</summary>
    public required ImmutableArray<double> FixedAccrualTimes { get; init; }

    /// <summary>Gets the floating coupons' accrual start dates.</summary>
    public required ImmutableArray<Date> FloatingResetDates { get; init; }

    /// <summary>Gets the floating coupons' payment dates.</summary>
    public required ImmutableArray<Date> FloatingPayDates { get; init; }

    /// <summary>Gets the floating coupons' fixing dates.</summary>
    public required ImmutableArray<Date> FloatingFixingDates { get; init; }

    /// <summary>Gets the floating coupons' accrual periods.</summary>
    public required ImmutableArray<double> FloatingAccrualTimes { get; init; }

    /// <summary>Gets the floating coupons' spreads.</summary>
    public required ImmutableArray<double> FloatingSpreads { get; init; }

    /// <summary>Gets the floating coupons' amounts, each <see langword="null"/> where it can't be computed.</summary>
    public required ImmutableArray<double?> FloatingCoupons { get; init; }

    /// <summary>The arguments of a swap.</summary>
    /// <param name="swap">The swap.</param>
    /// <param name="context">The evaluation context the coupons' amounts read.</param>
    /// <returns>The arguments.</returns>
    internal static FixedVsFloatingSwapArguments Of(FixedVsFloatingSwap swap, EvaluationContext context)
    {
        ImmutableArray<CashFlow> fixedCoupons = swap.FixedLeg;
        int n = fixedCoupons.Length;
        var fixedResetDates = new Date[n];
        var fixedPayDates = new Date[n];
        var fixedAmounts = new double[n];
        var fixedAccrualTimes = new double[n];
        for (int i = 0; i < n; ++i)
        {
            var coupon = (FixedRateCoupon)fixedCoupons[i];
            fixedPayDates[i] = coupon.Date;
            fixedResetDates[i] = coupon.AccrualStartDate;
            fixedAmounts[i] = coupon.Amount(context);
            fixedAccrualTimes[i] = coupon.AccrualPeriod();
        }

        ImmutableArray<CashFlow> floatingCoupons = swap.FloatingLeg;
        int m = floatingCoupons.Length;
        var floatingResetDates = new Date[m];
        var floatingPayDates = new Date[m];
        var floatingFixingDates = new Date[m];
        var floatingAccrualTimes = new double[m];
        var floatingSpreads = new double[m];
        var floatingAmounts = new double?[m];
        for (int i = 0; i < m; ++i)
        {
            var coupon = (FloatingRateCoupon)floatingCoupons[i];
            floatingResetDates[i] = coupon.AccrualStartDate;
            floatingPayDates[i] = coupon.Date;
            floatingFixingDates[i] = coupon.FixingDate();
            floatingAccrualTimes[i] = coupon.AccrualPeriod();
            floatingSpreads[i] = coupon.Spread;
            try
            {
                floatingAmounts[i] = coupon.Amount(context);
            }
            catch (Exception e) when (e is ArgumentException or NanoQLException)
            {
                floatingAmounts[i] = null;
            }
        }

        return new FixedVsFloatingSwapArguments
        {
            Type = swap.Type,
            Nominal = ConstantNominal(swap),
            FixedResetDates = [.. fixedResetDates],
            FixedPayDates = [.. fixedPayDates],
            FixedCoupons = [.. fixedAmounts],
            FixedAccrualTimes = [.. fixedAccrualTimes],
            FloatingResetDates = [.. floatingResetDates],
            FloatingPayDates = [.. floatingPayDates],
            FloatingFixingDates = [.. floatingFixingDates],
            FloatingAccrualTimes = [.. floatingAccrualTimes],
            FloatingSpreads = [.. floatingSpreads],
            FloatingCoupons = [.. floatingAmounts],
        };
    }

    // The swap's nominal when it is constant, as FixedVsFloatingSwap's constructor decides it: the same nominals on
    // both legs, all equal; otherwise QuantLib's Null<Real>().
    private static double? ConstantNominal(FixedVsFloatingSwap swap)
    {
        ImmutableArray<double> fixedNominals = swap.FixedNominals, floatingNominals = swap.FloatingNominals;
        if (fixedNominals.Length != floatingNominals.Length)
        {
            return null;
        }

        for (int i = 0; i < fixedNominals.Length; ++i)
        {
            if (fixedNominals[i] != floatingNominals[i] || fixedNominals[i] != fixedNominals[0])
            {
                return null;
            }
        }

        return swap.Nominal();
    }
}
