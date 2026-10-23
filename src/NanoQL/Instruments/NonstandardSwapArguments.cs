// SPDX-FileCopyrightText: Copyright (C) 2013, 2016 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/nonstandardswap.hpp ql/instruments/nonstandardswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// QuantLib's <c>NonstandardSwap::arguments</c>, which its <c>setupArguments</c> fills from the legs: per cash flow, in
/// the legs' order, the reset, fixing and payment dates, the amounts, the accrual times, spreads and gearings, and
/// whether it is a redemption, a redemption taking the dates of the coupon paid with it (the S-g1d plan's G1D6).
/// </summary>
/// <remarks>
/// A floating coupon whose amount can't be computed (a missing fixing) has no amount, QuantLib's
/// <c>Null&lt;Real&gt;</c>. QuantLib's <c>validate</c> checks the vectors' sizes against each other, which the builder
/// gives equal.
/// </remarks>
internal sealed class NonstandardSwapArguments
{
    private NonstandardSwapArguments()
    {
    }

    public required SwapType Type { get; init; }

    public required ImmutableArray<double> FixedNominal { get; init; }

    public required ImmutableArray<double> FloatingNominal { get; init; }

    public required ImmutableArray<Date> FixedResetDates { get; init; }

    public required ImmutableArray<Date> FixedPayDates { get; init; }

    public required ImmutableArray<double> FloatingAccrualTimes { get; init; }

    public required ImmutableArray<Date> FloatingResetDates { get; init; }

    public required ImmutableArray<Date> FloatingFixingDates { get; init; }

    public required ImmutableArray<Date> FloatingPayDates { get; init; }

    public required ImmutableArray<double> FixedCoupons { get; init; }

    public required ImmutableArray<double> FixedRate { get; init; }

    public required ImmutableArray<double> FloatingSpreads { get; init; }

    public required ImmutableArray<double> FloatingGearings { get; init; }

    public required ImmutableArray<double?> FloatingCoupons { get; init; }

    public required IborIndex IborIndex { get; init; }

    public required ImmutableArray<bool> FixedIsRedemptionFlow { get; init; }

    public required ImmutableArray<bool> FloatingIsRedemptionFlow { get; init; }

    // QuantLib's setupArguments (nonstandardswap.cpp:276-370).
    internal static NonstandardSwapArguments Of(NonstandardSwap swap, EvaluationContext context)
    {
        ImmutableArray<CashFlow> fixedCoupons = swap.FixedLeg;
        int n = fixedCoupons.Length;
        var fixedResetDates = new Date[n];
        var fixedPayDates = new Date[n];
        var fixedAmounts = new double[n];
        var fixedIsRedemptionFlow = new bool[n];
        for (int i = 0; i < n; ++i)
        {
            if (fixedCoupons[i] is FixedRateCoupon coupon)
            {
                fixedPayDates[i] = coupon.Date;
                fixedResetDates[i] = coupon.AccrualStartDate;
                fixedAmounts[i] = coupon.Amount(context);
            }
            else
            {
                CashFlow cashflow = fixedCoupons[i];
                int j = CouponOf(fixedPayDates, i, cashflow.Date);
                fixedIsRedemptionFlow[i] = true;
                fixedAmounts[i] = cashflow.Amount(context);
                fixedResetDates[i] = fixedResetDates[j];
                fixedPayDates[i] = cashflow.Date;
            }
        }

        ImmutableArray<CashFlow> floatingCoupons = swap.FloatingLeg;
        int m = floatingCoupons.Length;
        var floatingResetDates = new Date[m];
        var floatingPayDates = new Date[m];
        var floatingFixingDates = new Date[m];
        var floatingAccrualTimes = new double[m];
        var floatingSpreads = new double[m];
        var floatingGearings = new double[m];
        var floatingAmounts = new double?[m];
        var floatingIsRedemptionFlow = new bool[m];
        for (int i = 0; i < m; ++i)
        {
            // FloatingRateCoupon covers both Ibor and compounded overnight
            // coupons; every accessor used below lives on the base class
            if (floatingCoupons[i] is FloatingRateCoupon coupon)
            {
                floatingResetDates[i] = coupon.AccrualStartDate;
                floatingPayDates[i] = coupon.Date;
                floatingFixingDates[i] = coupon.FixingDate();
                floatingAccrualTimes[i] = coupon.AccrualPeriod();
                floatingSpreads[i] = coupon.Spread;
                floatingGearings[i] = coupon.Gearing;
                try
                {
                    floatingAmounts[i] = coupon.Amount(context);
                }
                catch (Exception e) when (e is ArgumentException or NanoQLException)
                {
                    floatingAmounts[i] = null;
                }
            }
            else
            {
                CashFlow cashflow = floatingCoupons[i];
                int j = CouponOf(floatingPayDates, i, cashflow.Date);
                floatingIsRedemptionFlow[i] = true;
                floatingAmounts[i] = cashflow.Amount(context);
                floatingResetDates[i] = floatingResetDates[j];
                floatingFixingDates[i] = floatingFixingDates[j];
                floatingAccrualTimes[i] = 0.0;
                floatingSpreads[i] = 0.0;
                floatingGearings[i] = 1.0;
                floatingPayDates[i] = cashflow.Date;
            }
        }

        return new NonstandardSwapArguments
        {
            Type = swap.Type,
            FixedNominal = swap.FixedNominal,
            FloatingNominal = swap.FloatingNominal,
            FixedRate = swap.FixedRate,
            FixedResetDates = [.. fixedResetDates],
            FixedPayDates = [.. fixedPayDates],
            FixedCoupons = [.. fixedAmounts],
            FixedIsRedemptionFlow = [.. fixedIsRedemptionFlow],
            FloatingResetDates = [.. floatingResetDates],
            FloatingPayDates = [.. floatingPayDates],
            FloatingFixingDates = [.. floatingFixingDates],
            FloatingAccrualTimes = [.. floatingAccrualTimes],
            FloatingSpreads = [.. floatingSpreads],
            FloatingGearings = [.. floatingGearings],
            FloatingCoupons = [.. floatingAmounts],
            FloatingIsRedemptionFlow = [.. floatingIsRedemptionFlow],
            IborIndex = swap.IborIndex,
        };
    }

    // The coupon a redemption is paid with: the first earlier cash flow paid on its date. QuantLib searches the whole
    // vector, whose later entries are still null dates; the swap's constructors put each redemption after its coupon.
    private static int CouponOf(Date[] payDates, int i, Date date)
    {
        for (int j = 0; j < i; j++)
        {
            if (payDates[j] == date)
            {
                return j;
            }
        }

        throw new NanoQLException($"nominal redemption on {date}has no corresponding coupon");
    }
}
