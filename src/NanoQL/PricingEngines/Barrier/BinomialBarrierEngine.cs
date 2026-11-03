// SPDX-FileCopyrightText: Copyright (C) 2014 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/binomialbarrierengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The binomial engine for barrier options: a discretized barrier asset rolled back on a Black-Scholes lattice over a
/// binomial tree, built on the process's curves and volatility flattened at the option's maturity. For
/// Cox-Ross-Rubinstein trees the number of steps is raised to Boyle and Lau's optimum ("Bumping up against the barrier
/// with the binomial method", Journal of Derivatives, 1/1994).
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's template on the tree and the discretized asset: <see cref="Calculate"/> takes both as type arguments, the
/// tree built through <see cref="IBinomialTree{TSelf}.Create"/> and the asset through
/// <see cref="IDiscretizedBarrierAsset{TSelf}.Create"/>, <see cref="DiscretizedBarrierOption"/> or
/// <see cref="DiscretizedDermanKaniBarrierOption"/> or a user's own. The delta and gamma come from the lattice's first
/// two steps, the gamma and delta <see langword="null"/> where the nodes don't separate, and the theta from the second
/// step's middle node, as QuantLib's.
/// </para>
/// <para>
/// Documented divergences. The tree runs on the risk-free curve's day counter from the earliest reference date of the
/// structures, each structure on another day counter or reference date flattened on it from its discount or variance
/// at the maturity date, as <see cref="BinomialVanillaEngine"/>'s tree, where QuantLib flattens each curve from the
/// risk-free curve's date on its own day counter (<c>Divergences/BinomialBarrierMismatchedTimesTests.cs</c>); where
/// every structure shares the risk-free curve's day counter and reference date, the tree is QuantLib's. The time grid
/// and the lattice take their step count from the tree, its columns less one, where QuantLib's take the count given;
/// Boyle and Lau's optimum reads the flattened volatility.
/// </para>
/// </remarks>
public static class BinomialBarrierEngine
{
    /// <summary>Prices a barrier option on a binomial tree.</summary>
    /// <typeparam name="TTree">The binomial tree.</typeparam>
    /// <typeparam name="TAsset">The discretized barrier asset.</typeparam>
    /// <param name="option">The option, with a striked payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, positive.</param>
    /// <param name="maxTimeSteps">The most time steps Boyle and Lau's optimum may raise them to: 0, QuantLib's default,
    /// for the heuristic max(1000, 5 × <paramref name="timeSteps"/>); equal to <paramref name="timeSteps"/> to disable
    /// it. Ignored but for Cox-Ross-Rubinstein trees.</param>
    /// <returns>The value, delta, gamma and theta; the delta and the gamma <see langword="null"/> where the tree's
    /// first nodes don't separate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="timeSteps"/> isn't positive, or <paramref name="maxTimeSteps"/> is neither 0 nor at least
    /// <paramref name="timeSteps"/>.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't striked or its strike isn't positive, the underlying isn't positive or lies beyond the barrier
    /// ("barrier touched"), or the tree can't be built.
    /// </exception>
    public static OneAssetOptionResults Calculate<TTree, TAsset>(
        BarrierOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int timeSteps,
        int maxTimeSteps = 0)
        where TTree : Tree, IBinomialTree<TTree>
        where TAsset : DiscretizedAsset, IDiscretizedBarrierAsset<TAsset>
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (!(timeSteps > 0))
        {
            throw new ArgumentException(
                $"timeSteps must be positive, {timeSteps.ToString(CultureInfo.InvariantCulture)} not allowed",
                nameof(timeSteps));
        }

        if (!(maxTimeSteps == 0 || maxTimeSteps >= timeSteps))
        {
            throw new ArgumentException(
                "maxTimeSteps must be zero or greater than or equal to timeSteps, "
                + $"{maxTimeSteps.ToString(CultureInfo.InvariantCulture)} not allowed",
                nameof(maxTimeSteps));
        }

        if (maxTimeSteps == 0)
        {
            maxTimeSteps = Math.Max(1000, timeSteps * 5);
        }

        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body<TTree, TAsset>(option, process, timeSteps, maxTimeSteps));
    }

    private static OneAssetOptionResults Body<TTree, TAsset>(
        BarrierOption option, GeneralizedBlackScholesProcess process, int timeSteps, int maxTimeSteps)
        where TTree : Tree, IBinomialTree<TTree>
        where TAsset : DiscretizedAsset, IDiscretizedBarrierAsset<TAsset>
    {
        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        if (!(payoff.Strike > 0.0))
        {
            throw new NanoQLException("strike must be positive");
        }

        double s0 = process.StateVariable;
        if (!(s0 > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        if (option.Triggered(s0))
        {
            throw new NanoQLException("barrier touched");
        }

        DayCounter rfdc = process.RiskFreeRate.DayCounter;
        DayCounter divdc = process.DividendYield.DayCounter;
        DayCounter voldc = process.BlackVolatility.DayCounter;
        NanoQL.Time.Calendar? volcal = process.BlackVolatility.Calendar;

        // the tree runs on the risk-free curve's day counter from the earliest reference date of the structures it
        // reads, a structure on another day counter or reference date flattened on it, from its discount or variance at
        // the maturity date, none accrued before its own date, as BinomialVanillaEngine's tree; QuantLib's reads where
        // the structure shares the tree's times
        Date maturityDate = option.Exercise.LastDate();
        Date referenceDate = process.RiskFreeRate.ReferenceDate;
        Date origin = process.EarliestReferenceDate();
        double maturity = rfdc.YearFraction(origin, maturityDate);
        bool rateFlattened = origin != referenceDate;
        bool dividendsFlattened = divdc != rfdc || process.DividendYield.ReferenceDate != origin;
        bool volatilityFlattened = voldc != rfdc || process.BlackVolatility.ReferenceDate != origin;

        double v = volatilityFlattened
            ? Math.Sqrt(process.BlackVolatility.AccruedVariance(maturityDate, s0) / maturity)
            : process.BlackVolatility.BlackVol(maturityDate, s0);
        double r = rateFlattened
            ? Math.Log(1.0 / process.RiskFreeRate.AccruedDiscount(maturityDate)) / maturity
            : process.RiskFreeRate.ZeroRate(maturityDate, rfdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
        double q = dividendsFlattened
            ? Math.Log(1.0 / process.DividendYield.AccruedDiscount(maturityDate)) / maturity
            : process.DividendYield.ZeroRate(maturityDate, divdc, Compounding.Continuous, Frequency.NoFrequency).Rate;

        if (dividendsFlattened)
        {
            divdc = rfdc;
        }

        if (volatilityFlattened)
        {
            voldc = rfdc;
        }

        // binomial trees with constant coefficient
        var flatRiskFree = new FlatForward(origin, r, rfdc);
        var flatDividends = new FlatForward(origin, q, divdc);
        var flatVol = new BlackConstantVol(origin, volcal, v, voldc);

        var bs = new GeneralizedBlackScholesProcess(s0, flatDividends, flatRiskFree, flatVol);

        // Note: for the moment we use an hardcoded optimum timesteps, but in the future this could be a parameter.
        // Boyle & Lau optimum timesteps for the CRR tree
        int optimumSteps = timeSteps;
        double barrier = option.Barrier;
        if (typeof(CoxRossRubinstein).IsAssignableFrom(typeof(TTree))
            && maxTimeSteps > timeSteps && s0 > 0 && barrier > 0)
        {
            double divisor = s0 > barrier
                ? Math.Pow(Math.Log(s0 / barrier), 2)
                : Math.Pow(Math.Log(barrier / s0), 2);
            if (!ComparisonFunctions.Close(divisor, 0))
            {
                for (int i = 1; i < timeSteps; ++i)
                {
                    // QuantLib's Size conversion truncates; its i*i a 64-bit Size, the square exact in double below 9e15
                    double optimum = Math.Floor((double)i * i * v * v * maturity / divisor);
                    if (timeSteps < optimum)
                    {
                        optimumSteps = optimum > int.MaxValue ? int.MaxValue : (int)optimum;
                        break; // found first minimum with iterations>=timesteps
                    }
                }
            }

            if (optimumSteps > maxTimeSteps)
            {
                optimumSteps = maxTimeSteps; // too high, limit
            }
        }

        TTree tree = TTree.Create(bs, maturity, optimumSteps, payoff.Strike);

        // The grid and the lattice on the steps the tree was built on
        int steps = tree.Columns - 1;
        var grid = new NanoQL.TimeGrid(maturity, steps);
        var lattice = new BlackScholesLattice<TTree>(tree, r, maturity, steps);

        // the exercise times on the tree's clock
        TAsset discretized = TAsset.Create(option, bs, grid);
        discretized.Initialize(lattice, maturity);

        // Partial derivatives calculated from various points in the
        // binomial tree
        // (see J.C.Hull, "Options, Futures and other derivatives", 6th edition, pp 397/398)

        // Rollback to third-last step, and get underlying prices (s2) &
        // option values (p2) at this point
        discretized.Rollback(grid[2]);
        ReadOnlySpan<double> va2 = discretized.Values;
        if (!(va2.Length == 3))
        {
            throw new NanoQLException("Expect 3 nodes in grid at second step");
        }

        double p2u = va2[2]; // up
        double p2m = va2[1]; // mid
        double p2d = va2[0]; // down (low)
        double s2u = lattice.Underlying(2, 2); // up price
        double s2m = lattice.Underlying(2, 1); // middle price
        double s2d = lattice.Underlying(2, 0); // down (low) price

        // calculate gamma by taking the first derivate of the two deltas
        double? gamma;
        if (s2u > s2m && s2m > s2d)
        {
            double delta2u = (p2u - p2m) / (s2u - s2m);
            double delta2d = (p2m - p2d) / (s2m - s2d);
            gamma = (delta2u - delta2d) / ((s2u - s2d) / 2);
        }
        else
        {
            gamma = null;
        }

        // Rollback to second-last step, and get option values (p1) at
        // this point
        discretized.Rollback(grid[1]);
        ReadOnlySpan<double> va = discretized.Values;
        if (!(va.Length == 2))
        {
            throw new NanoQLException("Expect 2 nodes in grid at first step");
        }

        double p1u = va[1];
        double p1d = va[0];
        double s1u = lattice.Underlying(1, 1); // up (high) price
        double s1d = lattice.Underlying(1, 0); // down (low) price

        double? delta = s1u > s1d ? (p1u - p1d) / (s1u - s1d) : null;

        // Finally, rollback to t=0
        discretized.Rollback(0.0);
        double p0 = discretized.PresentValue();

        // Calculate theta by taking the difference between option value at t=0 and the mid node at second step, divided
        // by their time difference (QuantLib's crude estimate)
        return new OneAssetOptionResults
        {
            NPV = p0,
            Delta = delta,
            Gamma = gamma,
            Theta = (p2m - p0) / grid[2],
        };
    }
}
