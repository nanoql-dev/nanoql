// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/choibasketengine.hpp ql/pricingengines/basket/choibasketengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.MathConstants;
using static NanoQL.Numerics.ArrayFunctions;
using static NanoQL.Numerics.CholeskyDecompositionFunctions;
using static NanoQL.Numerics.GetCovarianceFunctions;
using static NanoQL.Numerics.MatrixFunctions;
using static NanoQL.NumericLimits;

namespace NanoQL.PricingEngines;

/// <summary>
/// The engine for European basket and spread options on several assets by Choi's method: the basket's projection on
/// one factor priced in closed form, integrated over the others by Gauss-Hermite quadrature.
/// </summary>
/// <remarks>
/// <para>
/// J. Choi, "Sum of all Black-Scholes-Merton Models: An efficient Pricing Method for Spread, Basket and Asian Options",
/// 2018, https://papers.ssrn.com/sol3/papers.cfm?abstract_id=2913048; the author's Python implementation is at
/// https://github.com/PyFE/PyFENG. With <c>calcFwdDelta</c> (or the control variate) the forward deltas are returned
/// among the additional results as "forwardDelta k", as QuantLib's.
/// </para>
/// <para>
/// Each node is priced by the single-factor engine's formula on the node's forwards and signed deviations, where
/// QuantLib builds quotes, processes and an option priced by that engine; the arithmetic is the same. Where every
/// volatility is 0 a node's value is the intrinsic one and its root's d the limit, ±∞ by the side of the strike the
/// forward basket is on, where QuantLib's node sets no d and its read fails
/// (<c>Divergences/BasketZeroVolatilitiesTests.cs</c>).
/// </para>
/// <para>
/// The structures are read by date, accruing nothing before their dates, where QuantLib's reads throw for an option
/// expiring before a later structure's date (<c>Divergences/BasketExpiryBeforeReferenceDateTests.cs</c>).
/// </para>
/// <para>
/// The processes must discount alike: QuantLib's check that every risk-free discount is close enough to the first's.
/// The correctness of the returned value is tested by reproducing results available in literature.
/// </para>
/// </remarks>
public static class ChoiBasketEngine
{
    /// <summary>Prices a European average or spread basket option.</summary>
    /// <param name="option">The basket option, on an average or spread payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' processes.</param>
    /// <param name="rho">The assets' correlation matrix.</param>
    /// <param name="lambda">The integration's scale, from which each direction's order follows.</param>
    /// <param name="maxNrIntegrationSteps">
    /// The largest number of integration nodes, the scale reduced until the orders fit; <see langword="null"/>
    /// (QuantLib's largest <c>Size</c>) for no limit.
    /// </param>
    /// <param name="calcFwdDelta">Whether to compute the forward deltas.</param>
    /// <param name="controlVariate">Whether to correct the value by the forward deltas' control variate.</param>
    /// <returns>The value, and the forward deltas among the additional results where asked for.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// No process is given, the correlation's size isn't the processes', or lambda isn't positive.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't an average or spread basket, the weights aren't one per process or
    /// there is only one, the processes discount differently, the covariance isn't positive definite, or the orders
    /// can't be fitted.
    /// </exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        ImmutableArray<GeneralizedBlackScholesProcess> processes,
        Matrix rho,
        double lambda = 10.0,
        int? maxNrIntegrationSteps = null,
        bool calcFwdDelta = false,
        bool controlVariate = false)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rho);
        if (processes.IsDefault)
        {
            throw new ArgumentNullException(nameof(processes));
        }

        foreach (GeneralizedBlackScholesProcess p in processes)
        {
            ArgumentNullException.ThrowIfNull(p, nameof(processes));
        }

        int n = processes.Length;
        if (!(n > 0))
        {
            throw new ArgumentException("No Black-Scholes process is given.", nameof(processes));
        }

        if (!(n == rho.Rows && rho.Rows == rho.Columns))
        {
            throw new ArgumentException("process and correlation matrix must have the same size.", nameof(rho));
        }

        if (!(lambda > 0.0))
        {
            throw new ArgumentException("lambda must be positive", nameof(lambda));
        }

        if (maxNrIntegrationSteps is int max)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(max, nameof(maxNrIntegrationSteps));
        }

        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(
            option, processes, rho, lambda, maxNrIntegrationSteps, calcFwdDelta || controlVariate, controlVariate));
    }

    // QuantLib's calculate().
    private static MultiAssetOptionResults Body(
        BasketOption option,
        ImmutableArray<GeneralizedBlackScholesProcess> processes,
        Matrix rho,
        double lambda0,
        int? maxNrIntegrationSteps,
        bool calcFwdDelta,
        bool controlVariate)
    {
        int n = processes.Length;
        if (option.Exercise is not EuropeanExercise exercise)
        {
            throw new NanoQLException("not an European exercise");
        }

        Date maturityDate = exercise.LastDate();

        var pExtractor = new VectorBsmProcessExtractor(processes);
        double[] s = pExtractor.GetSpot();
        double[] dq = pExtractor.GetDividendYieldDf(maturityDate);
        double dr0 = pExtractor.GetInterestRateDf(maturityDate);

        double[] stdDev = pExtractor.GetBlackVariance(maturityDate);
        for (int i = 0; i < n; ++i)
        {
            // std::max(QL_EPSILON*QL_EPSILON, sqrt(variance))
            double x = Math.Sqrt(stdDev[i]);
            stdDev[i] = QL_EPSILON * QL_EPSILON < x ? x : QL_EPSILON * QL_EPSILON;
        }

        var fwd = new Vector(n);
        for (int i = 0; i < n; ++i)
        {
            fwd[i] = s[i] * dq[i] / dr0;
        }

        AverageBasketPayoff avgPayoff = option.Payoff switch
        {
            AverageBasketPayoff average => average,
            SpreadBasketPayoff spread => new AverageBasketPayoff(spread.BasePayoff, [1.0, -1.0]),
            _ => throw new NanoQLException("average or spread basket payoff expected"),
        };

        ImmutableArray<double> weights = avgPayoff.Weights;
        if (!(n == weights.Length && n > 1))
        {
            throw new NanoQLException("wrong number of weights arguments in payoff");
        }

        var weightedFwd = new Vector(n);
        for (int i = 0; i < n; ++i)
        {
            weightedFwd[i] = weights[i] * fwd[i];
        }

        Vector g = weightedFwd / Norm2(weightedFwd);

        Matrix sigma = GetCovariance(stdDev, rho);
        Vector vStar1 = sigma * g;
        vStar1 /= Math.Sqrt(DotProduct(g, vStar1));

        Matrix c = CholeskyDecomposition(sigma);

        double eps = 100 * Math.Sqrt(QL_EPSILON);
        double tol = 100 * Math.Sqrt(QL_EPSILON);

        bool flip = false;
        for (int i = 0; i < n; ++i)
        {
            if (Sign(g[i]) * vStar1[i] < tol * stdDev[i])
            {
                flip = true;
                vStar1[i] = eps * Sign(g[i]) * stdDev[i];
            }
        }

        Vector q1 = new(n);
        if (flip)
        {
            for (int i = 0; i < n; ++i)
            {
                ReadOnlySpan<double> row = c.Row(i);
                double sum = 0.0;
                for (int j = 0; j < i; ++j)
                {
                    sum = sum + (row[j] * q1[j]);
                }

                q1[i] = (vStar1[i] - sum) / c[i, i];
            }

            vStar1 /= Norm2(q1);
        }
        else
        {
            q1 = Transpose(c) * g;
        }

        q1 /= Norm2(q1);

        var e1 = new Vector(n, 0.0);
        e1[0] = 1.0;

        Matrix r = new HouseholderTransformation(new HouseholderReflection(e1).ReflectionVector(q1)).GetMatrix();
        var r2n = new Matrix(n, n - 1);
        for (int i = 0; i < n; ++i)
        {
            for (int j = 1; j < n; ++j)
            {
                r2n[i, j - 1] = r[i, j];
            }
        }

        var svd = new SVD(c * r2n);
        Matrix u = svd.U();
        Vector sv = svd.SingularValues();

        var v = new Matrix(n, n - 1);
        for (int i = 0; i < n - 1; ++i)
        {
            for (int k = 0; k < n; ++k)
            {
                v[k, i] = sv[i] * u[k, i];
            }
        }

        // the orders as QuantLib's Size values, in doubles while the scale is fitted
        double[] orders = new double[n - 1];
        double lambda = lambda0;
        double alpha = 1 / Math.Abs(DotProduct(g, vStar1));

        // QuantLib's Real(maxNrIntegrationSteps_), its default the largest Size
        double maxSteps = maxNrIntegrationSteps is int maxGiven ? maxGiven : 18446744073709551616.0;
        double product;
        do
        {
            double intScale = lambda * alpha;
            for (int i = 0; i < n - 1; ++i)
            {
                orders[i] = SizeOfLRound(1 + (intScale * sv[i]));
            }

            lambda *= 0.9;

            if (!(lambda / lambda0 > 1e-10))
            {
                throw new NanoQLException("can not rescale lambda to fit max integration order");
            }

            product = 1.0;
            foreach (double order in orders)
            {
                product = product * order;
            }
        }
        while (product > maxSteps);

        int[] nIntOrder = new int[n - 1];
        for (int i = 0; i < n - 1; ++i)
        {
            if (!(orders[i] <= int.MaxValue))
            {
                // QuantLib's integration allocates its nodes on the order, past what can be allocated
                throw new NanoQLException(
                    $"integration order {orders[i].ToString(CultureInfo.InvariantCulture)} too large");
            }

            nIntOrder[i] = (int)orders[i];
        }

        // the nodes' discounts, each process's risk-free curve's: QuantLib's BlackProcess dividend and risk-free curves,
        // accruing nothing before the curve's date
        double[] nodeDq = new double[n];
        double[] nodeStdDev = new double[n];
        for (int i = 0; i < n; ++i)
        {
            nodeDq[i] = processes[i].RiskFreeRate.AccruedDiscount(maturityDate);

            // QuantLib's BlackConstantVol at vStar1[i]/√t, read back times √t; no time to expiry from a volatility
            // dated at or after it, whose deviation, accrued from its date, is 0
            BlackVolTermStructure bv = processes[i].BlackVolatility;
            double t = bv.DayCounter.YearFraction(bv.ReferenceDate, maturityDate);
            if (t > 0.0)
            {
                double vol = vStar1[i] / Math.Sqrt(t);
                nodeStdDev[i] = vol * Math.Sqrt(t);
            }
            else
            {
                nodeStdDev[i] = vStar1[i];
            }
        }

        double[] vq = new double[n];
        for (int i = 0; i < n; ++i)
        {
            ReadOnlySpan<double> row = v.Row(i);
            double acc = 0.0;
            foreach (double x in row)
            {
                acc = acc + (x * x);
            }

            vq[i] = 0.5 * acc;
        }

        var ghq = new MultiDimGaussianIntegration(nIntOrder, k => new GaussHermiteIntegration(k));
        double normFactor = Math.Pow(M_PI, -0.5 * nIntOrder.Length);

        var dStore = new List<double>();
        double[] f = new double[n];
        double Bsm1dPricer(Vector z)
        {
            Vector vz = v * z;
            for (int i = 0; i < n; ++i)
            {
                f[i] = Math.Exp((-M_SQRT2 * vz[i]) - vq[i]) * fwd[i];
            }

            if (avgPayoff.BasePayoff is not PlainVanillaPayoff nodePayoff)
            {
                throw new NanoQLException("non-plain vanilla payoff given");
            }

            (double npv, double d, _) = SingleFactorBsmBasketEngine.Price(
                nodePayoff, weights.AsSpan(), f, nodeDq, dr0, nodeStdDev, 1e4 * QL_EPSILON);

            // QuantLib's additionalResults().at("d"); where the node's deviations are all 0, its limit, which QuantLib
            // doesn't set and whose read fails
            dStore.Add(d);
            return Math.Exp(-DotProduct(z, z)) * npv;
        }

        double value = ghq.Evaluate(Bsm1dPricer) * normFactor;
        ImmutableSortedDictionary<string, object> additional = ImmutableSortedDictionary<string, object>.Empty;

        if (calcFwdDelta)
        {
            if (avgPayoff.BasePayoff is not PlainVanillaPayoff payoff)
            {
                throw new NanoQLException("non-plain vanilla payoff given");
            }

            double putIndicator = payoff.OptionType == OptionType.Call ? 0.0 : -1.0;

            var nd = new CumulativeNormalDistribution();

            var fwdDelta = new Vector(n);
            var fHat = new Vector(n);
            for (int k = 0; k < n; ++k)
            {
                int dStoreCounter = 0;
                int kk = k;
                double DeltaPricer(Vector z)
                {
                    double d = dStore[dStoreCounter++];
                    double vzk = RowDot(v, kk, z);
                    double fk = Math.Exp((-M_SQRT2 * vzk) - vq[kk]);
                    return Math.Exp(-DotProduct(z, z)) * fk * nd.Evaluate(d + vStar1[kk]);
                }

                fwdDelta[k] = dr0 * weights[k] * ((ghq.Evaluate(DeltaPricer) * normFactor) + putIndicator);

                string deltaName = "forwardDelta " + k.ToString(CultureInfo.InvariantCulture);
                additional = additional.SetItem(deltaName, fwdDelta[k]);
            }

            if (controlVariate)
            {
                for (int k = 0; k < n; ++k)
                {
                    int kk = k;
                    double FHatPricer(Vector z)
                    {
                        double vzk = RowDot(v, kk, z);
                        double fk = Math.Exp((-M_SQRT2 * vzk) - vq[kk]);
                        return Math.Exp(-DotProduct(z, z)) * fk;
                    }

                    fHat[k] = ghq.Evaluate(FHatPricer) * normFactor;
                }

                double cvSum = 0.0;
                for (int k = 0; k < n; ++k)
                {
                    cvSum = cvSum + (fwdDelta[k] * fwd[k] * (fHat[k] - 1.0));
                }

                value -= cvSum;
            }
        }

        return new MultiAssetOptionResults { NPV = value, AdditionalResults = additional };
    }

    // std::inner_product(v.row_begin(k), v.row_end(k), z.begin(), 0.0)
    private static double RowDot(Matrix v, int k, Vector z)
    {
        ReadOnlySpan<double> row = v.Row(k);
        double sum = 0.0;
        for (int j = 0; j < row.Length; ++j)
        {
            sum = sum + (row[j] * z[j]);
        }

        return sum;
    }

    // boost::math::sign: 0 at a zero, otherwise by the sign bit
    private static int Sign(double z) => z == 0 ? 0 : double.IsNegative(z) ? -1 : 1;

    // Size(std::lround(x)) under MSVC, as a double: to nearest, halves away from zero, into a 32-bit long, whose
    // unspecified result for a NaN or a value past its range is LONG_MIN, which wraps as a Size
    private static double SizeOfLRound(double x)
    {
        double rounded = Math.Round(x, MidpointRounding.AwayFromZero);
        if (double.IsNaN(rounded) || rounded > 2147483647.0 || rounded < -2147483648.0)
        {
            return 18446744071562067968.0;
        }

        return rounded < 0.0 ? 18446744073709551616.0 + rounded : rounded;
    }
}
