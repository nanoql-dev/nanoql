// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/constnotionalcrosscurrencybasisswap.hpp ql/instruments/constnotionalcrosscurrencybasisswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The results an engine returns for a constant-notional cross-currency basis swap: the cross-currency swap's, with
/// each leg's fair spread.
/// </summary>
/// <remarks>
/// A fair spread is <see langword="null"/> where QuantLib's reader throws "Fair pay spread is not available": an
/// expired swap's, or one whose leg's BPS is unknown or zero.
/// </remarks>
public sealed record ConstNotionalCrossCurrencyBasisSwapResults : CrossCurrencySwapResults
{
    /// <summary>Initializes a new instance of the <see cref="ConstNotionalCrossCurrencyBasisSwapResults"/>
    /// class.</summary>
    public ConstNotionalCrossCurrencyBasisSwapResults()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstNotionalCrossCurrencyBasisSwapResults"/> class with a
    /// cross-currency swap's results, and no fair spreads.
    /// </summary>
    /// <param name="results">The cross-currency swap's results.</param>
    internal ConstNotionalCrossCurrencyBasisSwapResults(CrossCurrencySwapResults results)
        : base(results)
    {
    }

    /// <summary>Gets the pay leg's spread that gives the swap a zero NPV.</summary>
    public double? FairPaySpread { get; init; }

    /// <summary>Gets the receive leg's spread that gives the swap a zero NPV.</summary>
    public double? FairRecSpread { get; init; }

    /// <inheritdoc/>
    public bool Equals(ConstNotionalCrossCurrencyBasisSwapResults? other) =>
        other is not null
        && base.Equals(other)
        && EqualityComparer<double?>.Default.Equals(FairPaySpread, other.FairPaySpread)
        && EqualityComparer<double?>.Default.Equals(FairRecSpread, other.FairRecSpread);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), FairPaySpread, FairRecSpread);
}
