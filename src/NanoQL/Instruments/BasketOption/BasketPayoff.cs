// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/basketoption.hpp ql/instruments/basketoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The payoff of a basket option: a one-dimensional payoff applied to a value accumulated from the assets' prices.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib: the minimum, maximum, average and spread payoffs derive from it, and later
/// multi-asset options define their own. The assets' prices come as a <see cref="ReadOnlySpan{T}"/>, which takes an
/// array, a <see cref="NanoQL.Numerics.Vector"/>'s storage or a stack buffer alike, so the path pricers and the
/// finite-difference inner values pass their buffers without a copy, where QuantLib's members take an <c>Array</c>.
/// </para>
/// <para>The payoff is immutable and safe to share across threads.</para>
/// </remarks>
public abstract class BasketPayoff : Payoff
{
    /// <summary>Initializes a new instance of the <see cref="BasketPayoff"/> class.</summary>
    /// <param name="basePayoff">The one-dimensional payoff applied to the accumulated value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="basePayoff"/> is <see langword="null"/>.</exception>
    protected BasketPayoff(Payoff basePayoff)
    {
        ArgumentNullException.ThrowIfNull(basePayoff);
        BasePayoff = basePayoff;
    }

    /// <summary>Gets the one-dimensional payoff applied to the accumulated value.</summary>
    public Payoff BasePayoff { get; }

    /// <summary>Gets the base payoff's name.</summary>
    public override string Name => BasePayoff.Name;

    /// <summary>Gets the base payoff's description.</summary>
    public override string Description => BasePayoff.Description;

    /// <summary>The base payoff at a price.</summary>
    /// <param name="price">The price.</param>
    /// <returns>The base payoff's value.</returns>
    public override double Evaluate(double price) => BasePayoff.Evaluate(price);

    /// <summary>The payoff at the assets' prices: the base payoff at their accumulated value.</summary>
    /// <param name="a">The assets' prices.</param>
    /// <returns>The payoff.</returns>
    /// <exception cref="ArgumentException">The accumulation refuses the prices.</exception>
    public virtual double Evaluate(ReadOnlySpan<double> a) => BasePayoff.Evaluate(Accumulate(a));

    /// <summary>The value the payoff accumulates from the assets' prices.</summary>
    /// <param name="a">The assets' prices.</param>
    /// <returns>The accumulated value.</returns>
    /// <exception cref="ArgumentException">The payoff refuses the prices.</exception>
    public abstract double Accumulate(ReadOnlySpan<double> a);
}
