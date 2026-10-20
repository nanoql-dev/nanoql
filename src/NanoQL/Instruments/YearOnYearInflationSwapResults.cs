// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/yearonyearinflationswap.hpp ql/instruments/yearonyearinflationswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Instruments;

/// <summary>
/// The results an engine returns for a <see cref="YearOnYearInflationSwap"/>: the swap's, with the fair fixed rate and
/// the fair spread over the year-on-year leg.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's swap stores these from its own results class, or derives them from the swap's results; here they are
/// members of the results <see cref="YearOnYearInflationSwap.FetchResults"/> returns. The fixed leg is the first leg
/// and the year-on-year leg the second.
/// </para>
/// <para>
/// A result QuantLib refuses to read ("result not available") is <see langword="null"/>: the fair rate when the fixed
/// leg's BPS is zero or unknown, the fair spread likewise for the year-on-year leg, both for an expired swap, and a
/// leg's NPV when it is unknown. Equality compares the per-leg arrays element by element, and the fair rate and spread
/// by value.
/// </para>
/// </remarks>
public sealed record YearOnYearInflationSwapResults : SwapResults
{
    /// <summary>Initializes a new instance of the <see cref="YearOnYearInflationSwapResults"/> class.</summary>
    public YearOnYearInflationSwapResults()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YearOnYearInflationSwapResults"/> class with a swap's results, and
    /// neither a fair rate nor a fair spread.
    /// </summary>
    /// <param name="results">The swap's results.</param>
    internal YearOnYearInflationSwapResults(SwapResults results)
        : base(results)
    {
    }

    /// <summary>Gets the fixed rate that gives the swap a zero NPV.</summary>
    public double? FairRate { get; init; }

    /// <summary>Gets the spread over the year-on-year leg that gives the swap a zero NPV.</summary>
    public double? FairSpread { get; init; }

    /// <summary>Gets the fixed leg's NPV, signed by whether it is paid.</summary>
    public double? FixedLegNPV => Element(LegNPV, 0);

    /// <summary>Gets the year-on-year leg's NPV, signed by whether it is paid.</summary>
    public double? YoYLegNPV => Element(LegNPV, 1);

    /// <inheritdoc/>
    public bool Equals(YearOnYearInflationSwapResults? other) =>
        other is not null
        && base.Equals(other)
        && EqualityComparer<double?>.Default.Equals(FairRate, other.FairRate)
        && EqualityComparer<double?>.Default.Equals(FairSpread, other.FairSpread);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), FairRate, FairSpread);

    private static double? Element(ImmutableArray<double?> values, int j) =>
        !values.IsDefault && j < values.Length ? values[j] : null;
}
