// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/btp.hpp ql/instruments/bonds/btp.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Utilities;

namespace NanoQL.Instruments;

/// <summary>
/// The basket of BTPs the Rendistato index is computed on: each bond with its outstanding amount and clean price, and
/// its weight, its outstanding over the basket's.
/// </summary>
/// <remarks>
/// Immutable; its clean prices are values where QuantLib's are quote handles the basket observes.
/// </remarks>
public sealed class RendistatoBasket
{
    /// <summary>Initializes a new instance of the <see cref="RendistatoBasket"/> class.</summary>
    /// <param name="btps">The bonds.</param>
    /// <param name="outstandings">The outstanding amount of each bond, non-negative.</param>
    /// <param name="cleanPrices">The clean price of each bond, per 100 of notional.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>, or holds a null bond.</exception>
    /// <exception cref="ArgumentException">
    /// The basket is empty, the three lists' sizes differ, or an outstanding amount is negative.
    /// </exception>
    public RendistatoBasket(IEnumerable<BTP> btps, IEnumerable<double> outstandings, IEnumerable<double> cleanPrices)
    {
        ArgumentNullException.ThrowIfNull(btps);
        ArgumentNullException.ThrowIfNull(outstandings);
        ArgumentNullException.ThrowIfNull(cleanPrices);
        Btps = [.. btps];
        Outstandings = [.. outstandings];
        CleanPrices = [.. cleanPrices];
        foreach (BTP btp in Btps)
        {
            ArgumentNullException.ThrowIfNull(btp, nameof(btps));
        }

        if (Btps.IsEmpty)
        {
            throw new ArgumentException("empty RendistatoCalculator Basket", nameof(btps));
        }

        int k = Btps.Length;
        if (Outstandings.Length != k)
        {
            throw new ArgumentException(
                $"mismatch between number of BTPs ({k}) and number of outstandings ({Outstandings.Length})",
                nameof(outstandings));
        }

        if (CleanPrices.Length != k)
        {
            throw new ArgumentException(
                $"mismatch between number of BTPs ({k}) and number of clean prices quotes ({CleanPrices.Length})",
                nameof(cleanPrices));
        }

        // require non-negative outstanding
        for (int i = 0; i < k; ++i)
        {
            if (!(Outstandings[i] >= 0))
            {
                throw new ArgumentException(
                    $"negative outstanding for {DataFormattersFunctions.Ordinal(i)} bond, maturity "
                    + $"{Btps[i].MaturityDate()}",
                    nameof(outstandings));
            }
        }

        double outstanding = 0.0;
        for (int i = 0; i < k; ++i)
        {
            outstanding += Outstandings[i];
        }

        Outstanding = outstanding;
        ImmutableArray<double>.Builder weights = ImmutableArray.CreateBuilder<double>(k);
        for (int i = 0; i < k; ++i)
        {
            weights.Add(Outstandings[i] / outstanding);
        }

        Weights = weights.MoveToImmutable();
    }

    /// <summary>Gets the number of bonds.</summary>
    public int Size => Btps.Length;

    /// <summary>Gets the bonds.</summary>
    public ImmutableArray<BTP> Btps { get; }

    /// <summary>Gets the clean price of each bond, per 100 of notional: QuantLib's <c>cleanPriceQuotes</c>.</summary>
    public ImmutableArray<double> CleanPrices { get; }

    /// <summary>Gets the outstanding amount of each bond.</summary>
    public ImmutableArray<double> Outstandings { get; }

    /// <summary>Gets each bond's weight, its outstanding over the basket's.</summary>
    public ImmutableArray<double> Weights { get; }

    /// <summary>Gets the basket's outstanding amount, the sum of the bonds'.</summary>
    public double Outstanding { get; }
}
