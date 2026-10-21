// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/crosscurrencyswap.hpp ql/instruments/crosscurrencyswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Instruments;

/// <summary>
/// The results an engine returns for a cross-currency swap: the swap's, in the NPV currency, with each leg's NPV and
/// BPS in its own currency and its discount factor at the NPV date.
/// </summary>
/// <remarks>
/// Unsealed, as QuantLib's, since the constant-notional and mark-to-market swaps' results derive from it. A per-leg
/// result QuantLib leaves null is <see langword="null"/>. Equality compares the per-leg arrays element by element.
/// </remarks>
public record CrossCurrencySwapResults : SwapResults
{
    /// <summary>Initializes a new instance of the <see cref="CrossCurrencySwapResults"/> class.</summary>
    public CrossCurrencySwapResults()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrossCurrencySwapResults"/> class with a swap's results, and no
    /// in-currency results.
    /// </summary>
    /// <param name="results">The swap's results.</param>
    internal CrossCurrencySwapResults(SwapResults results)
        : base(results)
    {
    }

    /// <summary>Gets each leg's NPV in its own currency, signed by whether the leg is paid.</summary>
    public ImmutableArray<double?> InCcyLegNPV { get; init; } = [];

    /// <summary>Gets each leg's basis-point sensitivity in its own currency, signed by whether the leg is
    /// paid.</summary>
    public ImmutableArray<double?> InCcyLegBPS { get; init; } = [];

    /// <summary>Gets each leg's discount factor at the NPV date, on its own currency's curve.</summary>
    public ImmutableArray<double?> NpvDateDiscounts { get; init; } = [];

    /// <inheritdoc/>
    public virtual bool Equals(CrossCurrencySwapResults? other) =>
        other is not null
        && base.Equals(other)
        && SequenceEquals(InCcyLegNPV, other.InCcyLegNPV)
        && SequenceEquals(InCcyLegBPS, other.InCcyLegBPS)
        && SequenceEquals(NpvDateDiscounts, other.NpvDateDiscounts);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(base.GetHashCode());
        AddSequence(ref hash, InCcyLegNPV);
        AddSequence(ref hash, InCcyLegBPS);
        AddSequence(ref hash, NpvDateDiscounts);
        return hash.ToHashCode();
    }
}
