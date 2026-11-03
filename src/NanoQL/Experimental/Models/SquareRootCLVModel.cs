// SPDX-FileCopyrightText: Copyright (C) 2016 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/models/squarerootclvmodel.hpp ql/experimental/models/squarerootclvmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The collocating local volatility (CLV) model with a square-root kernel process, its distribution non-central χ²,
/// collocated to a Black-Scholes process's distribution at given maturities: QuantLib's <c>SquareRootCLVModel</c>.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, its mapping function computed once, on the first call of <see cref="G"/>, as QuantLib's lazy object
/// computes it. The collocation points are the Gauss nodes of the kernel's non-central χ² distribution (<see
/// cref="GaussNonCentralChiSquaredPolynomial"/>, exact), their range clamped by the pMin and pMax quantiles and
/// rescaled by QuantLib's map a·x + (xMin − x.front()), a = (xMax − xMin)/(x.back() − x.front()); where only the top
/// bound clamps, as at every date of QuantLib's two cases, the points scale toward zero by a and the top one stops
/// short of the pMax quantile, QuantLib's design, kept. Where that map's lowest point falls below the pMin quantile,
/// breaking the lower bound the method states, it is raised by the shortfall: QuantLib's map with its bits wherever its
/// lowest point meets the bound, the affine map onto [xMin, xMax] where the low bound clamps
/// (<c>Divergences/SquareRootCLVRescaleTests.cs</c>).
/// </para>
/// <para>
/// The non-centrality 4κe^(−κt)x₀/(σ²(1 − e^(−κt))) takes 1 − e^(−κt) as −expm1(−κt) below κt 1e-2, where QuantLib's
/// cancels, and QuantLib's form from it (<c>Divergences/SquareRootCLVNonCentralityTests.cs</c>). The mapping
/// interpolates linearly in time between the maturities; past the last one QuantLib's reads its map's end before its
/// check, and here refuses with QuantLib's text (<c>Conventions/CLVModelTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class SquareRootCLVModel
{
    private readonly double? pMax;
    private readonly double? pMin;
    private readonly GeneralizedBlackScholesProcess bsProcess;
    private readonly SquareRootProcess sqrtProcess;
    private readonly ImmutableArray<Date> maturityDates;
    private readonly int lagrangeOrder;
    private readonly GBSMRNDCalculator rndCalculator;
    private readonly Lazy<MappingFunction> g;

    /// <summary>Initializes a new instance of the <see cref="SquareRootCLVModel"/> class.</summary>
    /// <param name="bsProcess">The Black-Scholes process whose distribution the model collocates.</param>
    /// <param name="sqrtProcess">The kernel process.</param>
    /// <param name="maturityDates">The collocation maturities.</param>
    /// <param name="lagrangeOrder">The number of collocation points, at least 2 and at most 14.</param>
    /// <param name="pMax">The probability bounding the collocation points above, or <see langword="null"/>.</param>
    /// <param name="pMin">The probability bounding them below, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A process or the dates are null.</exception>
    public SquareRootCLVModel(
        GeneralizedBlackScholesProcess bsProcess,
        SquareRootProcess sqrtProcess,
        IEnumerable<Date> maturityDates,
        int lagrangeOrder,
        double? pMax = null,
        double? pMin = null)
    {
        ArgumentNullException.ThrowIfNull(bsProcess);
        ArgumentNullException.ThrowIfNull(sqrtProcess);
        ArgumentNullException.ThrowIfNull(maturityDates);
        this.pMax = pMax;
        this.pMin = pMin;
        this.bsProcess = bsProcess;
        this.sqrtProcess = sqrtProcess;
        this.maturityDates = [.. maturityDates];
        this.lagrangeOrder = lagrangeOrder;
        rndCalculator = new GBSMRNDCalculator(bsProcess);
        g = new Lazy<MappingFunction>(() => new MappingFunction(this));
    }

    /// <summary>The Black-Scholes process's cumulative distribution function at a date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="k">The value.</param>
    /// <returns>The probability of a value at or below <paramref name="k"/>.</returns>
    public double Cdf(Date d, double k) => rndCalculator.Cdf(k, bsProcess.Time(d));

    /// <summary>The Black-Scholes process's inverse cumulative distribution function at a date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="q">The probability.</param>
    /// <returns>The value.</returns>
    public double InvCDF(Date d, double q) => rndCalculator.InvCdf(q, bsProcess.Time(d));

    /// <summary>The kernel process's collocation points at a date, sorted and rescaled into the quantile bounds.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The points.</returns>
    public Vector CollocationPointsX(Date d)
    {
        (double df, double ncp) = NonCentralChiSquaredParams(d);
        double[] x = [.. new GaussianQuadrature(lagrangeOrder, new GaussNonCentralChiSquaredPolynomial(df, ncp)).X()];
        Array.Sort(x);
        NonCentralChiSquaredFunctions.NonCentralChiSquaredDistribution(df, ncp);
        double qMin = pMin is double min ? NonCentralChiSquaredFunctions.Quantile(df, ncp, min) : 0.0;
        double xMin = Max(x[0], qMin);
        double xMax = Min(x[^1], pMax is double max ? NonCentralChiSquaredFunctions.Quantile(df, ncp, max) : double.MaxValue);
        double b = xMin - x[0];
        double a = (xMax - xMin) / (x[^1] - x[0]);

        // QuantLib's map, raised where its lowest point falls below the pMin quantile
        double raise = Max(0.0, qMin - ((a * x[0]) + b));
        var points = new Vector(x.Length);
        for (int i = 0; i < x.Length; ++i)
        {
            points[i] = raise == 0.0 ? (a * x[i]) + b : (a * x[i]) + b + raise;
        }

        return points;
    }

    /// <summary>The underlying's collocation points at a date.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The points.</returns>
    public Vector CollocationPointsY(Date d)
    {
        Vector x = CollocationPointsX(d);
        (double df, double ncp) = NonCentralChiSquaredParams(d);
        NonCentralChiSquaredFunctions.NonCentralChiSquaredDistribution(df, ncp);
        var s = new Vector(x.Size);
        for (int i = 0; i < x.Size; ++i)
        {
            double q = NonCentralChiSquaredFunctions.Cdf(df, ncp, x[i]);
            s[i] = InvCDF(d, q);
        }

        return s;
    }

    /// <summary>The CLV mapping function: the underlying's value at a time for the kernel's value then.</summary>
    /// <returns>The function of the time, from the first maturity to the last, and the kernel's value.</returns>
    /// <exception cref="NanoQLException">A polynomial or quantile can't be computed.</exception>
    public Func<double, double, double> G() => g.Value.Evaluate;

    // std::max(a, b) and std::min(a, b), which keep a where b is NaN.
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;

    // The kernel's degrees of freedom and non-centrality at a date; 1 − e^(−κt) through expm1 below κt 1e-2.
    private (double Df, double Ncp) NonCentralChiSquaredParams(Date d)
    {
        double t = bsProcess.Time(d);
        double kappa = sqrtProcess.A;
        double theta = sqrtProcess.B;
        double sigma = sqrtProcess.Sigma;

        double df = 4 * theta * kappa / (sigma * sigma);
        double kt = kappa * t;
        double oneMinusExp = Math.Abs(kt) < 1e-2 ? -Expm1Functions.StdExpm1(-kt) : 1 - Math.Exp(-kappa * t);
        double ncp = 4 * kappa * Math.Exp(-kappa * t) / (sigma * sigma * oneMinusExp) * sqrtProcess.X0;
        return (df, ncp);
    }

    // QuantLib's MappingFunction: a Lagrange interpolation per maturity time, in increasing time, immutable.
    private sealed class MappingFunction
    {
        private readonly ImmutableArray<double> times;
        private readonly ImmutableArray<LagrangeInterpolation> interpl;

        public MappingFunction(SquareRootCLVModel model)
        {
            Date[] maturityDates = [.. model.maturityDates];
            Array.Sort(maturityDates);
            var map = new SortedDictionary<double, LagrangeInterpolation>();
            foreach (Date maturityDate in maturityDates)
            {
                Vector x = model.CollocationPointsX(maturityDate);
                Vector y = model.CollocationPointsY(maturityDate);
                double maturity = model.bsProcess.Time(maturityDate);

                // std::map::insert keeps the first value at a time
                map.TryAdd(maturity, new LagrangeInterpolation(x.AsSpan(), y.AsSpan()));
            }

            times = [.. map.Keys];
            interpl = [.. map.Values];
        }

        public double Evaluate(double t, double x)
        {
            // std::map::lower_bound: the first time not less than t
            int ge = 0;
            while (ge < times.Length && times[ge] < t)
            {
                ++ge;
            }

            // QuantLib's reads the time at ge before it checks ge against the end
            if (ge == times.Length)
            {
                throw new NanoQLException("extrapolation to large or small t is not allowed");
            }

            if (ComparisonFunctions.CloseEnough(times[ge], t))
            {
                return interpl[ge].Evaluate(x, true);
            }

            if (!(ge != 0))
            {
                throw new NanoQLException("extrapolation to large or small t is not allowed");
            }

            double t1 = times[ge];
            double y1 = interpl[ge].Evaluate(x, true);
            double t0 = times[ge - 1];
            double y0 = interpl[ge - 1].Evaluate(x, true);
            return y0 + ((y1 - y0) / (t1 - t0) * (t - t0));
        }
    }
}
