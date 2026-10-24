// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/stickyratchet.hpp ql/instruments/stickyratchet.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The payoff of a single or double sticky or ratchet option: on a forward F, the accrual factor times the swaplet
/// a₃F + b₃ less, by the first type t₁, t₁ max(t₁(swaplet − K₁), t₁t₂ max(t₂(swaplet − K₂), 0)), with the effective
/// strikes K₁ = a₁I₁ + b₁ and K₂ = a₂I₂ + b₂ on the initial values I₁ and I₂, a forward rate or a coupon over its
/// accrual factor.
/// </summary>
/// <remarks>
/// An immutable function object (the S-cms plan's CMD17). Each type is +1 (sticky), −1 (ratchet) or 0 (no second
/// option), checked when the payoff is evaluated, as QuantLib checks them. An extension point: the six named payoffs
/// derive from it, as in QuantLib. QuantLib's single <c>StickyRatchetPayoff</c> and its two payoffs, in a block of
/// comments the double payoff superseded, aren't ported.
/// </remarks>
public class DoubleStickyRatchetPayoff : Payoff
{
    private readonly double type1;
    private readonly double type2;
    private readonly double gearing1;
    private readonly double gearing2;
    private readonly double gearing3;
    private readonly double spread1;
    private readonly double spread2;
    private readonly double spread3;
    private readonly double initialValue1;
    private readonly double initialValue2;
    private readonly double accrualFactor;

    /// <summary>Initializes a new instance of the <see cref="DoubleStickyRatchetPayoff"/> class.</summary>
    /// <param name="type1">The first option's type: +1 sticky, −1 ratchet.</param>
    /// <param name="type2">The second option's type: +1, −1, or 0 for none.</param>
    /// <param name="gearing1">The first strike's gearing.</param>
    /// <param name="gearing2">The second strike's gearing.</param>
    /// <param name="gearing3">The swaplet's gearing.</param>
    /// <param name="spread1">The first strike's spread.</param>
    /// <param name="spread2">The second strike's spread.</param>
    /// <param name="spread3">The swaplet's spread.</param>
    /// <param name="initialValue1">The first strike's initial value.</param>
    /// <param name="initialValue2">The second strike's initial value.</param>
    /// <param name="accrualFactor">The accrual factor.</param>
    public DoubleStickyRatchetPayoff(
        double type1,
        double type2,
        double gearing1,
        double gearing2,
        double gearing3,
        double spread1,
        double spread2,
        double spread3,
        double initialValue1,
        double initialValue2,
        double accrualFactor)
    {
        this.type1 = type1;
        this.type2 = type2;
        this.gearing1 = gearing1;
        this.gearing2 = gearing2;
        this.gearing3 = gearing3;
        this.spread1 = spread1;
        this.spread2 = spread2;
        this.spread3 = spread3;
        this.initialValue1 = initialValue1;
        this.initialValue2 = initialValue2;
        this.accrualFactor = accrualFactor;
    }

    /// <inheritdoc/>
    public override string Name => "DoubleStickyRatchetPayoff";

    /// <inheritdoc/>
    public override string Description => Name;

    /// <summary>The payoff on a forward.</summary>
    /// <param name="price">The forward.</param>
    /// <returns>The payoff.</returns>
    /// <exception cref="NanoQLException">A type isn't 0 or ±1, with QuantLib's text.</exception>
    public override double Evaluate(double price)
    {
        if (!(Math.Abs(type1) == 1.0 || type1 == 0.0))
        {
            throw new NanoQLException("unknown/illegal type1 value (only 0.0 and +/-1,0 are allowed))");
        }

        if (!(Math.Abs(type2) == 1.0 || type2 == 0.0))
        {
            throw new NanoQLException("unknown/illegal type2 value(only 0.0 and +/-1,0 are allowed)");
        }

        double forward = price;
        double swaplet = (gearing3 * forward) + spread3;
        double effStrike1 = (gearing1 * initialValue1) + spread1;
        double effStrike2 = (gearing2 * initialValue2) + spread2;
        double effStrike3 = type1 * type2 * Math.Max(type2 * (swaplet - effStrike2), 0.0);
        return accrualFactor * (swaplet - (type1 * Math.Max(type1 * (swaplet - effStrike1), effStrike3)));
    }
}
