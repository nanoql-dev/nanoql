// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/localvolrndcalculator.hpp ql/methods/finitedifferences/utilities/localvolrndcalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The risk-neutral density of the logarithm of the underlying under a local volatility: the forward (Fokker-Planck)
/// equation solved by Douglas's scheme from a Gaussian first step, on a grid that widens where probability mass leaks
/// past its ends.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: the forward equation is solved at construction (the playbook's <c>LazyObject</c> row), where QuantLib
/// solves it at the first query, and its meshers and density interpolations are held read-only. Its argument x is ln(S)
/// and its times are the local volatility's.
/// </para>
/// <para>
/// The risk-free and dividend curves are read at the instants the local volatility's times denote, each on its own
/// clock, where QuantLib reads them at those times (the mismatched-dates class); curves that share the local
/// volatility's times keep QuantLib's bits. A documented divergence.
/// </para>
/// </remarks>
public sealed class LocalVolRNDCalculator : RiskNeutralDensityCalculator
{
    private const double QL_EPSILON = 2.220446049250313E-16;

    private readonly int xGrid;
    private readonly int tGrid;
    private readonly double localVolProbEps;
    private readonly int maxIter;
    private readonly double spot;
    private readonly LocalVolTermStructure localVol;
    private readonly YieldTermStructure rTS;
    private readonly YieldTermStructure qTS;
    private readonly TimeGrid timeGrid;
    private readonly ImmutableArray<Fdm1dMesher> xm;
    private readonly ImmutableArray<ImmutableArray<double>> pm;
    private readonly ImmutableArray<int> rescaleTimeSteps;
    private readonly ImmutableArray<Interpolation> pFct;

    /// <summary>Initializes a new instance of the <see cref="LocalVolRNDCalculator"/> class on a uniform time grid to
    /// the local volatility's last time.</summary>
    /// <param name="spot">The spot.</param>
    /// <param name="rTS">The risk-free curve.</param>
    /// <param name="qTS">The dividend curve.</param>
    /// <param name="localVol">The local volatility.</param>
    /// <param name="xGrid">The number of the log spot's points, above 10.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="x0Density">The first grid's density at the mean.</param>
    /// <param name="localVolProbEps">The probability beyond the first grid's ends, and the mass a grid's ends may hold
    /// before it widens.</param>
    /// <param name="maxIter">The integrals' and the inverse's maximum evaluations.</param>
    /// <param name="gaussianStepSize">The first, Gaussian step's length; <see langword="null"/> (QuantLib's
    /// <c>-Null&lt;Time&gt;()</c>) or a non-positive value for half the first step.</param>
    /// <exception cref="ArgumentNullException">A structure is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The grid is too small, with QuantLib's message.</exception>
    /// <exception cref="NanoQLException">A structure can't be read, or a step can't be solved.</exception>
    public LocalVolRNDCalculator(
        double spot,
        YieldTermStructure rTS,
        YieldTermStructure qTS,
        LocalVolTermStructure localVol,
        int xGrid = 101,
        int tGrid = 51,
        double x0Density = 0.1,
        double localVolProbEps = 1e-6,
        int maxIter = 10000,
        double? gaussianStepSize = null)
        : this(spot, rTS, qTS, localVol, new TimeGrid(LocalVolOf(localVol).MaxTime, tGrid), xGrid, x0Density, localVolProbEps, maxIter, gaussianStepSize)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalVolRNDCalculator"/> class on a given time
    /// grid.</summary>
    /// <param name="spot">The spot.</param>
    /// <param name="rTS">The risk-free curve.</param>
    /// <param name="qTS">The dividend curve.</param>
    /// <param name="localVol">The local volatility.</param>
    /// <param name="timeGrid">The time grid, from 0.</param>
    /// <param name="xGrid">The number of the log spot's points, above 10.</param>
    /// <param name="x0Density">The first grid's density at the mean.</param>
    /// <param name="eps">The probability beyond the first grid's ends, and the mass a grid's ends may hold before it
    /// widens.</param>
    /// <param name="maxIter">The integrals' and the inverse's maximum evaluations.</param>
    /// <param name="gaussianStepSize">The first, Gaussian step's length; <see langword="null"/> or a non-positive value
    /// for half the first step.</param>
    /// <exception cref="ArgumentNullException">A structure or the grid is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The grid is too small, with QuantLib's message.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeGrid"/> has no step.</exception>
    /// <exception cref="NanoQLException">A structure can't be read, or a step can't be solved.</exception>
    public LocalVolRNDCalculator(
        double spot,
        YieldTermStructure rTS,
        YieldTermStructure qTS,
        LocalVolTermStructure localVol,
        TimeGrid timeGrid,
        int xGrid = 101,
        double x0Density = 0.1,
        double eps = 1e-6,
        int maxIter = 10000,
        double? gaussianStepSize = null)
    {
        ArgumentNullException.ThrowIfNull(rTS);
        ArgumentNullException.ThrowIfNull(qTS);
        ArgumentNullException.ThrowIfNull(localVol);
        ArgumentNullException.ThrowIfNull(timeGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        if (!(timeGrid.Size >= 2))
        {
            throw new ArgumentException("the time grid has no step", nameof(timeGrid));
        }

        this.xGrid = xGrid;
        tGrid = timeGrid.Size - 1;
        localVolProbEps = eps;
        this.maxIter = maxIter;
        this.spot = spot;
        this.localVol = localVol;
        this.rTS = rTS;
        this.qTS = qTS;
        this.timeGrid = timeGrid;

        // QuantLib's performCalculations, at construction
        var meshers = new Fdm1dMesher[tGrid];
        var densities = new ImmutableArray<double>[tGrid];
        var interpolations = new Interpolation[tGrid];
        var rescaled = ImmutableArray.CreateBuilder<int>();

        double sT = timeGrid[1];
        double t = Math.Min(sT, (gaussianStepSize > 0.0) ? gaussianStepSize.Value : 0.5 * sT);
        double vol = localVol.LocalVol(0.0, spot);

        double stdDev = vol * Math.Sqrt(t);
        double xm0 = (-0.5 * stdDev * stdDev) + Math.Log(spot * Discount(qTS, t) / Discount(rTS, t));

        double stdDevOfFirstStep = vol * Math.Sqrt(sT);
        double normInvEps = new InverseCumulativeNormal().Evaluate(1 - localVolProbEps);

        double sLowerBound = xm0 - (normInvEps * stdDevOfFirstStep);
        double sUpperBound = xm0 + (normInvEps * stdDevOfFirstStep);

        Fdm1dMesher mesher = new Concentrating1dMesher(sLowerBound, sUpperBound, xGrid, (xm0, x0Density), true);

        var p = new Vector(mesher.Size);
        var x = new Vector(mesher.Locations().AsSpan());

        var gaussianPDF = new NormalDistribution(xm0, vol * Math.Sqrt(t));

        for (int idx = 0; idx < p.Size; ++idx)
        {
            p[idx] = gaussianPDF.Evaluate(x[idx]);
        }

        p = RescalePDF(x, p);

        if (!(x.Size > 10))
        {
            throw new ArgumentException("x grid is too small. Minimum size is greater than 10", nameof(xGrid));
        }

        int b = Math.Max(1, (int)(x.Size * 0.04));

        var evolver = new DouglasScheme(0.5, ForwardOp(mesher));

        for (int i = 1; i <= tGrid; ++i)
        {
            double dt = timeGrid[i] - t;

            // leaking probability mass?
            double maxLeftValue = Math.Max(Math.Abs(Min(p, 0, b)), Math.Abs(Max(p, 0, b)));
            double maxRightValue = Math.Max(Math.Abs(Min(p, p.Size - b, p.Size)), Math.Abs(Max(p, p.Size - b, p.Size)));

            if (Math.Max(maxLeftValue, maxRightValue) > localVolProbEps)
            {
                rescaled.Add(i);

                double oldLowerBound = sLowerBound;
                double oldUpperBound = sUpperBound;

                double xm = DiscreteSimpsonIntegral.Integrate(x.AsSpan(), (x * p).AsSpan());
                var vols = new Vector(x.Size);
                for (int j = 0; j < vols.Size; ++j)
                {
                    vols[j] = localVol.LocalVol(t + dt, Math.Exp(x[j]));
                }

                double vm = DiscreteSimpsonIntegral.Integrate(x.AsSpan(), vols.AsSpan()) / (x[x.Size - 1] - x[0]);

                double scalingFactor = vm * Math.Sqrt(0.5 * timeGrid.Back());

                if (maxLeftValue > localVolProbEps)
                {
                    sLowerBound -= scalingFactor * (oldUpperBound - oldLowerBound);
                }

                if (maxRightValue > localVolProbEps)
                {
                    sUpperBound += scalingFactor * (oldUpperBound - oldLowerBound);
                }

                mesher = new Concentrating1dMesher(sLowerBound, sUpperBound, xGrid, (xm, 0.1), false);

                var pSpline = new CubicNaturalSpline(x.AsSpan(), p.AsSpan());
                var xn = new Vector(mesher.Locations().AsSpan());
                var pn = new Vector(xn.Size, 0.0);

                for (int j = 0; j < xn.Size; ++j)
                {
                    if (xn[j] >= oldLowerBound && xn[j] <= oldUpperBound)
                    {
                        pn[j] = pSpline.Evaluate(xn[j]);
                    }
                }

                x = xn;
                p = RescalePDF(xn, pn);

                evolver = new DouglasScheme(0.5, ForwardOp(mesher));
            }

            evolver.SetStep(dt);
            t += dt;

            if (dt > QL_EPSILON)
            {
                evolver.Step(p.AsSpan(), t);
                p = RescalePDF(x, p);
            }

            meshers[i - 1] = mesher;
            densities[i - 1] = [.. p.AsSpan()];
            interpolations[i - 1] = new CubicNaturalSpline(mesher.Locations().AsSpan(), densities[i - 1].AsSpan());
        }

        xm = [.. meshers];
        pm = [.. densities];
        pFct = [.. interpolations];
        rescaleTimeSteps = rescaled.ToImmutable();
    }

    /// <summary>Gets the time grid.</summary>
    public TimeGrid TimeGrid => timeGrid;

    /// <summary>The density of ln(S) at a time, interpolated between the grid's times, Gaussian before the first.</summary>
    /// <param name="x">The logarithm of the underlying.</param>
    /// <param name="t">The time, positive and within the grid.</param>
    /// <returns>The density.</returns>
    /// <exception cref="ArgumentException">The time isn't positive, or exceeds the grid, with QuantLib's
    /// messages.</exception>
    /// <exception cref="NanoQLException">A structure can't be read.</exception>
    public override double Pdf(double x, double t)
    {
        if (!(t > 0))
        {
            throw new ArgumentException("positive time expected", nameof(t));
        }

        if (!(t <= timeGrid.Back()))
        {
            throw new ArgumentException("given time exceeds local vol time grid", nameof(t));
        }

        double tMin = Math.Min(timeGrid[1], 1.0 / 365);

        if (t <= tMin)
        {
            double vol = localVol.LocalVol(0.0, spot);
            double stdDev = vol * Math.Sqrt(t);
            double xm = (-0.5 * stdDev * stdDev) + Math.Log(spot * Discount(qTS, t) / Discount(rTS, t));

            return new NormalDistribution(xm, stdDev).Evaluate(x);
        }
        else if (t <= timeGrid[1])
        {
            double vol = localVol.LocalVol(0.0, spot);
            double stdDev = vol * Math.Sqrt(tMin);
            double xm = (-0.5 * stdDev * stdDev) + Math.Log(spot * Discount(qTS, tMin) / Discount(rTS, tMin));

            var gaussianPDF = new NormalDistribution(xm, stdDev);

            double deltaT = timeGrid[1] - tMin;
            return (gaussianPDF.Evaluate(x) * (timeGrid[1] - t) / deltaT) + (ProbabilityInterpolation(0, x) * (t - tMin) / deltaT);
        }
        else
        {
            int lb = LowerBound(t);
            int llb = lb - 1;

            int idx = lb - 1;

            double deltaT = timeGrid[lb] - timeGrid[llb];
            return (ProbabilityInterpolation(idx - 1, x) * (timeGrid[lb] - t) / deltaT)
                + (ProbabilityInterpolation(idx, x) * (t - timeGrid[llb]) / deltaT);
        }
    }

    /// <summary>The distribution function of ln(S) at a time: the density's Gauss-Lobatto integral from the nearer
    /// end of the grid.</summary>
    /// <param name="x">The logarithm of the underlying.</param>
    /// <param name="t">The time, within the grid.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentException">The time isn't positive, or exceeds the grid.</exception>
    /// <exception cref="NanoQLException">The integral doesn't converge, or a structure can't be read.</exception>
    public override double Cdf(double x, double t)
    {
        // get the left side of the integral
        double tc = timeGrid.ClosestTime(t);
        int idx = (tc > t) ? timeGrid.Index(tc) - 1 : Math.Min(xm.Length - 1, timeGrid.Index(tc));

        double xl = xm[idx].Location(0);
        double xr = xm[idx].Location(xm[idx].Size - 1);

        if (x < xl)
        {
            return 0.0;
        }
        else if (x > xr)
        {
            return 1.0;
        }

        double addition = 0.1 * (xr - xl);

        // left or right hand integral
        if (x > 0.5 * (xr + xl))
        {
            while (Pdf(xr, t) > 0.01 * localVolProbEps)
            {
                addition *= 1.1;
                xr += addition;
            }

            return 1.0 - new GaussLobattoIntegral(maxIter, 0.1 * localVolProbEps).Integrate(u => Pdf(u, t), x, xr);
        }
        else
        {
            while (Pdf(xl, t) > 0.01 * localVolProbEps)
            {
                addition *= 1.1;
                xl -= addition;
            }

            return new GaussLobattoIntegral(maxIter, 0.1 * localVolProbEps).Integrate(u => Pdf(u, t), xl, x);
        }
    }

    /// <summary>The inverse of the distribution function at a time, by Brent's method from the density's mean on the
    /// nearest grid time.</summary>
    /// <param name="p">The probability.</param>
    /// <param name="t">The time, within the grid.</param>
    /// <returns>The logarithm of the underlying.</returns>
    /// <exception cref="NanoQLException">Brent's method fails, or a structure can't be read.</exception>
    public override double InvCdf(double p, double t)
    {
        double closeGridTime = timeGrid.ClosestTime(t);

        if (closeGridTime == 0.0)
        {
            double stepSize = 0.02 * (xm[0].Location(xm[0].Size - 1) - xm[0].Location(0));
            return new InvCdfHelper(this, Math.Log(spot), 0.1 * localVolProbEps, maxIter, stepSize).InverseCdf(p, t);
        }
        else
        {
            int idx = timeGrid.Index(closeGridTime) - 1;

            ImmutableArray<double> x = xm[idx].Locations();
            double stepSize = 0.005 * (x[^1] - x[0]);

            var xp = new double[xGrid];
            for (int i = 0; i < xp.Length; ++i)
            {
                xp[i] = x[i] * pm[idx][i];
            }

            double mean = DiscreteSimpsonIntegral.Integrate(x.AsSpan(), xp);
            return new InvCdfHelper(this, mean, 0.1 * localVolProbEps, maxIter, stepSize).InverseCdf(p, t);
        }
    }

    /// <summary>The grid at a time: the step's mesher ending at it, or at time 0 every point at the log spot.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The mesher.</returns>
    /// <exception cref="ArgumentException">The time is past the grid, with QuantLib's message.</exception>
    public Fdm1dMesher Mesher(double t)
    {
        int idx = timeGrid.Index(t);
        if (!(idx <= xm.Length))
        {
            throw new ArgumentException(
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"inconsistent time {t} given"), nameof(t));
        }

        if (idx > 0)
        {
            return xm[idx - 1];
        }
        else
        {
            return new Predefined1dMesher(Enumerable.Repeat(Math.Log(spot), xGrid));
        }
    }

    /// <summary>The steps at which the grid widened, QuantLib's <c>rescaleTimeSteps()</c>.</summary>
    /// <returns>The steps' indices in the time grid.</returns>
    public ImmutableArray<int> RescaleTimeSteps() => rescaleTimeSteps;

    private static LocalVolTermStructure LocalVolOf(LocalVolTermStructure localVol)
    {
        ArgumentNullException.ThrowIfNull(localVol);
        return localVol;
    }

    private static Vector RescalePDF(Vector x, Vector p) => p / DiscreteSimpsonIntegral.Integrate(x.AsSpan(), p.AsSpan());

    private static double Min(Vector p, int from, int to)
    {
        double m = p[from];
        for (int i = from + 1; i < to; ++i)
        {
            if (p[i] < m)
            {
                m = p[i];
            }
        }

        return m;
    }

    private static double Max(Vector p, int from, int to)
    {
        double m = p[from];
        for (int i = from + 1; i < to; ++i)
        {
            if (m < p[i])
            {
                m = p[i];
            }
        }

        return m;
    }

    // a curve's discount at the instant a time on the local volatility's clock denotes
    private double Discount(YieldTermStructure curve, double t) => curve.DiscountAt(localVol, t);

    // the forward operator on the local volatility's clock
    private FdmLocalVolFwdOp ForwardOp(Fdm1dMesher mesher) =>
        new(new FdmMesherComposite(mesher), rTS, qTS, localVol, localVol, 0);

    private double ProbabilityInterpolation(int idx, double x)
    {
        if (x < xm[idx].Location(0) || x > xm[idx].Location(xm[idx].Size - 1))
        {
            return 0.0;
        }
        else
        {
            return pFct[idx].Evaluate(x);
        }
    }

    // std::lower_bound over the time grid
    private int LowerBound(double t)
    {
        int lo = 0, hi = timeGrid.Size;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (timeGrid[mid] < t)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}
