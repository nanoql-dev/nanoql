// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/exoticoptions/everestoption.hpp ql/experimental/exoticoptions/everestoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;

namespace NanoQL.Experimental;

/// <summary>
/// An Everest option on several assets: at expiry it pays the notional times one plus the worst of the assets'
/// returns since inception plus a guaranteed return.
/// </summary>
/// <remarks>
/// Immutable; its payoff is a <see cref="NullPayoff"/>. Its engines return an <see cref="EverestOptionResults"/>, with
/// the yield the price implies.
/// </remarks>
[Experimental("NQL0001")]
public sealed class EverestOption : MultiAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="EverestOption"/> class.</summary>
    /// <param name="notional">The notional.</param>
    /// <param name="guarantee">The guaranteed return.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exercise"/> is <see langword="null"/>.</exception>
    public EverestOption(double notional, double guarantee, Exercise exercise)
        : base(new NullPayoff(), exercise)
    {
        Notional = notional;
        Guarantee = guarantee;
    }

    /// <summary>Gets the notional.</summary>
    public double Notional { get; }

    /// <summary>Gets the guaranteed return.</summary>
    public double Guarantee { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The notional is 0 ("null notional given").</exception>
    /// <remarks>QuantLib's "no notional given" and "no guarantee given" can't fail: both are values.</remarks>
    public override void Validate()
    {
        base.Validate();
        if (!(Notional != 0.0))
        {
            throw new NanoQLException("null notional given");
        }
    }

    /// <inheritdoc/>
    /// <remarks>An expired option's NPV and Greeks are 0, as <see cref="MultiAssetOption"/>'s; its yield is
    /// <see langword="null"/>, as QuantLib's reset results leave it.</remarks>
    public override EverestOptionResults SetupExpired() => new(base.SetupExpired());

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The results aren't an Everest option's.</exception>
    public override EverestOptionResults FetchResults(InstrumentResults results)
    {
        MultiAssetOptionResults fetched = base.FetchResults(results);
        if (fetched is not EverestOptionResults r)
        {
            throw new ArgumentException("no results returned from pricing engine", nameof(results));
        }

        return r;
    }
}
