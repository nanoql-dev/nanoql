// SPDX-FileCopyrightText: Copyright (C) 2014, 2016 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/lineartsrpricer.hpp ql/cashflows/lineartsrpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// What QuantLib's <c>LinearTsrPricer::initialize(coupon)</c> stores in the pricer for its queries, built for one call
/// (the S-cms plan's CMR1 (i)): the coupon's terms and discounts, and while its fixing is in the future the swap's rate
/// and annuity, the smile section, the adjusted bounds and the linear model's coefficients.
/// </summary>
internal sealed class LinearTsrPricerState
{
    public required CmsCoupon Coupon { get; init; }

    public required EvaluationContext Context { get; init; }

    public required double Gearing { get; init; }

    public required double Spread { get; init; }

    public required double AccrualPeriod { get; init; }

    public required Date FixingDate { get; init; }

    public required Date PaymentDate { get; init; }

    public required Date Today { get; init; }

    public required double DiscountCurvePaymentDiscount { get; init; }

    public required double CouponDiscountRatio { get; init; }

    public required double SpreadLegValue { get; init; }

    // Set while the fixing is in the future.
    public Period SwapTenor { get; init; }

    public double SwapRateValue { get; init; }

    public double Annuity { get; init; }

    public SmileSection? SmileSection { get; init; }

    public double AdjustedLowerBound { get; init; }

    public double AdjustedUpperBound { get; init; }

    public double A { get; init; }

    public double B { get; init; }

    public bool IsFixed => FixingDate <= Today;

    public SwapIndex SwapIndex => Coupon.SwapIndex;
}
