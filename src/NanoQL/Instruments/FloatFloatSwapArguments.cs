// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/floatfloatswap.hpp ql/instruments/floatfloatswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// QuantLib's <c>FloatFloatSwap::arguments</c>, which its <c>setupArguments</c> fills from the legs: per cash flow, in
/// each leg's order, the reset, fixing and payment dates, the amounts, the accrual times, spreads, gearings, caps and
/// floors, and whether it is a redemption, a redemption taking the dates of the coupon paid with it.
/// </summary>
/// <remarks>
/// A coupon whose amount can't be computed (a missing fixing, a CMS coupon without a pricer) has no amount, and an
/// uncapped or unfloored one no cap or floor, QuantLib's <c>Null&lt;Real&gt;</c>. QuantLib's <c>validate</c> checks the
/// vectors' sizes against the nominals', which the builder and the swap give equal.
/// </remarks>
internal sealed class FloatFloatSwapArguments
{
    private FloatFloatSwapArguments()
    {
    }

    public required SwapType Type { get; init; }

    public required ImmutableArray<double> Nominal1 { get; init; }

    public required ImmutableArray<double> Nominal2 { get; init; }

    public required InterestRateIndex Index1 { get; init; }

    public required InterestRateIndex Index2 { get; init; }

    public required Leg Leg1 { get; init; }

    public required Leg Leg2 { get; init; }

    // QuantLib's setupArguments (floatfloatswap.cpp:391-525).
    internal static FloatFloatSwapArguments Of(FloatFloatSwap swap, EvaluationContext context) => new()
    {
        Type = swap.Type,
        Nominal1 = swap.Nominal1,
        Nominal2 = swap.Nominal2,
        Index1 = swap.Index1,
        Index2 = swap.Index2,
        Leg1 = Leg.Of(swap.Leg1, context),
        Leg2 = Leg.Of(swap.Leg2, context),
    };

    // One leg's vectors.
    internal sealed class Leg
    {
        private Leg()
        {
        }

        public required ImmutableArray<Date> ResetDates { get; init; }

        public required ImmutableArray<Date> FixingDates { get; init; }

        public required ImmutableArray<Date> PayDates { get; init; }

        public required ImmutableArray<double> Spreads { get; init; }

        public required ImmutableArray<double> Gearings { get; init; }

        public required ImmutableArray<double?> CappedRates { get; init; }

        public required ImmutableArray<double?> FlooredRates { get; init; }

        public required ImmutableArray<double?> Coupons { get; init; }

        public required ImmutableArray<double> AccrualTimes { get; init; }

        public required ImmutableArray<bool> IsRedemptionFlow { get; init; }

        internal static Leg Of(ImmutableArray<CashFlow> legCoupons, EvaluationContext context)
        {
            int n = legCoupons.Length;
            var resetDates = new Date[n];
            var payDates = new Date[n];
            var fixingDates = new Date[n];
            var spreads = new double[n];
            var accrualTimes = new double[n];
            var gearings = new double[n];
            var coupons = new double?[n];
            var isRedemptionFlow = new bool[n];
            var cappedRates = new double?[n];
            var flooredRates = new double?[n];
            for (int i = 0; i < n; ++i)
            {
                if (legCoupons[i] is FloatingRateCoupon coupon)
                {
                    accrualTimes[i] = coupon.AccrualPeriod();
                    payDates[i] = coupon.Date;
                    resetDates[i] = coupon.AccrualStartDate;
                    fixingDates[i] = coupon.FixingDate();
                    spreads[i] = coupon.Spread;
                    gearings[i] = coupon.Gearing;
                    try
                    {
                        coupons[i] = coupon.Amount(context);
                    }
                    catch (Exception e) when (e is ArgumentException or NanoQLException)
                    {
                        coupons[i] = null;
                    }

                    if (legCoupons[i] is CappedFlooredCoupon cfcoupon)
                    {
                        cappedRates[i] = cfcoupon.Cap;
                        flooredRates[i] = cfcoupon.Floor;
                    }
                }
                else
                {
                    CashFlow cashflow = legCoupons[i];
                    int j = CouponOf(payDates, i, cashflow.Date);
                    isRedemptionFlow[i] = true;
                    coupons[i] = cashflow.Amount(context);
                    resetDates[i] = resetDates[j];
                    fixingDates[i] = fixingDates[j];
                    accrualTimes[i] = 0.0;
                    spreads[i] = 0.0;
                    gearings[i] = 1.0;
                    payDates[i] = cashflow.Date;
                }
            }

            return new Leg
            {
                ResetDates = [.. resetDates],
                FixingDates = [.. fixingDates],
                PayDates = [.. payDates],
                Spreads = [.. spreads],
                Gearings = [.. gearings],
                CappedRates = [.. cappedRates],
                FlooredRates = [.. flooredRates],
                Coupons = [.. coupons],
                AccrualTimes = [.. accrualTimes],
                IsRedemptionFlow = [.. isRedemptionFlow],
            };
        }

        // The coupon a redemption is paid with: the first earlier cash flow paid on its date. QuantLib searches the
        // whole vector, whose later entries are still null dates; the swap's constructors put each redemption after its
        // coupon.
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
}
