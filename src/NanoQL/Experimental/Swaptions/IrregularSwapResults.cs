// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2010 Andre Miemiec
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/swaptions/irregularswap.hpp ql/experimental/swaptions/irregularswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;

namespace NanoQL.Experimental;

/// <summary>
/// The results an engine returns for an irregular swap: the swap's, with the fair fixed rate and the fair floating
/// spread: QuantLib's <c>IrregularSwap::results</c>.
/// </summary>
/// <remarks>
/// A result QuantLib leaves null is <see langword="null"/>, where QuantLib throws "result not available" on reading it.
/// The fair rate and the fair spread are null on a swap engine's results and on an expired swap's. The leg accessors
/// are QuantLib's instrument accessors, read from the per-leg arrays: the fixed leg is the first leg and the floating
/// leg the second. Equality compares the per-leg arrays element by element, and the fair rate and spread by value.
/// </remarks>
[Experimental("NQL0001")]
public sealed record IrregularSwapResults : SwapResults
{
    /// <summary>Initializes a new instance of the <see cref="IrregularSwapResults"/> class.</summary>
    public IrregularSwapResults()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IrregularSwapResults"/> class with a swap's results, and neither a
    /// fair rate nor a fair spread.
    /// </summary>
    /// <param name="results">The swap's results.</param>
    internal IrregularSwapResults(SwapResults results)
        : base(results)
    {
    }

    /// <summary>Gets the fixed rate that gives the swap a zero NPV, an irregular-swap engine's.</summary>
    public double? FairRate { get; init; }

    /// <summary>Gets the floating spread that gives the swap a zero NPV, an irregular-swap engine's.</summary>
    public double? FairSpread { get; init; }

    /// <summary>Gets the fixed leg's basis-point sensitivity, signed by whether it is paid.</summary>
    public double? FixedLegBPS => Element(LegBPS, 0);

    /// <summary>Gets the fixed leg's NPV, signed by whether it is paid.</summary>
    public double? FixedLegNPV => Element(LegNPV, 0);

    /// <summary>Gets the floating leg's basis-point sensitivity, signed by whether it is paid.</summary>
    public double? FloatingLegBPS => Element(LegBPS, 1);

    /// <summary>Gets the floating leg's NPV, signed by whether it is paid.</summary>
    public double? FloatingLegNPV => Element(LegNPV, 1);

    /// <inheritdoc/>
    public bool Equals(IrregularSwapResults? other) =>
        other is not null
        && base.Equals(other)
        && EqualityComparer<double?>.Default.Equals(FairRate, other.FairRate)
        && EqualityComparer<double?>.Default.Equals(FairSpread, other.FairSpread);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), FairRate, FairSpread);

    private static double? Element(ImmutableArray<double?> values, int j) =>
        !values.IsDefault && j < values.Length ? values[j] : null;
}
