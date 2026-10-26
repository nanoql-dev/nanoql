// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/fdndimblackscholesvanillaengine.hpp ql/pricingengines/basket/fdndimblackscholesvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Numerics.GetCovarianceFunctions;
using static NanoQL.NumericLimits;

namespace NanoQL.PricingEngines;

/// <summary>
/// The n-dimensional finite-difference Black-Scholes engine for basket options on up to four assets: the problem in
/// the principal components of the assets' log prices, rolled back on <see cref="FdmWienerOp"/>.
/// </summary>
/// <remarks>
/// <para>
/// Its times run on the clock spanning every process's structures, each structure read at the instants those times
/// denote and accruing nothing before its own date, where QuantLib runs on the first process's risk-free times
/// (<c>Divergences/FdNdimMismatchedTimesTests.cs</c>); a market on one clock keeps QuantLib's arithmetic. The
/// variances and the European discount, read by date, accrue nothing before their structures' dates, where QuantLib's
/// reads throw for an option expiring before a later structure's date
/// (<c>Divergences/BasketExpiryBeforeReferenceDateTests.cs</c>).
/// </para>
/// <para>
/// Each asset's forward is read on its own process's curves and the price discounted on the first process's
/// risk-free curve, in both branches, as QuantLib's spread and Monte Carlo basket engines read two curves: QuantLib's
/// American branch builds every forward on the first process's curve, and its European branch refuses risk-free
/// curves that differ (<c>Divergences/FdNdimTwoRiskFreeCurvesTests.cs</c>).
/// </para>
/// <para>
/// A principal direction whose eigenvalue is below the largest one's times machine epsilon (a singular or
/// near-singular covariance: a correlation of 1, a zero volatility, a rounding negative eigenvalue) is meshed at that
/// floor, the operator keeping the eigenvalue, where QuantLib's mesh collapses to a point, which its spline refuses, or
/// is NaN (<c>Divergences/FdNdimSingularCovarianceTests.cs</c>); where every volatility is (nearly) 0, so is the
/// largest eigenvalue, and the floor is QL_EPSILON², where QuantLib's mesh fails
/// (<c>Divergences/BasketZeroVolatilitiesTests.cs</c>).
/// </para>
/// <para>
/// The correctness of the returned value is tested by reproducing results available in literature and comparison with
/// the PyFENG python package.
/// </para>
/// </remarks>
public static class FdndimBlackScholesVanillaEngine
{
    /// <summary>QuantLib's default build's largest dimension, <c>PDE_MAX_SUPPORTED_DIM</c>.</summary>
    public const int MaxSupportedDimension = 4;

    /// <summary>Prices a basket option on one grid size for every direction.</summary>
    /// <param name="option">The basket option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' processes.</param>
    /// <param name="rho">The assets' correlation matrix.</param>
    /// <param name="xGrid">The grid size of the largest principal direction, the others scaled by their
    /// eigenvalues.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="dampingSteps">The number of damping steps.</param>
    /// <param name="schemeDesc">The scheme; Douglas's by default.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// No process is given, the correlation's size isn't the processes', or there are more than four processes.
    /// </exception>
    /// <exception cref="NanoQLException">A structure can't be read, or the rollback fails.</exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        ImmutableArray<GeneralizedBlackScholesProcess> processes,
        Matrix rho,
        int xGrid,
        int tGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null) =>
        Calculate(option, context, processes, rho, [xGrid], tGrid, dampingSteps, schemeDesc);

    /// <summary>Prices a basket option on a grid size per direction, or one for all.</summary>
    /// <param name="option">The basket option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' processes.</param>
    /// <param name="rho">The assets' correlation matrix.</param>
    /// <param name="xGrids">
    /// The grid sizes of the principal directions, one per process; or one, the largest direction's, the others scaled
    /// by their eigenvalues.
    /// </param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="dampingSteps">The number of damping steps.</param>
    /// <param name="schemeDesc">The scheme; Douglas's by default.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// No process is given, the correlation's size isn't the processes', the grid sizes are neither one nor one per
    /// process, a grid size is below four, or there are more than four processes.
    /// </exception>
    /// <exception cref="NanoQLException">A structure can't be read, or the rollback fails.</exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        ImmutableArray<GeneralizedBlackScholesProcess> processes,
        Matrix rho,
        ImmutableArray<int> xGrids,
        int tGrid = 50,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rho);
        if (!(!processes.IsDefaultOrEmpty))
        {
            throw new ArgumentException("no Black-Scholes process is given.", nameof(processes));
        }

        foreach (GeneralizedBlackScholesProcess p in processes)
        {
            ArgumentNullException.ThrowIfNull(p, nameof(processes));
        }

        if (!(rho.Rows == rho.Columns && rho.Rows == processes.Length))
        {
            throw new ArgumentException("correlation matrix has the wrong size.", nameof(rho));
        }

        if (!(!xGrids.IsDefault && (xGrids.Length == 1 || xGrids.Length == processes.Length)))
        {
            throw new ArgumentException("wrong number of xGrids is given.", nameof(xGrids));
        }

        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(
            option, context, processes, rho, xGrids, tGrid, dampingSteps, schemeDesc ?? FdmSchemeDesc.Douglas()));
    }

    // QuantLib's calculate().
    private static MultiAssetOptionResults Body(
        BasketOption option,
        EvaluationContext context,
        ImmutableArray<GeneralizedBlackScholesProcess> processes,
        Matrix rho,
        ImmutableArray<int> xGrids,
        int tGrid,
        int dampingSteps,
        FdmSchemeDesc schemeDesc)
    {
        int n = processes.Length;
        if (!(n <= MaxSupportedDimension))
        {
            throw new ArgumentException(
                $"This engine does not support {n} underlyings. Max number of underlyings is {MaxSupportedDimension}.",
                nameof(processes));
        }

        Date maturityDate = option.Exercise.LastDate();

        // the times on the clock across every process, where QuantLib's are the first process's
        TermStructure clock = FdmBlackScholesOp.ClockAcross(processes);
        double maturity = clock.TimeFromReference(maturityDate);
        double sqrtT = Math.Sqrt(maturity);

        var pExtractor = new VectorBsmProcessExtractor(processes);
        double[] s = pExtractor.GetSpot();
        _ = pExtractor.GetDividendYieldDf(maturityDate);
        double[] stdDev = pExtractor.GetBlackVariance(maturityDate);
        double[] vols = new double[n];
        for (int i = 0; i < n; ++i)
        {
            stdDev[i] = Math.Sqrt(stdDev[i]);
            vols[i] = stdDev[i] / sqrtT;
        }

        var schur = new SymmetricSchurDecomposition(GetCovariance(vols, rho));
        Matrix q = schur.Eigenvectors();
        Vector l = schur.Eigenvalues();

        const double Eps = 1e-4;
        var meshers = new List<Fdm1dMesher>(n);
        var icn = new InverseCumulativeNormal();
        // the mesh's eigenvalues floored at the largest one's times machine epsilon, and at QL_EPSILON² where every
        // volatility is (nearly) 0, where QuantLib's collapse; the operator keeps QuantLib's
        double lFloor = QL_EPSILON * l[0] < QL_EPSILON * QL_EPSILON ? QL_EPSILON * QL_EPSILON : QL_EPSILON * l[0];
        double lMesh0 = l[0] < lFloor ? lFloor : l[0];
        for (int i = 0; i < n; ++i)
        {
            double lMesh = l[i] < lFloor ? lFloor : l[i];
            int xGrid = xGrids.Length > 1
                ? xGrids[i]
                : Math.Max(4, (int)(xGrids[0] * Math.Pow(lMesh / lMesh0, 0.1)));
            if (!(xGrid >= 4))
            {
                throw new ArgumentException("minimum grid size is four", nameof(xGrids));
            }

            double xStepStize = (1.0 - (2 * Eps)) / (xGrid - 1);

            double[] x = new double[xGrid];
            for (int j = 0; j < xGrid; ++j)
            {
                x[j] = 1.3 * Math.Sqrt(lMesh) * sqrtT * icn.Evaluate(Eps + (j * xStepStize));
            }

            meshers.Add(new Predefined1dMesher(x));
        }

        var mesher = new FdmMesherComposite(meshers);

        var payoff = (BasketPayoff)option.Payoff!;

        YieldTermStructure rTS = processes[0].RiskFreeRate;
        var rTSs = new YieldTermStructure[n];
        var qTS = new YieldTermStructure[n];
        for (int i = 0; i < n; ++i)
        {
            rTSs[i] = processes[i].RiskFreeRate;
            qTS[i] = processes[i].DividendYield;
        }

        double[] logS0 = new double[n];
        for (int i = 0; i < n; ++i)
        {
            logS0[i] = Math.Log(s[i]);
        }

        var calculator = new FdmPCABasketInnerValue(payoff, mesher, logS0, vols, qTS, rTSs, q, clock);

        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            [], option.Exercise, mesher, calculator, clock.ReferenceDate, clock.DayCounter, context);

        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, dampingSteps);

        bool isEuropean = option.Exercise is EuropeanExercise;
        var op = new FdmWienerOp(mesher, isEuropean ? null : rTS, l.AsSpan(), clock);

        double value = new FdmNdimSolver(solverDesc, schemeDesc, op).InterpolateAt(new double[n]);

        if (isEuropean)
        {
            // the first process's discount, where QuantLib refuses risk-free curves that differ, accruing nothing
            // before the curve's date
            value *= rTS.AccruedDiscount(maturityDate);
        }

        return new MultiAssetOptionResults { NPV = value };
    }
}

// QuantLib's detail::FdmPCABasketInnerValue: the payoff at the assets' prices a point of the principal components
// denotes at a time, each forward on its own process's curves (the S-exo plan's ER5 (xxxi)) read on the engine's
// clock; QuantLib's cache of the discounts by time never fills and isn't ported.
file sealed class FdmPCABasketInnerValue : FdmInnerValueCalculator
{
    private readonly int n;
    private readonly BasketPayoff payoff;
    private readonly FdmMesher mesher;
    private readonly double[] logS0;
    private readonly double[] v;
    private readonly YieldTermStructure[] qTS;
    private readonly YieldTermStructure[] rTS;
    private readonly Matrix q;
    private readonly TermStructure clock;
    private readonly double[] x;
    private readonly double[] prices;

    public FdmPCABasketInnerValue(
        BasketPayoff payoff,
        FdmMesher mesher,
        double[] logS0,
        double[] vols,
        YieldTermStructure[] qTS,
        YieldTermStructure[] rTS,
        Matrix q,
        TermStructure clock)
    {
        n = logS0.Length;
        this.payoff = payoff;
        this.mesher = mesher;
        this.logS0 = logS0;
        v = new double[n];
        for (int i = 0; i < n; ++i)
        {
            v[i] = vols[i] * vols[i];
        }

        this.qTS = qTS;
        this.rTS = rTS;
        this.q = q;
        this.clock = clock;
        x = new double[n];
        prices = new double[n];
    }

    public override double InnerValue(FdmLinearOpIterator iter, double t)
    {
        for (int i = 0; i < n; ++i)
        {
            x[i] = mesher.Location(iter, i);
        }

        // Exp(Q_*x - 0.5*v_*t + logS0_)*qf_/rf_, each asset discounted on its own risk-free curve
        for (int i = 0; i < n; ++i)
        {
            ReadOnlySpan<double> row = q.Row(i);
            double qx = 0.0;
            for (int k = 0; k < n; ++k)
            {
                qx = qx + (x[k] * row[k]);
            }

            double qf = qTS[i].DiscountAt(clock, t);
            double rf = rTS[i].DiscountAt(clock, t);
            prices[i] = Math.Exp(qx - (0.5 * v[i] * t) + logS0[i]) * qf / rf;
        }

        return payoff.Evaluate(prices);
    }

    public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
}
