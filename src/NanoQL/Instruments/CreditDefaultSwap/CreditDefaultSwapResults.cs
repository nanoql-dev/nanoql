// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/creditdefaultswap.hpp ql/instruments/creditdefaultswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The results an engine returns for a <see cref="CreditDefaultSwap"/>: the instrument's, with the fair running spread
/// and upfront, and the legs' values and sensitivities.
/// </summary>
/// <remarks>
/// A result QuantLib leaves at <c>Null&lt;Real&gt;()</c>, whose accessor refuses it ("… not available"), is
/// <see langword="null"/>: the fair spread where the coupon leg is worth nothing, the fair upfront where the upfront
/// has occurred, the coupon-leg BPS for a zero running spread, the upfront BPS without an upfront. Equality is by
/// value.
/// </remarks>
public sealed record CreditDefaultSwapResults : InstrumentResults
{
    /// <summary>Gets the running spread that, given the recovery rate, gives the running-only swap a zero NPV, ignoring
    /// any upfront.</summary>
    public double? FairSpread { get; init; }

    /// <summary>Gets the upfront that, given the running spread and the recovery rate, gives the swap a zero
    /// NPV.</summary>
    public double? FairUpfront { get; init; }

    /// <summary>Gets the change of the coupon leg's value for a one-basis-point change of the running spread.</summary>
    public double? CouponLegBPS { get; init; }

    /// <summary>Gets the coupon leg's value, signed by the side.</summary>
    public double? CouponLegNPV { get; init; }

    /// <summary>Gets the default leg's value, signed by the side.</summary>
    public double? DefaultLegNPV { get; init; }

    /// <summary>Gets the change of the upfront's value for a one-basis-point change of the upfront.</summary>
    public double? UpfrontBPS { get; init; }

    /// <summary>Gets the upfront's value, signed by the side.</summary>
    public double? UpfrontNPV { get; init; }

    /// <summary>Gets the accrual rebate's value, signed by the side.</summary>
    public double? AccrualRebateNPV { get; init; }
}
