// SPDX-FileCopyrightText: Copyright (C) 2003, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/payoffs.hpp ql/instruments/payoffs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Instruments;

/// <summary>The binary superfund payoff.</summary>
/// <remarks>
/// <para>
/// Superfund is sometimes also called "supershare", which can lead to ambiguity; QuantLib uses the terms supershare and
/// superfund consistently with the definitions in the help pages of Bloomberg's OVX function.
/// </para>
/// <para>
/// It is equivalent to being (1/lower strike) long an <see cref="AssetOrNothingPayoff"/> call at the lower strike and
/// short one at the higher strike. Its type is always <see cref="OptionType.Call"/>, and its description is the striked
/// payoff's, without the second strike, as QuantLib's is.
/// </para>
/// </remarks>
public sealed class SuperFundPayoff : StrikedTypePayoff
{
    /// <summary>Initializes a new instance of the <see cref="SuperFundPayoff"/> class.</summary>
    /// <param name="strike">The lower strike, positive.</param>
    /// <param name="secondStrike">The higher strike.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="strike"/> isn't positive, or <paramref name="secondStrike"/> isn't above it.
    /// </exception>
    public SuperFundPayoff(double strike, double secondStrike)
        : base(OptionType.Call, strike)
    {
        if (!(strike > 0.0))
        {
            throw new ArgumentException($"strike ({Show(strike)}) must be positive", nameof(strike));
        }

        if (!(secondStrike > strike))
        {
            throw new ArgumentException(
                $"second strike ({Show(secondStrike)}) must be higher than first strike ({Show(strike)})",
                nameof(secondStrike));
        }

        SecondStrike = secondStrike;
    }

    /// <inheritdoc/>
    public override string Name => "SuperFund";

    /// <summary>Gets the higher strike.</summary>
    public double SecondStrike { get; }

    /// <summary>The payoff at the given price: price/strike from the strike up to, not including, the second strike; otherwise 0.</summary>
    /// <param name="price">The underlying's price.</param>
    /// <returns>The payoff.</returns>
    public override double Evaluate(double price) =>
        price >= Strike && price < SecondStrike ? price / Strike : 0.0;

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
