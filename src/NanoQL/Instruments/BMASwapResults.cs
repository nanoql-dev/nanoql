// SPDX-FileCopyrightText: Copyright (C) 2007 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bmaswap.hpp ql/instruments/bmaswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Instruments;

/// <summary>
/// The results an engine returns for a <see cref="BMASwap"/>: the swap's, with QuantLib's inspectors derived from them,
/// each leg's NPV and BPS, the fair Libor fraction and the fair Libor spread.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's <c>BMASwap</c> derives these inspectors from its swap results on demand; here they are members of the
/// results the swap's <see cref="BMASwap.FetchResults"/> returns. The Libor leg is the first leg and the BMA leg the
/// second.
/// </para>
/// <para>
/// A result QuantLib refuses to read is <see langword="null"/>: a leg's NPV or BPS it leaves null ("result not
/// available"), and the fair Libor fraction when the Libor leg's NPV without its spread is zero ("result not available
/// (null libor NPV)"), as for an expired swap, or when an input to it is null; and the fair Libor spread when the NPV
/// or the Libor leg's BPS is null. A defined but infinite or NaN value is kept, as QuantLib computes it: an expired
/// swap's fair Libor spread divides zero by zero. Equality compares the per-leg arrays element by element, and the two
/// fair values by value.
/// </para>
/// </remarks>
public sealed record BMASwapResults : SwapResults
{
    /// <summary>Initializes a new instance of the <see cref="BMASwapResults"/> class.</summary>
    public BMASwapResults()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BMASwapResults"/> class with a swap's results, and neither fair
    /// value.
    /// </summary>
    /// <param name="results">The swap's results.</param>
    internal BMASwapResults(SwapResults results)
        : base(results)
    {
    }

    /// <summary>Gets the Libor fraction that gives the swap a zero NPV.</summary>
    public double? FairLiborFraction { get; init; }

    /// <summary>Gets the spread over the Libor leg that gives the swap a zero NPV.</summary>
    public double? FairLiborSpread { get; init; }

    /// <summary>Gets the Libor leg's basis-point sensitivity, signed by whether it is paid.</summary>
    public double? LiborLegBPS => Element(LegBPS, 0);

    /// <summary>Gets the Libor leg's NPV, signed by whether it is paid.</summary>
    public double? LiborLegNPV => Element(LegNPV, 0);

    /// <summary>Gets the BMA leg's basis-point sensitivity, signed by whether it is paid.</summary>
    public double? BmaLegBPS => Element(LegBPS, 1);

    /// <summary>Gets the BMA leg's NPV, signed by whether it is paid.</summary>
    public double? BmaLegNPV => Element(LegNPV, 1);

    /// <inheritdoc/>
    public bool Equals(BMASwapResults? other) =>
        other is not null
        && base.Equals(other)
        && EqualityComparer<double?>.Default.Equals(FairLiborFraction, other.FairLiborFraction)
        && EqualityComparer<double?>.Default.Equals(FairLiborSpread, other.FairLiborSpread);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), FairLiborFraction, FairLiborSpread);

    private static double? Element(ImmutableArray<double?> values, int j) =>
        !values.IsDefault && j < values.Length ? values[j] : null;
}
