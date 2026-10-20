// SPDX-FileCopyrightText: Copyright (C) 2007, 2009, 2011 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/cpiswap.hpp ql/instruments/cpiswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Instruments;

/// <summary>
/// The results an engine returns for a <see cref="CPISwap"/>: the swap's, with the fair fixed rate of the CPI leg and
/// the fair spread over the floating leg.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's swap stores these from its own results class, or derives them from the swap's results; here they are
/// members of the results <see cref="CPISwap.FetchResults"/> returns. The CPI leg is the first leg and the floating leg
/// the second.
/// </para>
/// <para>
/// The fair rate is the engine's: <see cref="PricingEngines.DiscountingSwapEngine"/>'s <see cref="CPISwap"/> overload
/// gives it, where QuantLib derives it from the CPI leg's BPS, which leaves out the index ratio that a CPI coupon's
/// sensitivity to its rate carries, so the swap repriced at QuantLib's fair rate isn't worth zero (a documented
/// divergence). The fair spread is derived from the floating leg's BPS, as QuantLib derives it. A result QuantLib
/// refuses to read ("result not available") is <see langword="null"/>: the fair rate when the engine gives none or the
/// CPI leg has no live coupon, the fair spread when the floating leg's BPS is zero or unknown, both for an expired
/// swap, and a leg's NPV when it is unknown. Equality compares the per-leg arrays element by element, and the fair rate
/// and spread by value.
/// </para>
/// </remarks>
public sealed record CPISwapResults : SwapResults
{
    /// <summary>Initializes a new instance of the <see cref="CPISwapResults"/> class.</summary>
    public CPISwapResults()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CPISwapResults"/> class with a swap's results, and neither a fair
    /// rate nor a fair spread.
    /// </summary>
    /// <param name="results">The swap's results.</param>
    internal CPISwapResults(SwapResults results)
        : base(results)
    {
    }

    /// <summary>Gets the fixed rate of the CPI leg that gives the swap a zero NPV.</summary>
    public double? FairRate { get; init; }

    /// <summary>Gets the spread over the floating leg that gives the swap a zero NPV.</summary>
    public double? FairSpread { get; init; }

    /// <summary>Gets the CPI leg's NPV, signed by whether it is paid.</summary>
    public double? FixedLegNPV => Element(LegNPV, 0);

    /// <summary>Gets the floating leg's NPV, signed by whether it is paid.</summary>
    public double? FloatLegNPV => Element(LegNPV, 1);

    /// <inheritdoc/>
    public bool Equals(CPISwapResults? other) =>
        other is not null
        && base.Equals(other)
        && EqualityComparer<double?>.Default.Equals(FairRate, other.FairRate)
        && EqualityComparer<double?>.Default.Equals(FairSpread, other.FairSpread);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), FairRate, FairSpread);

    private static double? Element(ImmutableArray<double?> values, int j) =>
        !values.IsDefault && j < values.Length ? values[j] : null;
}
