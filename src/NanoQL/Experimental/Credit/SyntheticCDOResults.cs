// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/syntheticcdo.hpp ql/experimental/credit/syntheticcdo.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A synthetic CDO's results: its legs, its fair rates and the expected tranche losses.</summary>
/// <remarks>
/// The legs are signed by the protection side, as QuantLib's engines sign them; <see cref="PremiumLegNPV"/> and
/// <see cref="ProtectionLegNPV"/> are QuantLib's instrument's, which sign them again (the premium leg's value is always
/// the negative of the premium's size, the protection leg's the protection's size).
/// </remarks>
[Experimental("NQL0001")]
public sealed record SyntheticCDOResults : InstrumentResults
{
    /// <summary>Gets the premium leg's value, signed by the side.</summary>
    public double? PremiumValue { get; init; }

    /// <summary>Gets the protection leg's value, signed by the side.</summary>
    public double? ProtectionValue { get; init; }

    /// <summary>Gets the upfront's value, signed by the side.</summary>
    public double? UpfrontPremiumValue { get; init; }

    /// <summary>Gets the tranche's outstanding notional, levered: its inception notional less the realized tranche
    /// loss and the amortized recovery, times the leverage factor.</summary>
    public double? RemainingNotional { get; init; }

    /// <summary>Gets the lower end of the tranche, the basket's attachment amount: QuantLib's <c>xMin</c>.</summary>
    public double? XMin { get; init; }

    /// <summary>Gets the upper end of the tranche, the basket's detachment amount: QuantLib's <c>xMax</c>.</summary>
    public double? XMax { get; init; }

    /// <summary>Gets the number of integration steps where the expected tranche loss fell, which a consistent model
    /// never gives.</summary>
    public int Error { get; init; }

    /// <summary>Gets the expected tranche losses: at the first accrual start, then at each coupon's end, 0 for the
    /// coupons paid.</summary>
    public ImmutableArray<double> ExpectedTrancheLoss { get; init; } = [];

    /// <summary>Gets the running rate that, with the upfront, zeroes the NPV: QuantLib's instrument's
    /// <c>fairPremium</c>, the running rate times the protection less the upfront over the premium; <see
    /// langword="null"/> where the premium leg is 0, which QuantLib's refuses ("Attempted divide by zero while
    /// calculating syntheticCDO premium.").</summary>
    public double? FairPremium { get; init; }

    /// <summary>
    /// Gets the upfront rate that zeroes the NPV, whichever the side: the protection less the premium, as the seller
    /// values them, over the outstanding notional and the first accrual start's discount; <see langword="null"/> once
    /// the first coupon is paid, when no upfront is due.
    /// </summary>
    public double? FairUpfrontPremium { get; init; }

    /// <summary>Gets the premium leg's value as QuantLib's instrument reads it: the premium's value, negated for a
    /// seller.</summary>
    public double? PremiumLegNPV { get; init; }

    /// <summary>Gets the protection leg's value as QuantLib's instrument reads it: the protection's value, negated
    /// for a buyer.</summary>
    public double? ProtectionLegNPV { get; init; }
}
