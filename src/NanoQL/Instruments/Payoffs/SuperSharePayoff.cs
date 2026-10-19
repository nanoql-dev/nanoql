// SPDX-FileCopyrightText: Copyright (C) 2003, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/payoffs.hpp ql/instruments/payoffs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Utilities;

namespace NanoQL.Instruments;

/// <summary>The binary supershare payoff: a cash amount paid between two strikes.</summary>
/// <remarks>
/// Its type is always <see cref="OptionType.Call"/>. Unlike <see cref="SuperFundPayoff"/>, QuantLib doesn't check that
/// its strike is positive.
/// </remarks>
public sealed class SuperSharePayoff : StrikedTypePayoff
{
    /// <summary>Initializes a new instance of the <see cref="SuperSharePayoff"/> class.</summary>
    /// <param name="strike">The lower strike.</param>
    /// <param name="secondStrike">The higher strike.</param>
    /// <param name="cashPayoff">The cash paid between the strikes.</param>
    /// <exception cref="ArgumentException"><paramref name="secondStrike"/> isn't above <paramref name="strike"/>.</exception>
    public SuperSharePayoff(double strike, double secondStrike, double cashPayoff)
        : base(OptionType.Call, strike)
    {
        if (!(secondStrike > strike))
        {
            throw new ArgumentException(
                $"second strike ({Show(secondStrike)}) must be higher than first strike ({Show(strike)})",
                nameof(secondStrike));
        }

        SecondStrike = secondStrike;
        CashPayoff = cashPayoff;
    }

    /// <inheritdoc/>
    public override string Name => "SuperShare";

    /// <summary>
    /// Gets the payoff's description: the striked payoff's, then the second strike and the cash amount ("…, 110 second
    /// strike, 10 amount").
    /// </summary>
    public override string Description =>
        $"{base.Description}, {DataFormattersFunctions.General(SecondStrike)} second strike, "
        + $"{DataFormattersFunctions.General(CashPayoff)} amount";

    /// <summary>Gets the higher strike.</summary>
    public double SecondStrike { get; }

    /// <summary>Gets the cash paid between the strikes.</summary>
    public double CashPayoff { get; }

    /// <summary>
    /// The payoff at the given price: the cash payoff from the strike up to, not including, the second strike; otherwise 0.
    /// </summary>
    /// <param name="price">The underlying's price.</param>
    /// <returns>The payoff.</returns>
    public override double Evaluate(double price) =>
        price >= Strike && price < SecondStrike ? CashPayoff : 0.0;

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
