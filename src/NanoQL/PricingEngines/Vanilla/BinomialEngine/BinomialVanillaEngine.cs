// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Affine Group Limited
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/binomialengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The binomial pricing engine for vanilla options: the option rolled back on a Black-Scholes lattice over a binomial
/// tree, built on the process's curves and volatility flattened at the option's maturity.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's template on the tree (D12): <see cref="Calculate{TTree}"/> takes the tree as its type argument, which the
/// engine builds through <see cref="IBinomialTree{TSelf}.Create"/>; the seven named engines, as QuantLib-Python names
/// them, fix it. The delta and gamma come from the lattice's first two steps, the theta from the Black-Scholes
/// relation, as QuantLib's do.
/// </para>
/// <para>
/// The time grid and the lattice take their step count from the tree, its columns less one, where QuantLib's take the
/// count given (<c>binomialengine.hpp:111-116</c>): the Leisen-Reimer and Joshi trees build an even count on the next
/// odd one, so QuantLib rolls back a tree built for n + 1 steps over n, which at 250 steps errs by 4.7e-2 against the
/// analytic engine. Every odd count and every other tree is QuantLib's. A documented divergence (the S-eq plan's SEQR7
/// (x)).
/// </para>
/// <para>
/// QuantLib's tree runs on the risk-free curve's time to maturity, and QuantLib flattens the dividend curve and the
/// volatility on their own day counters and reads them over that time, so where either has another day counter or
/// reference date it integrates the wrong dividend or variance. Here the tree runs on the risk-free curve's day counter
/// from the earliest reference date of the structures, as the finite-difference engine's grid does, and a structure on
/// another day counter or reference date is flattened on it, its rate or volatility the one whose discount or variance
/// at the maturity date is the structure's own; where every structure shares the risk-free curve's day counter and
/// reference date, the tree is QuantLib's (the S-eq plan's rulings of 2026-10-20 and 2026-10-21,
/// <c>Divergences/BinomialMismatchedTimesTests.cs</c>). A structure accrues nothing before its own date, so an option
/// expiring before a later risk-free curve's is priced, where QuantLib's read throws, and the theta reads no rate or
/// volatility of a structure dated on or after the exercise, where QuantLib reads each at the structure's own time 0
/// (<c>Divergences/ExerciseBeforeReferenceDateTests.cs</c>).
/// </para>
/// </remarks>
public static class BinomialVanillaEngine
{
    /// <summary>Prices a vanilla option on a binomial tree.</summary>
    /// <typeparam name="TTree">The binomial tree.</typeparam>
    /// <param name="option">The option, with a plain-vanilla payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, at least 2.</param>
    /// <returns>The value, delta, gamma and theta; the delta, the gamma and the theta are <see langword="null"/> where
    /// the tree's first nodes don't separate, as QuantLib's <c>Null&lt;Real&gt;()</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> is below 2.</exception>
    /// <exception cref="NanoQLException">
    /// The option's payoff isn't plain vanilla, the underlying isn't positive, or the tree can't be built.
    /// </exception>
    public static OneAssetOptionResults Calculate<TTree>(
        VanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, int timeSteps)
        where TTree : Tree, IBinomialTree<TTree>
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (!(timeSteps >= 2))
        {
            throw new ArgumentException($"at least 2 time steps required, {timeSteps} provided", nameof(timeSteps));
        }

        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body<TTree>(option, process, timeSteps));
    }

    internal static OneAssetOptionResults Body<TTree>(VanillaOption option, GeneralizedBlackScholesProcess process, int timeSteps)
        where TTree : Tree, IBinomialTree<TTree>
    {
        DayCounter rfdc = process.RiskFreeRate.DayCounter;
        DayCounter divdc = process.DividendYield.DayCounter;
        DayCounter voldc = process.BlackVolatility.DayCounter;
        Calendar? volcal = process.BlackVolatility.Calendar;

        double s0 = process.StateVariable;
        if (!(s0 > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        // the tree runs on the risk-free curve's day counter from the earliest reference date of the structures it
        // reads, the risk-free curve's where none is dated before it: a structure on another day counter or reference
        // date is flattened on it, from its discount or variance at the maturity date, each counted from its own
        // reference date and none accrued before it (the S-eq plan's rulings of 2026-10-20 and 2026-10-21); QuantLib's
        // reads where the structure shares the tree's times
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

        // QuantLib checks the payoff after building its flat structures, which can't fail
        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

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

        TTree tree = TTree.Create(bs, maturity, timeSteps, payoff.Strike);

        // SEQR7 (x): the grid and the lattice on the steps the tree was built on
        int steps = tree.Columns - 1;
        var grid = new TimeGrid(maturity, steps);
        var lattice = new BlackScholesLattice<TTree>(tree, r, maturity, steps);

        // the exercise times on the tree's clock, the process's where the tree runs from the risk-free curve's date
        var discretized = new DiscretizedVanillaOption(payoff, option.Exercise, bs, grid);

        discretized.Initialize(lattice, maturity);

        // Partial derivatives calculated from various points in the binomial tree
        // (see J.C.Hull, "Options, Futures and other derivatives", 6th edition, pp 397/398)

        // Rollback to third-last step, and get underlying prices (s2) & option values (p2) at this point
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

        // Rollback to second-last step, and get option values (p1) at this point
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

        return new OneAssetOptionResults
        {
            NPV = p0,
            Delta = delta,
            Gamma = gamma,
            Theta = delta is double d && gamma is double g ? GreeksFunctions.BlackScholesTheta(process, p0, d, g, maturityDate) : null,
        };
    }
}
