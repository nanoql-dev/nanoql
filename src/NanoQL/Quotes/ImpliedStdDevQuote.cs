// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/quotes/impliedstddevquote.hpp ql/quotes/impliedstddevquote.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.PricingEngines;

namespace NanoQL.Quotes;

/// <summary>
/// The standard deviation Black's formula implies for an option price: QuantLib's <c>ImpliedStdDevQuote</c>.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: its value is solved once, at construction, from the guess given (§2.2's quote row; the S-core plan's CR1
/// (d)), at a discount of 1 and no displacement. QuantLib's quote starts each solve after the first from the last one's
/// result; here a new forward or price is a new quote, solved from its caller's guess, which may be the last quote's
/// <see cref="Value"/>. A documented divergence: a second solve differs from QuantLib's within the accuracy.
/// </para>
/// <para>
/// A solve that fails gives 0, by QuantLib's design (its <c>catch (Error&amp;)</c>, <c>impliedstddevquote.cpp:54-63</c>):
/// a price out of the formula's range, a negative price, or a solver that finds no root within its evaluations.
/// QuantLib's <c>isValid</c> isn't ported: a value is always valid.
/// </para>
/// </remarks>
public sealed class ImpliedStdDevQuote
{
    /// <summary>Initializes a new instance of the <see cref="ImpliedStdDevQuote"/> class.</summary>
    /// <param name="optionType">The option's type.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="price">The option's price.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="guess">The solve's starting point.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxIter">The solver's maximum number of evaluations.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its type, or
    /// <paramref name="maxIter"/> is negative.</exception>
    public ImpliedStdDevQuote(
        OptionType optionType,
        double forward,
        double price,
        double strike,
        double guess,
        double accuracy = 1.0e-6,
        int maxIter = 100)
    {
        EnumArguments.Check(optionType, nameof(optionType));
        ArgumentOutOfRangeException.ThrowIfNegative(maxIter);
        const double Discount = 1.0;
        const double Displacement = 0.0;
        try
        {
            Value = BlackFormulaFunctions.BlackFormulaImpliedStdDev(
                optionType, strike, forward, price, Discount, Displacement, guess, accuracy, maxIter);
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            Value = 0.0;
        }
    }

    /// <summary>Gets the value: the implied standard deviation, or 0 where the solve fails.</summary>
    public double Value { get; }
}
