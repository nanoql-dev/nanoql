// SPDX-FileCopyrightText: Copyright (C) 2018 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/constnotionalcrosscurrencyfixedvsfloatingswap.hpp ql/instruments/constnotionalcrosscurrencyfixedvsfloatingswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The results an engine returns for a constant-notional cross-currency fixed-vs-floating swap: the cross-currency
/// swap's, with the fair fixed rate and the fair floating spread.
/// </summary>
/// <remarks>
/// The fair rate and spread are <see langword="null"/> where QuantLib's readers throw "Fair fixed rate is not
/// available" and "Fair spread is not available": an expired swap's, or one whose leg's BPS is unknown or zero.
/// </remarks>
public sealed record ConstNotionalCrossCurrencyFixedVsFloatingSwapResults : CrossCurrencySwapResults
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConstNotionalCrossCurrencyFixedVsFloatingSwapResults"/> class.
    /// </summary>
    public ConstNotionalCrossCurrencyFixedVsFloatingSwapResults()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstNotionalCrossCurrencyFixedVsFloatingSwapResults"/> class with
    /// a cross-currency swap's results, and no fair rate or spread.
    /// </summary>
    /// <param name="results">The cross-currency swap's results.</param>
    internal ConstNotionalCrossCurrencyFixedVsFloatingSwapResults(CrossCurrencySwapResults results)
        : base(results)
    {
    }

    /// <summary>Gets the fixed rate that gives the swap a zero NPV.</summary>
    public double? FairFixedRate { get; init; }

    /// <summary>Gets the floating leg's spread that gives the swap a zero NPV.</summary>
    public double? FairSpread { get; init; }

    /// <inheritdoc/>
    public bool Equals(ConstNotionalCrossCurrencyFixedVsFloatingSwapResults? other) =>
        other is not null
        && base.Equals(other)
        && EqualityComparer<double?>.Default.Equals(FairFixedRate, other.FairFixedRate)
        && EqualityComparer<double?>.Default.Equals(FairSpread, other.FairSpread);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), FairFixedRate, FairSpread);
}
