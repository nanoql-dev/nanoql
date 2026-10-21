// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/fx/mtmcrosscurrencybasisswap.hpp ql/experimental/fx/mtmcrosscurrencybasisswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;

namespace NanoQL.Experimental;

/// <summary>
/// The results an engine returns for a mark-to-market cross-currency basis swap: the cross-currency swap's, with each
/// leg's fair spread and the FX rates and notionals of the resettable coupons.
/// </summary>
/// <remarks>
/// A fair spread is <see langword="null"/> where QuantLib's reader throws "Fair FX-base spread is not available" (or
/// FX-quote): an expired swap's, or one whose leg's BPS is unknown. The rates and notionals follow the order of the
/// resettable leg's coupons that haven't occurred; they are empty for an expired swap. Equality compares the arrays
/// element by element.
/// </remarks>
[Experimental("NQL0001")]
public sealed record MtMCrossCurrencyBasisSwapResults : CrossCurrencySwapResults
{
    /// <summary>Initializes a new instance of the <see cref="MtMCrossCurrencyBasisSwapResults"/> class.</summary>
    public MtMCrossCurrencyBasisSwapResults()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MtMCrossCurrencyBasisSwapResults"/> class with a cross-currency
    /// swap's results, no fair spreads and no reset rates.
    /// </summary>
    /// <param name="results">The cross-currency swap's results.</param>
    internal MtMCrossCurrencyBasisSwapResults(CrossCurrencySwapResults results)
        : base(results)
    {
    }

    /// <summary>Gets the FX base-currency leg's spread that gives the swap a zero NPV.</summary>
    public double? FairFxBaseSpread { get; init; }

    /// <summary>Gets the FX quote-currency leg's spread that gives the swap a zero NPV.</summary>
    public double? FairFxQuoteSpread { get; init; }

    /// <summary>Gets the FX rates the resettable coupons' notionals are converted at.</summary>
    public ImmutableArray<double> FxResetRates { get; init; } = [];

    /// <summary>Gets the notionals of the resettable coupons, in the resettable leg's currency.</summary>
    public ImmutableArray<double> FxResetNotionals { get; init; } = [];

    /// <inheritdoc/>
    public bool Equals(MtMCrossCurrencyBasisSwapResults? other) =>
        other is not null
        && base.Equals(other)
        && EqualityComparer<double?>.Default.Equals(FairFxBaseSpread, other.FairFxBaseSpread)
        && EqualityComparer<double?>.Default.Equals(FairFxQuoteSpread, other.FairFxQuoteSpread)
        && Same(FxResetRates, other.FxResetRates)
        && Same(FxResetNotionals, other.FxResetNotionals);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(base.GetHashCode());
        hash.Add(FairFxBaseSpread);
        hash.Add(FairFxQuoteSpread);
        Add(ref hash, FxResetRates);
        Add(ref hash, FxResetNotionals);
        return hash.ToHashCode();
    }

    private static bool Same(ImmutableArray<double> x, ImmutableArray<double> y) =>
        x.IsDefaultOrEmpty ? y.IsDefaultOrEmpty : !y.IsDefault && x.SequenceEqual(y);

    private static void Add(ref HashCode hash, ImmutableArray<double> x)
    {
        hash.Add(x.IsDefault ? 0 : x.Length);
        if (!x.IsDefault)
        {
            foreach (double v in x)
            {
                hash.Add(v);
            }
        }
    }
}
