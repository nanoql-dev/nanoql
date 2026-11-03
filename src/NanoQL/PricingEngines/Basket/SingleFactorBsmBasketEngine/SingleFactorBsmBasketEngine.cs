// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/singlefactorbsmbasketengine.hpp ql/pricingengines/basket/singlefactorbsmbasketengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Numerics.ComparisonFunctions;
using static NanoQL.NumericLimits;

namespace NanoQL.PricingEngines;

/// <summary>
/// The engine for European average-basket options whose assets are all driven by one stochastic factor: each asset's
/// deviation is its Black volatility at the spot, signed, times the square root of the time.
/// </summary>
/// <remarks>
/// <para>
/// J. Choi, "Sum of all Black-Scholes-Merton Models: An efficient Pricing Method for Spread, Basket and Asian
/// Options", https://arxiv.org/pdf/1805.03172. The price is closed form at the root of a sum of exponentials, found by
/// Brent's solver, and the root's d is returned among the additional results as "d", as QuantLib's.
/// </para>
/// <para>
/// The processes must discount alike: QuantLib's check that every risk-free discount is close enough to the first's
/// ("interest rates need to be the same for all underlyings"). The structures are read by date, accruing nothing
/// before their dates, where QuantLib's reads throw for an option expiring before a later structure's date
/// (<c>Divergences/BasketExpiryBeforeReferenceDateTests.cs</c>).
/// </para>
/// </remarks>
public static class SingleFactorBsmBasketEngine
{
    /// <summary>Prices a European average-basket option.</summary>
    /// <param name="option">The basket option, on an average-basket payoff of a plain vanilla.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' processes, one per weight.</param>
    /// <param name="xTol">The root solver's accuracy.</param>
    /// <returns>The value, and the root's d among the additional results where a deviation isn't 0.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't an average basket of a plain vanilla, the weights aren't one per process, the exercise isn't
    /// European, the processes discount differently, or the root can't be found.
    /// </exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        ImmutableArray<GeneralizedBlackScholesProcess> processes,
        double xTol = 1e4 * QL_EPSILON)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        if (processes.IsDefault)
        {
            throw new ArgumentNullException(nameof(processes));
        }

        foreach (GeneralizedBlackScholesProcess p in processes)
        {
            ArgumentNullException.ThrowIfNull(p, nameof(processes));
        }

        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(option, processes, xTol));
    }

    // QuantLib's calculate().
    private static MultiAssetOptionResults Body(
        BasketOption option, ImmutableArray<GeneralizedBlackScholesProcess> processes, double xTol)
    {
        if (option.Payoff is not AverageBasketPayoff avgPayoff)
        {
            throw new NanoQLException("average basket payoff expected");
        }

        if (avgPayoff.BasePayoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain vanilla payoff given");
        }

        ImmutableArray<double> weights = avgPayoff.Weights;
        if (!(processes.Length == weights.Length))
        {
            throw new NanoQLException("wrong number of weights arguments in payoff");
        }

        if (option.Exercise is not EuropeanExercise exercise)
        {
            throw new NanoQLException("not an European exercise");
        }

        Date maturityDate = exercise.LastDate();

        var pExtractor = new VectorBsmProcessExtractor(processes);
        double[] s = pExtractor.GetSpot();
        double[] dq = pExtractor.GetDividendYieldDf(maturityDate);
        double dr0 = pExtractor.GetInterestRateDf(maturityDate);
        double[] stdDev = pExtractor.GetBlackStdDev(maturityDate);

        (double value, double d, bool intrinsic) = Price(payoff, weights.AsSpan(), s, dq, dr0, stdDev, xTol);
        var results = new MultiAssetOptionResults { NPV = value };
        return intrinsic
            ? results
            : results with { AdditionalResults = results.AdditionalResults.Add("d", d) };
    }

    // The engine's formula from the extractor's values: QuantLib's arithmetic from fwdBasket on, the price and the
    // root's d. Where every deviation is close to 0 the price is the intrinsic value and d its limit, +∞ above the
    // strike, −∞ below and 0 at it, which QuantLib doesn't set (Intrinsic). Choi's engine prices each of its nodes
    // through it, on the node's forwards and signed deviations, and reads d at every node.
    internal static (double Value, double D, bool Intrinsic) Price(
        PlainVanillaPayoff payoff,
        ReadOnlySpan<double> weights,
        ReadOnlySpan<double> s,
        ReadOnlySpan<double> dq,
        double dr0,
        ReadOnlySpan<double> stdDev,
        double xTol)
    {
        int n = s.Length;
        double strike = payoff.Strike;

        double[] v = new double[n];
        double[] fwdBasket = new double[n];
        bool allZero = true;
        for (int i = 0; i < n; ++i)
        {
            v[i] = stdDev[i] * stdDev[i];
            fwdBasket[i] = weights[i] * s[i] * dq[i] / dr0;
            allZero &= CloseEnough(stdDev[i], 0.0);
        }

        if (allZero)
        {
            double sum = 0.0;
            foreach (double f in fwdBasket)
            {
                sum += f;
            }

            double limit = sum > strike ? double.PositiveInfinity : sum < strike ? double.NegativeInfinity : 0.0;
            return (dr0 * payoff.Evaluate(sum), limit, true);
        }

        double[] a = new double[n];
        for (int i = 0; i < n; ++i)
        {
            a[i] = fwdBasket[i] * Math.Exp(-0.5 * v[i]);
        }

        double d = -new SumExponentialsRootSolver(a, stdDev.ToArray(), strike)
            .GetRoot(xTol, SumExponentialsRootSolver.Strategy.Brent).Root;

        var nd = new CumulativeNormalDistribution();
        double cp = payoff.OptionType == OptionType.Call ? 1.0 : -1.0;

        // std::inner_product from -strike*N(cp*d), each term x*N(cp*(d+y))
        double acc = -strike * nd.Evaluate(cp * d);
        for (int i = 0; i < n; ++i)
        {
            acc = acc + (fwdBasket[i] * nd.Evaluate(cp * (d + stdDev[i])));
        }

        return (cp * dr0 * acc, d, false);
    }
}
