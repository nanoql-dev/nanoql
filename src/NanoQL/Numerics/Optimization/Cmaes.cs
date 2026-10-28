// SPDX-FileCopyrightText: Copyright (C) 2026 Colin Alberts
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/cmaes.hpp ql/math/optimization/cmaes.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Numerics.ArrayFunctions;
using static NanoQL.Numerics.MatrixFunctions;

namespace NanoQL.Numerics;

/// <summary>The Covariance Matrix Adaptation Evolution Strategy (CMA-ES).</summary>
/// <remarks>
/// <para>
/// Derivative-free global optimizer. Standard (μ/μ_w, λ)-CMA-ES with positive recombination weights. Reference: N.
/// Hansen, "The CMA Evolution Strategy: A Tutorial", 2016, arXiv:1604.00772.
/// </para>
/// <para>
/// Immutable: each minimization draws from a new Mersenne Twister built from the seed, so a second minimization
/// repeats the first's draws, where QuantLib's optimizer keeps its generator and continues its stream (the S-core
/// plan's CR2 (c); <c>Divergences/CmaesTests.cs</c>). The samples are ranked by a stable sort of their costs, so equal
/// costs keep QuantLib's order up to a population of 32 (MSVC's insertion sort) and their own beyond, where MSVC's
/// introsort reorders them (CD5). A NaN cost is refused before the ranking, where QuantLib's sort reads it and breaks
/// its ordering (CR7 (xxvi)).
/// </para>
/// </remarks>
public sealed class Cmaes : OptimizationMethod
{
    private readonly long seed;

    /// <summary>Initializes a new instance of the <see cref="Cmaes"/> class.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="seed">The Mersenne Twister's seed, in [1, 4294967295]; QuantLib's 0, the clock, is refused.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public Cmaes(CmaesConfiguration configuration, long seed)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Seeds.Check(seed);
        Configuration = configuration;
        this.seed = seed;
    }

    /// <summary>Gets the configuration.</summary>
    public CmaesConfiguration Configuration { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// The problem has no dimension, the population is below 2, the initial mean or a bound has another size than the
    /// problem, or a cost is NaN.
    /// </exception>
    protected override EndCriteriaType MinimizeCore(ProblemState P, EndCriteria endCriteria)
    {
        var rng = new MersenneTwisterUniformRng(seed);
        EndCriteriaType ecType = EndCriteriaType.None;
        P.Reset();

        int n = P.CurrentValue.Size;
        if (!(n >= 1))
        {
            throw new NanoQLException("CMA-ES needs at least one dimension");
        }

        int lambda = Configuration.PopulationSize != 0
            ? Configuration.PopulationSize
            : (int)(4 + Math.Floor(3.0 * Math.Log(n)));
        if (!(lambda >= 2))
        {
            throw new NanoQLException("CMA-ES population size must be at least 2");
        }

        int mu = lambda / 2;

        var weights = new Vector(mu);
        for (int i = 0; i < mu; ++i)
        {
            weights[i] = Math.Log((lambda + 1.0) / 2.0) - Math.Log(i + 1);
        }

        double sum = 0.0;
        for (int i = 0; i < mu; ++i)
        {
            sum += weights[i];
        }

        weights /= sum;

        double muEff = 1.0 / DotProduct(weights, weights);
        double cSigma = (muEff + 2.0) / (n + muEff + 5.0);
        double dSigma = 1.0 + (2.0 * Max(0.0, Math.Sqrt((muEff - 1.0) / (n + 1.0)) - 1.0)) + cSigma;
        double cc = (4.0 + (muEff / n)) / (n + 4.0 + (2.0 * muEff / n));
        double c1 = 2.0 / (Math.Pow(n + 1.3, 2) + muEff);
        double cMu = Min(1.0 - c1, 2.0 * (muEff - 2.0 + (1.0 / muEff)) / (Math.Pow(n + 2.0, 2) + muEff));
        double chiN = Math.Sqrt(n) * (1.0 - (1.0 / (4.0 * n)) + (1.0 / (21.0 * n * n)));

        double pSigmaGain = Math.Sqrt(cSigma * (2.0 - cSigma) * muEff);
        double pCovGain = Math.Sqrt(cc * (2.0 - cc) * muEff);

        Vector mean = Configuration.InitialMean.IsEmpty
            ? P.CurrentValue.Clone()
            : new Vector(Configuration.InitialMean.AsSpan());
        if (!(mean.Size == n))
        {
            throw new NanoQLException("initial mean size does not match problem dimension");
        }

        double sigma = Configuration.Sigma;

        // C = L L^T, initialised to the identity
        var C = new Matrix(n, n, 0.0);
        for (int i = 0; i < n; ++i)
        {
            C[i, i] = 1.0;
        }

        Matrix L;

        var pSigma = new Vector(n, 0.0);
        var pCov = new Vector(n, 0.0);

        // Box bounds: configuration overrides the constraint
        Vector lo = Configuration.LowerBound.IsEmpty
            ? P.Constraint.LowerBound(P.CurrentValue)
            : new Vector(Configuration.LowerBound.AsSpan());
        Vector up = Configuration.UpperBound.IsEmpty
            ? P.Constraint.UpperBound(P.CurrentValue)
            : new Vector(Configuration.UpperBound.AsSpan());
        if (!(lo.Size == n && up.Size == n))
        {
            throw new NanoQLException("bound size does not match problem dimension");
        }

        Vector bestX = mean.Clone();
        double bestF = NumericLimits.QL_MAX_REAL;

        var icn = default(InverseCumulativeNormal);

        const int maxResample = 10;      // resample attempts before clamping
        const double gamma = 1.0;        // out-of-bounds penalty weight
        const double degenerateScale = 1e-12;
        double pivotFloor = Math.Sqrt(NumericLimits.QL_EPSILON);

        var xSamples = new Vector[lambda];
        var ySamples = new Vector[lambda];
        var fSamples = new Vector(lambda);

        int g = 0;
        int statState = 0;               // consecutive near-stationary generations
        double fOld = NumericLimits.QL_MAX_REAL;

        while (true)
        {
            if (endCriteria.CheckMaxIterations(g))
            {
                ecType = EndCriteriaType.MaxIterations;
                break;
            }

            // Cholesky absorbs a semi-definite C by zeroing the pivot. Every
            // term of the update below is elementwise symmetric.
            L = CholeskyDecompositionFunctions.CholeskyDecomposition(C, true);

            // A pivot at or below the floor means that direction has
            // collapsed. Flooring the diagonal alone is not sufficient as the
            // subsequent column was already divided by the pre-floor pivot.
            // This means L would factor nothing like C. Thus we zero it, leaving the
            // direction contributing only its floored variance.
            for (int i = 0; i < n; ++i)
            {
                if (!(L[i, i] > pivotFloor))
                {
                    L[i, i] = pivotFloor;

                    for (int j = i + 1; j < n; ++j)
                    {
                        L[j, i] = 0.0;
                    }
                }
            }

            // per-sample scratch, reused across offspring
            var z = new Vector(n);
            var y = new Vector(n);
            var x = new Vector(n);
            var xClamped = new Vector(n);

            for (int k = 0; k < lambda; ++k)
            {
                bool isFeasible = false;
                for (int attempt = 0; attempt < maxResample && !isFeasible; ++attempt)
                {
                    for (int i = 0; i < n; ++i)
                    {
                        z[i] = icn.Evaluate(rng.Next().Value);
                    }

                    y = L * z;
                    x = mean + (sigma * y);

                    isFeasible = P.Constraint.Test(x);
                    for (int i = 0; i < n && isFeasible; ++i)
                    {
                        isFeasible = x[i] >= lo[i] && x[i] <= up[i];
                    }
                }

                // unclamped y (not the clamped x) feeds the updates
                ySamples[k] = y.Clone();

                if (isFeasible)
                {
                    xSamples[k] = x.Clone();
                    fSamples[k] = P.Value(x);
                }
                else
                {
                    for (int i = 0; i < n; ++i)
                    {
                        // std::clamp(v, lo, hi): (v < lo) ? lo : (hi < v) ? hi : v
                        xClamped[i] = x[i] < lo[i] ? lo[i] : (up[i] < x[i] ? up[i] : x[i]);
                    }

                    Vector excess = x - xClamped;
                    double penalty = gamma * DotProduct(excess, excess);

                    xSamples[k] = xClamped.Clone();
                    fSamples[k] = P.Value(xClamped) + penalty;
                }
            }

            // a NaN cost breaks the strict weak ordering the ranking needs
            for (int k = 0; k < lambda; ++k)
            {
                if (double.IsNaN(fSamples[k]))
                {
                    throw new NanoQLException("CMA-ES cost function returned NaN");
                }
            }

            // a stable sort: MSVC's insertion sort up to 32 samples
            int[] order = [.. Enumerable.Range(0, lambda).OrderBy(i => fSamples[i], CostComparer.Instance)];

            var yw = new Vector(n, 0.0);
            for (int i = 0; i < mu; ++i)
            {
                yw += weights[i] * ySamples[order[i]];
            }

            mean += sigma * yw;

            if (fSamples[order[0]] < bestF)
            {
                bestF = fSamples[order[0]];
                bestX = xSamples[order[0]].Clone();
            }

            Vector cInvSqrtYw = ForwardSubstitution(L, yw);

            pSigma = ((1.0 - cSigma) * pSigma) + (pSigmaGain * cInvSqrtYw);

            double pSigmaNorm = Norm2(pSigma);

            double hSigmaLHS = pSigmaNorm / Math.Sqrt(1.0 - Math.Pow(1.0 - cSigma, 2.0 * (g + 1)));
            double hSigma = hSigmaLHS < (1.4 + (2.0 / (n + 1.0))) * chiN ? 1.0 : 0.0;

            pCov = ((1.0 - cc) * pCov) + (hSigma * pCovGain * yw);

            double deltaH = (1.0 - hSigma) * cc * (2.0 - cc);

            var rankMu = new Matrix(n, n, 0.0);
            for (int i = 0; i < mu; ++i)
            {
                rankMu += weights[i] * OuterProduct(ySamples[order[i]], ySamples[order[i]]);
            }

            C = ((1.0 - c1 - cMu) * C)
                + (c1 * (OuterProduct(pCov, pCov) + (deltaH * C)))
                + (cMu * rankMu);

            sigma *= Math.Exp((cSigma / dSigma) * ((pSigmaNorm / chiN) - 1.0));

            // largest per-coordinate standard deviation of the search
            // distribution, standing in for the largest eigenvalue's root
            double maxVariance = 0.0;
            for (int i = 0; i < n; ++i)
            {
                maxVariance = Max(maxVariance, C[i, i]);
            }

            if (sigma * Math.Sqrt(maxVariance) < degenerateScale)
            {
                ecType = EndCriteriaType.StationaryFunctionValue;
                break;
            }

            EndCriteriaCheck stationary = endCriteria.CheckStationaryFunctionValue(fOld, bestF, statState);
            statState = stationary.StationaryStateIterations;
            if (stationary.Stop)
            {
                ecType = stationary.Type;
                break;
            }

            fOld = bestF;
            ++g;
        }

        P.CurrentValue = bestX;
        P.FunctionValue = bestF;
        return ecType;
    }

    // solves the lower-triangular system L t = b for t
    private static Vector ForwardSubstitution(Matrix L, Vector b)
    {
        int n = b.Size;
        var t = new Vector(n);

        for (int i = 0; i < n; ++i)
        {
            double dot = 0.0;
            for (int j = 0; j < i; ++j)
            {
                dot += L[i, j] * t[j];
            }

            t[i] = (b[i] - dot) / L[i, i];
        }

        return t;
    }

    // std::max and std::min as C++ defines them, (a < b) ? b : a and (b < a) ? b : a.
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;

    // The samples' order, QuantLib's operator< on their costs.
    private sealed class CostComparer : IComparer<double>
    {
        public static readonly CostComparer Instance = new();

        public int Compare(double a, double b) => a < b ? -1 : (b < a ? 1 : 0);
    }
}
