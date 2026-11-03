// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/assetswap.hpp ql/instruments/assetswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The results of an <see cref="AssetSwap"/>'s calculation: the swap's results, and the fair spread, clean price and
/// non-par repayment derived from them.
/// </summary>
/// <remarks>
/// QuantLib's <c>fairSpread()</c>, <c>fairCleanPrice()</c> and <c>fairNonParRepayment()</c> compute these from the
/// swap's results on reading and cache them; here <see cref="AssetSwap.FetchResults"/> derives them in the engine's
/// results step. A value that can't be derived is <see langword="null"/>, and <see cref="AssetSwap"/>'s members that
/// read it refuse with QuantLib's texts. Equality compares the swap's results and the three values.
/// </remarks>
public sealed record AssetSwapResults : SwapResults
{
    /// <summary>Initializes a new instance of the <see cref="AssetSwapResults"/> class.</summary>
    public AssetSwapResults()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AssetSwapResults"/> class with a swap's results, and no
    /// fair values.</summary>
    /// <param name="results">The swap's results.</param>
    internal AssetSwapResults(SwapResults results)
        : base(results)
    {
    }

    /// <summary>Gets the spread that gives the swap a zero NPV.</summary>
    public double? FairSpread { get; init; }

    /// <summary>Gets the bond's clean price that gives the swap a zero NPV, per 100 of notional.</summary>
    public double? FairCleanPrice { get; init; }

    /// <summary>Gets the final repayment that gives the swap a zero NPV, per 100 of notional.</summary>
    public double? FairNonParRepayment { get; init; }

    /// <inheritdoc/>
    public bool Equals(AssetSwapResults? other) =>
        other is not null
        && base.Equals(other)
        && EqualityComparer<double?>.Default.Equals(FairSpread, other.FairSpread)
        && EqualityComparer<double?>.Default.Equals(FairCleanPrice, other.FairCleanPrice)
        && EqualityComparer<double?>.Default.Equals(FairNonParRepayment, other.FairNonParRepayment);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(base.GetHashCode(), FairSpread, FairCleanPrice, FairNonParRepayment);
}
