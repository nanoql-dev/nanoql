// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The two-asset divergence tests' market: flat curves and volatilities from one date, two-asset correlation matrices,
/// spread and basket options expiring in a year.
/// </summary>
internal static class TwoAssetMarket
{
    internal static readonly Date Today = new(15, Month.October, 2026);
    internal static readonly DayCounter A365 = new Actual365Fixed();
    internal static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    internal static GeneralizedBlackScholesProcess Process(Real s, Rate q, Rate r, Volatility v) =>
        new BlackScholesMertonProcess(s, Flat(q), Flat(r), Vol(v));

    internal static GeneralizedBlackScholesProcess Process(Real s, Rate q, YieldTermStructure r, Volatility v) =>
        new BlackScholesMertonProcess(s, Flat(q), r, Vol(v));

    internal static YieldTermStructure Flat(Rate r) => new FlatForward(Today, r, A365);

    internal static BlackVolTermStructure Vol(Volatility v) => new BlackConstantVol(Today, new NullCalendar(), v, A365);

    internal static Exercise European() => new EuropeanExercise(Today + 365);

    internal static BasketOption Spread(OptionType type, Real strike) =>
        new(new SpreadBasketPayoff(new PlainVanillaPayoff(type, strike)), European());

    internal static BasketOption Average(OptionType type, Real strike, params Real[] weights) =>
        new(new AverageBasketPayoff(new PlainVanillaPayoff(type, strike), [.. weights]), European());

    internal static Matrix Rho(Real rho, int n = 2)
    {
        var m = new Matrix(n, n, rho);
        for (int i = 0; i < n; ++i)
        {
            m[i, i] = 1.0;
        }

        return m;
    }

    internal static ImmutableArray<GeneralizedBlackScholesProcess> Both(
        GeneralizedBlackScholesProcess p1, GeneralizedBlackScholesProcess p2) => [p1, p2];
}
