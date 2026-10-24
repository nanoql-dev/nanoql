// SPDX-FileCopyrightText: Copyright (C) 2008 Toyin Akin
// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/multipleresetscoupon.hpp ql/cashflows/multipleresetscoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>The averaging pricer of multiple-resets coupons: each reset's fixing weighted by its accrual time, over the
/// accrual period, geared and spread.</summary>
public sealed class AveragingMultipleResetsPricer : MultipleResetsPricer
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The coupon isn't a multiple-resets coupon.</exception>
    /// <exception cref="NanoQLException">The coupon's accrual period is 0.</exception>
    public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context)
    {
        // past or future fixing is managed in InterestRateIndex::fixing()
        (MultipleResetsCoupon c, double[] subPeriodFixings) = Fixings(coupon, context);
        double aggregateFactor = 0.0;
        for (int i = 0; i < subPeriodFixings.Length; i++)
        {
            aggregateFactor += subPeriodFixings[i] * c.Dt[i];
        }

        double rate = aggregateFactor / c.AccrualPeriod();
        return (c.Gearing * rate) + c.Spread;
    }
}
