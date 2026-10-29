// SPDX-FileCopyrightText: Copyright (C) 2016 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/models/normalclvmodel.hpp ql/experimental/models/normalclvmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The collocating local volatility (CLV) model with a normally distributed kernel process, an Ornstein-Uhlenbeck
/// process, collocated to a Black-Scholes process's distribution at given maturities: QuantLib's <c>NormalCLVModel</c>.
/// </summary>
/// <remarks>
/// <para>
/// References: A. Grzelak, N. Thijs, 2016, "The CLV Framework — A Fresh Look at Efficient Pricing with Smile",
/// http://papers.ssrn.com/sol3/papers.cfm?abstract_id=2747541.
/// </para>
/// <para>
/// Immutable, its mapping function computed once, on the first call of <see cref="G"/>, as QuantLib's lazy object
/// computes it, so that a model read only through <see cref="Cdf"/> and <see cref="InvCDF"/> takes no maturities and
/// no kernel process, as QuantLib's own cases build one. The collocation points are √2 times the Gauss-Hermite nodes of the Lagrange order, scaled by σ
/// so that the outer one meets the pMax quantile, or else the pMin one, or 1. The mapping interpolates each collocation
/// value linearly in time, extrapolated past the maturities, then by Lagrange interpolation at the kernel's standardized
/// value; at t = 0 the kernel's deviation is 0 and QuantLib's standardized value σ(x − E)/0 is NaN at x₀ and infinite
/// elsewhere, so the mapping refuses a time that isn't positive (the S-xrt plan's XRR5 (xli);
/// <c>Conventions/CLVModelTests.cs</c>). QuantLib's mapping shares one buffer for its interpolated values among its
/// calls; here each call has its own.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class NormalCLVModel
{
    private readonly ImmutableArray<double> x;
    private readonly double sigma;
    private readonly GeneralizedBlackScholesProcess bsProcess;
    private readonly OrnsteinUhlenbeckProcess? ouProcess;
    private readonly ImmutableArray<Date> maturityDates;
    private readonly GBSMRNDCalculator rndCalculator;
    private readonly ImmutableArray<double> maturityTimes;
    private readonly Lazy<MappingFunction> g;

    /// <summary>Initializes a new instance of the <see cref="NormalCLVModel"/> class.</summary>
    /// <param name="bsProcess">The Black-Scholes process whose distribution the model collocates.</param>
    /// <param name="ouProcess">The kernel process, or <see langword="null"/> for a model read only through
    /// <see cref="Cdf"/> and <see cref="InvCDF"/>.</param>
    /// <param name="maturityDates">The collocation maturities, in increasing order.</param>
    /// <param name="lagrangeOrder">The number of collocation points, at least 2.</param>
    /// <param name="pMax">The probability the outer collocation point is set at, or <see langword="null"/>.</param>
    /// <param name="pMin">The probability the inner collocation point is set at when <paramref name="pMax"/> is
    /// <see langword="null"/>, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">The Black-Scholes process or the dates are null.</exception>
    /// <exception cref="NanoQLException">The dates aren't sorted, with QuantLib's text.</exception>
    public NormalCLVModel(
        GeneralizedBlackScholesProcess bsProcess,
        OrnsteinUhlenbeckProcess? ouProcess,
        IEnumerable<Date> maturityDates,
        int lagrangeOrder,
        double? pMax = null,
        double? pMin = null)
    {
        ArgumentNullException.ThrowIfNull(bsProcess);
        ArgumentNullException.ThrowIfNull(maturityDates);
        Vector nodes = new GaussHermiteIntegration(lagrangeOrder).X();
        double[] xs = new double[nodes.Size];
        for (int i = 0; i < xs.Length; ++i)
        {
            xs[i] = MathConstants.M_SQRT2 * nodes[i];
        }

        x = [.. xs];
        sigma = pMax is double max
            ? x[^1] / new InverseCumulativeNormal().Evaluate(max)
            : pMin is double min ? x[0] / new InverseCumulativeNormal().Evaluate(min) : 1.0;
        this.bsProcess = bsProcess;
        this.ouProcess = ouProcess;
        this.maturityDates = [.. maturityDates];
        rndCalculator = new GBSMRNDCalculator(bsProcess);
        double[] times = new double[this.maturityDates.Length];
        for (int i = 0; i < times.Length; ++i)
        {
            times[i] = bsProcess.Time(this.maturityDates[i]);
            if (!(i == 0 || times[i - 1] < times[i]))
            {
                throw new NanoQLException("dates must be sorted");
            }
        }

        maturityTimes = [.. times];
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

    /// <summary>The kernel process's collocation points at a date.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The points.</returns>
    /// <exception cref="NanoQLException">The model has no kernel process.</exception>
    public Vector CollocationPointsX(Date d)
    {
        OrnsteinUhlenbeckProcess ou = Kernel();
        double t = bsProcess.Time(d);
        double expectation = ou.Expectation(0.0, ou.X0, t);
        double stdDeviation = ou.StdDeviation(0.0, ou.X0, t);
        var points = new Vector(x.Length);
        for (int i = 0; i < x.Length; ++i)
        {
            points[i] = expectation + (stdDeviation * x[i]);
        }

        return points;
    }

    /// <summary>The underlying's collocation points at a date.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The points.</returns>
    public Vector CollocationPointsY(Date d)
    {
        var s = new Vector(x.Length);
        var n = new CumulativeNormalDistribution();
        for (int i = 0; i < x.Length; ++i)
        {
            s[i] = InvCDF(d, n.Evaluate(x[i] / sigma));
        }

        return s;
    }

    /// <summary>The CLV mapping function: the underlying's value at a time for the kernel's value then.</summary>
    /// <returns>The function of the time, positive, and the kernel's value.</returns>
    /// <exception cref="NanoQLException">The model has no kernel process, or a quantile can't be found.</exception>
    /// <exception cref="ArgumentException">There are fewer than two maturities or collocation points.</exception>
    public Func<double, double, double> G() => g.Value.Evaluate;

    // QuantLib dereferences its null process.
    private OrnsteinUhlenbeckProcess Kernel() =>
        ouProcess ?? throw new NanoQLException("no Ornstein-Uhlenbeck process given");

    // QuantLib's MappingFunction and its InterpolationData, immutable.
    private sealed class MappingFunction
    {
        private readonly double sigma;
        private readonly OrnsteinUhlenbeckProcess ouProcess;
        private readonly ImmutableArray<LinearInterpolation> interpl;
        private readonly LagrangeInterpolation lagrangeInterpl;

        public MappingFunction(NormalCLVModel model)
        {
            sigma = model.sigma;
            ouProcess = model.Kernel();
            int rows = model.x.Length;
            int columns = model.maturityDates.Length;
            var s = new double[rows, columns];
            for (int i = 0; i < columns; ++i)
            {
                Vector y = model.CollocationPointsY(model.maturityDates[i]);
                for (int j = 0; j < rows; ++j)
                {
                    s[j, i] = y[j];
                }
            }

            var rowInterpolations = ImmutableArray.CreateBuilder<LinearInterpolation>(rows);
            double[] row = new double[columns];
            for (int i = 0; i < rows; ++i)
            {
                for (int j = 0; j < columns; ++j)
                {
                    row[j] = s[i, j];
                }

                rowInterpolations.Add(new LinearInterpolation(model.maturityTimes.AsSpan(), row));
            }

            interpl = rowInterpolations.MoveToImmutable();
            lagrangeInterpl = new LagrangeInterpolation(model.x.AsSpan(), model.x.AsSpan());
        }

        public double Evaluate(double t, double x)
        {
            if (!(t > 0.0))
            {
                throw new NanoQLException(
                    "the mapping function needs a positive time: the kernel's standard deviation is 0 at time 0");
            }

            double[] y = new double[interpl.Length];
            for (int i = 0; i < y.Length; ++i)
            {
                y[i] = interpl[i].Evaluate(t, true);
            }

            double expectation = ouProcess.Expectation(0.0, ouProcess.X0, t);
            double stdDeviation = ouProcess.StdDeviation(0.0, ouProcess.X0, t);
            double r = sigma * (x - expectation) / stdDeviation;
            return lagrangeInterpl.Value(y, r);
        }
    }
}
