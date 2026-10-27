// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/quotes/eurodollarfuturesquote.hpp ql/quotes/eurodollarfuturesquote.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.PricingEngines;

namespace NanoQL.Quotes;

/// <summary>
/// The standard deviation implied by a Eurodollar futures option's price: QuantLib's
/// <c>EurodollarFuturesImpliedStdDevQuote</c>.
/// </summary>
/// <remarks>
/// <para>
/// The futures price and strike are quoted as 100 less a rate; the solve is on the rate, so a call on the futures is a
/// put on the rate. Where the rate strike, 100 less the strike, is above the rate forward, 100 less the futures price,
/// QuantLib solves a call on the rate at the put's price, and otherwise a put on the rate at the call's price; it reads
/// only that price.
/// </para>
/// <para>
/// Immutable: its value is solved once, at construction, from the guess given (§2.2's quote row; the S-core plan's CR1
/// (d)). QuantLib's quote starts each solve after the first from the last one's result; here a new price is a new quote,
/// solved from its caller's guess, a documented divergence. A solve that fails is refused, as QuantLib's is
/// (<c>eurodollarfuturesquote.cpp:58-75</c>). QuantLib's <c>isValid</c> isn't ported.
/// </para>
/// </remarks>
public sealed class EurodollarFuturesImpliedStdDevQuote
{
    /// <summary>Initializes a new instance of the <see cref="EurodollarFuturesImpliedStdDevQuote"/> class.</summary>
    /// <param name="forward">The futures price, 100 less the rate forward.</param>
    /// <param name="callPrice">The call's price, read where the rate strike isn't above the rate forward.</param>
    /// <param name="putPrice">The put's price, read where the rate strike is above the rate forward.</param>
    /// <param name="strike">The strike, 100 less the rate strike.</param>
    /// <param name="guess">The solve's starting point.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxIter">The solver's maximum number of evaluations.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIter"/> is negative.</exception>
    /// <exception cref="ArgumentException">A parameter is out of the formula's domain, or the solver refuses the guess
    /// or the accuracy.</exception>
    /// <exception cref="NanoQLException">The solver finds no root.</exception>
    public EurodollarFuturesImpliedStdDevQuote(
        double forward,
        double callPrice,
        double putPrice,
        double strike,
        double guess = 0.15,
        double accuracy = 1.0e-6,
        int maxIter = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxIter);
        const double Discount = 1.0;
        const double Displacement = 0.0;
        double rateStrike = 100.0 - strike;
        double forwardValue = 100.0 - forward;
        Value = rateStrike > forwardValue
            ? BlackFormulaFunctions.BlackFormulaImpliedStdDev(
                OptionType.Call, rateStrike, forwardValue, putPrice, Discount, Displacement, guess, accuracy, maxIter)
            : BlackFormulaFunctions.BlackFormulaImpliedStdDev(
                OptionType.Put, rateStrike, forwardValue, callPrice, Discount, Displacement, guess, accuracy, maxIter);
    }

    /// <summary>Gets the value: the implied standard deviation of the rate.</summary>
    public double Value { get; }
}
