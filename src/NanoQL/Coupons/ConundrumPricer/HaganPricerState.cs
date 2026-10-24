// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2023 Andre Miemiec
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/conundrumpricer.hpp ql/cashflows/conundrumpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// What QuantLib's <c>HaganPricer::initialize(coupon)</c> stores in the pricer for its queries, built for one call
/// (the S-cms plan's CMR1 (i)): the coupon's terms, the discount, and while its fixing is in the future the swap's rate
/// and annuity, the g-function and the option pricer the replication integrates.
/// </summary>
internal sealed class HaganPricerState
{
    public required CmsCoupon Coupon { get; init; }

    public required EvaluationContext Context { get; init; }

    public required double Gearing { get; init; }

    public required double Spread { get; init; }

    public required double AccrualPeriod { get; init; }

    public required Date FixingDate { get; init; }

    public required Date PaymentDate { get; init; }

    public required Date Today { get; init; }

    public required double Discount { get; init; }

    public required double SpreadLegValue { get; init; }

    // Set while the fixing is in the future.
    public SwaptionVolatilityStructure? Volatility { get; init; }

    public Period SwapTenor { get; init; }

    public double SwapRateValue { get; init; }

    public double Annuity { get; init; }

    public GFunction? GFunction { get; init; }

    public VanillaOptionPricer? VanillaOptionPricer { get; init; }

    public bool IsFixed => FixingDate <= Today;

    public SwapIndex SwapIndex => Coupon.SwapIndex;
}
