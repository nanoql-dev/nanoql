// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/swap.hpp ql/instruments/swap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Equality-tested: tests/NanoQL.Tests/Conventions/ResultsEqualityTests.cs

using System.Collections.Immutable;

namespace NanoQL.Instruments;

/// <summary>The results an engine returns for a swap: the instrument's, and each leg's.</summary>
/// <remarks>
/// A per-leg result QuantLib leaves null is <see langword="null"/>, where QuantLib throws "result not available" on
/// reading it. Equality compares the per-leg arrays element by element.
/// </remarks>
public record SwapResults : InstrumentResults
{
    /// <summary>Gets each leg's NPV, signed by whether the leg is paid.</summary>
    public ImmutableArray<double?> LegNPV { get; init; } = [];

    /// <summary>Gets each leg's basis-point sensitivity, signed by whether the leg is paid.</summary>
    public ImmutableArray<double?> LegBPS { get; init; } = [];

    /// <summary>Gets the discount factor at each leg's start date.</summary>
    public ImmutableArray<double?> StartDiscounts { get; init; } = [];

    /// <summary>Gets the discount factor at each leg's maturity date.</summary>
    public ImmutableArray<double?> EndDiscounts { get; init; } = [];

    /// <summary>Gets the discount factor at the valuation date.</summary>
    public double? NpvDateDiscount { get; init; }

    /// <inheritdoc/>
    public virtual bool Equals(SwapResults? other) =>
        other is not null
        && base.Equals(other)
        && SequenceEquals(LegNPV, other.LegNPV)
        && SequenceEquals(LegBPS, other.LegBPS)
        && SequenceEquals(StartDiscounts, other.StartDiscounts)
        && SequenceEquals(EndDiscounts, other.EndDiscounts)
        && EqualityComparer<double?>.Default.Equals(NpvDateDiscount, other.NpvDateDiscount);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(base.GetHashCode());
        AddSequence(ref hash, LegNPV);
        AddSequence(ref hash, LegBPS);
        AddSequence(ref hash, StartDiscounts);
        AddSequence(ref hash, EndDiscounts);
        hash.Add(NpvDateDiscount);
        return hash.ToHashCode();
    }
}
