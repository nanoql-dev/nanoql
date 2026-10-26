// SPDX-FileCopyrightText: Copyright (C) 2010 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/margrabeoption.hpp ql/instruments/margrabeoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// Margrabe's option on two assets: the right to exchange Q₂ units of the second asset for Q₁ of the first at expiry.
/// </summary>
/// <remarks>
/// Immutable; its payoff is a <see cref="NullPayoff"/>, the quantities giving the exchange. Its engines return a
/// <see cref="MargrabeOptionResults"/>, with each asset's delta and gamma.
/// </remarks>
public sealed class MargrabeOption : MultiAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="MargrabeOption"/> class.</summary>
    /// <param name="q1">The quantity of the first asset received.</param>
    /// <param name="q2">The quantity of the second asset given.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exercise"/> is <see langword="null"/>.</exception>
    public MargrabeOption(int q1, int q2, Exercise exercise)
        : base(new NullPayoff(), exercise)
    {
        Q1 = q1;
        Q2 = q2;
    }

    /// <summary>Gets the quantity of the first asset received.</summary>
    public int Q1 { get; }

    /// <summary>Gets the quantity of the second asset given.</summary>
    public int Q2 { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">A quantity isn't positive.</exception>
    public override void Validate()
    {
        base.Validate();
        if (!(Q1 > 0))
        {
            throw new NanoQLException("quantity of asset 1 must be positive");
        }

        if (!(Q2 > 0))
        {
            throw new NanoQLException("quantity of asset 2 must be positive");
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// An expired option's NPV and Greeks are 0, as <see cref="MultiAssetOption"/>'s; its assets' deltas and gammas
    /// are <see langword="null"/>, where QuantLib's expired option reads them uninitialized
    /// (<c>Divergences/MargrabeExpiredGreeksTests.cs</c>).
    /// </remarks>
    public override MargrabeOptionResults SetupExpired() => new(base.SetupExpired());

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The results aren't a Margrabe option's.</exception>
    public override MargrabeOptionResults FetchResults(InstrumentResults results)
    {
        MultiAssetOptionResults fetched = base.FetchResults(results);
        if (fetched is not MargrabeOptionResults r)
        {
            throw new ArgumentException("wrong result type", nameof(results));
        }

        return r;
    }
}
