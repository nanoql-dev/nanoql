// SPDX-FileCopyrightText: Copyright (C) 2012, 2013, 2015 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2014, 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2014 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmhestonfwdop.hpp ql/methods/finitedifferences/operators/fdmhestonfwdop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The Heston Fokker-Planck forward operator on a mesher of the log spot in direction 0 and the variance (or a
/// transformation of it, <see cref="FdmSquareRootFwdOpTransformationType"/>) in direction 1: the density's transport
/// under the log spot's drift r − q − ½vL² and diffusion ½vL², the variance's square-root dynamics and their
/// correlation, L a leverage function's value at each point (1 without one), with zero-flux boundaries in the variance.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-hes plan's HD8): built at construction; <see cref="SetTime"/> rewrites the equity part in
/// place, the leverage composition written straight into its bands by QuantLib's arithmetic in its order, and the
/// apply, splitting solves and preconditioner write into the caller's spans through buffers it holds, so a step
/// allocates nothing.
/// </para>
/// <para>
/// The curves and the leverage function are read on a clock, each at the instants its times denote: QuantLib reads the
/// dividend curve and the leverage at the risk-free curve's times (HR7 (xxi)). The public constructor's clock is
/// <see cref="FdmHestonOp.Clock"/>'s, the risk-free curve itself where no structure is dated before it, QuantLib's
/// reads.
/// </para>
/// </remarks>
[WorkingType]
public sealed class FdmHestonFwdOp : FdmLinearOpComposite
{
    private readonly FdmSquareRootFwdOpTransformationType type;
    private readonly double kappa;
    private readonly double theta;
    private readonly double rho;
    private readonly double mixedSigma;

    private readonly TermStructure clock;
    private readonly YieldTermStructure rTS;
    private readonly YieldTermStructure qTS;

    private readonly double[] varianceValues;
    private readonly ModTripleBandLinearOp dxMap;
    private readonly ModTripleBandLinearOp dxxMap;
    private readonly ModTripleBandLinearOp boundary;

    private readonly ModTripleBandLinearOp mapX;
    private readonly FdmSquareRootFwdOp mapY;
    private readonly NinePointLinearOp correlation;

    private readonly LocalVolTermStructure? leverageFct;

    // the leverage slice, its square and the drift's correlation term at each point; the log spots of the first
    // variance row, each point's row position and the row's leverage; QuantLib's 0.5·exp(2.0·v/2) for the log
    // transformation; and apply's terms, held
    private readonly double[] l;
    private readonly double[] lSquare;
    private readonly double[] w;
    private readonly double[] xRow;
    private readonly int[] nx;
    private readonly double[] lRow;
    private readonly double[] logDrift;
    private readonly double[] drift;
    private readonly double[] tempX;
    private readonly double[] tempY;
    private readonly double[] lu;

    /// <summary>Initializes a new instance of the <see cref="FdmHestonFwdOp"/> class.</summary>
    /// <param name="mesher">The mesher: the log spot in direction 0, the variance or its transformation in direction 1,
    /// at least two points in each.</param>
    /// <param name="process">The Heston process.</param>
    /// <param name="type">The variable the density is written in, in the variance's direction.</param>
    /// <param name="leverageFct">The leverage function, or <see langword="null"/> for none.</param>
    /// <param name="mixingFactor">The factor scaling the variance's volatility σ.</param>
    /// <exception cref="ArgumentNullException">The mesher or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The mesher has fewer than two directions, or <paramref name="type"/> isn't a member of its type.
    /// </exception>
    /// <exception cref="ArgumentException">A direction has fewer than two points.</exception>
    public FdmHestonFwdOp(
        FdmMesher mesher,
        HestonProcess process,
        FdmSquareRootFwdOpTransformationType type = FdmSquareRootFwdOpTransformationType.Plain,
        LocalVolTermStructure? leverageFct = null,
        double mixingFactor = 1.0)
        : this(mesher, process, type, leverageFct, mixingFactor, null)
    {
    }

    // The SLV model's form: the operator's times the model's clock.
    internal FdmHestonFwdOp(
        FdmMesher mesher,
        HestonProcess process,
        FdmSquareRootFwdOpTransformationType type,
        LocalVolTermStructure? leverageFct,
        double mixingFactor,
        TermStructure? clock)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfLessThan(mesher.Layout.Dim.Length, 2, nameof(mesher));
        this.type = type;
        kappa = process.Kappa;
        theta = process.Theta;
        rho = process.Rho;
        mixedSigma = mixingFactor * process.Sigma;
        rTS = process.RiskFreeRate;
        qTS = process.DividendYield;
        this.clock = clock ?? FdmHestonOp.Clock(process, null, leverageFct);
        this.leverageFct = leverageFct;

        // the square-root operator checks the transformation and the variance's points
        mapY = new FdmSquareRootFwdOp(mesher, kappa, theta, mixedSigma, 1, type);

        int n = mesher.Layout.Size;
        Vector locations1 = mesher.Locations(1);
        varianceValues = new double[n];
        for (int i = 0; i < n; ++i)
        {
            varianceValues[i] = 0.5 * locations1[i];
        }

        dxMap = new ModTripleBandLinearOp(new FirstDerivativeOp(0, mesher));
        var dxxScale = new Vector(n);
        for (int i = 0; i < n; ++i)
        {
            dxxScale[i] = type == FdmSquareRootFwdOpTransformationType.Log ? 0.5 * Math.Exp(locations1[i]) : 0.5 * locations1[i];
        }

        dxxMap = new ModTripleBandLinearOp(new SecondDerivativeOp(0, mesher).Mult(dxxScale));
        boundary = new ModTripleBandLinearOp(new SecondDerivativeOp(0, mesher).Mult(new Vector(mesher.Locations(0).Size, 0.0)));
        mapX = new ModTripleBandLinearOp(0, mesher);

        var correlationScale = new Vector(n);
        for (int i = 0; i < n; ++i)
        {
            correlationScale[i] = type == FdmSquareRootFwdOpTransformationType.Log ? rho * mixedSigma : rho * mixedSigma * locations1[i];
        }

        correlation = new SecondOrderMixedDerivativeOp(0, 1, mesher).Mult(correlationScale);

        // zero flux boundary condition
        int nv = mesher.Layout.Dim[1];
        double lowerBoundaryFactor = mapY.LowerBoundaryFactor(type);
        double upperBoundaryFactor = mapY.UpperBoundaryFactor(type);

        double logFacLow = type == FdmSquareRootFwdOpTransformationType.Log ? Math.Exp(mapY.V(0)) : 1.0;
        double logFacUpp = type == FdmSquareRootFwdOpTransformationType.Log ? Math.Exp(mapY.V(nv + 1)) : 1.0;

        double alpha = -2 * rho / mixedSigma * lowerBoundaryFactor * logFacLow;
        double beta = -2 * rho / mixedSigma * upperBoundaryFactor * logFacUpp;

        ModTripleBandLinearOp fDx = dxMap;
        int dim0 = mesher.Layout.Dim[0];
        nx = new int[n];
        xRow = new double[dim0];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int k = 0; k < n; ++k, iter.Increment())
        {
            int idx = iter.Index;
            int c1 = iter.Coordinates[1];
            nx[idx] = iter.Coordinates[0];
            if (c1 == 0)
            {
                xRow[iter.Coordinates[0]] = Math.Exp(mesher.Location(iter, 0));
                if (leverageFct is null)
                {
                    dxxMap.Upper(idx) += alpha * fDx.Upper(idx);
                    dxxMap.Diag(idx) += alpha * fDx.Diag(idx);
                    dxxMap.Lower(idx) += alpha * fDx.Lower(idx);
                }

                boundary.Upper(idx) = alpha * fDx.Upper(idx);
                boundary.Diag(idx) = alpha * fDx.Diag(idx);
                boundary.Lower(idx) = alpha * fDx.Lower(idx);
            }
            else if (c1 == nv - 1)
            {
                if (leverageFct is null)
                {
                    dxxMap.Upper(idx) += beta * fDx.Upper(idx);
                    dxxMap.Diag(idx) += beta * fDx.Diag(idx);
                    dxxMap.Lower(idx) += beta * fDx.Lower(idx);
                }

                boundary.Upper(idx) = beta * fDx.Upper(idx);
                boundary.Diag(idx) = beta * fDx.Diag(idx);
                boundary.Lower(idx) = beta * fDx.Lower(idx);
            }
        }

        l = new double[n];
        Array.Fill(l, 1.0);
        lSquare = new double[n];
        w = new double[n];
        lRow = new double[dim0];
        logDrift = new double[n];
        for (int i = 0; i < n; ++i)
        {
            logDrift[i] = 0.5 * Math.Exp(2.0 * varianceValues[i]);
        }

        drift = new double[n];
        tempX = new double[n];
        tempY = new double[n];
        lu = new double[n];
    }

    /// <inheritdoc/>
    public override int Size => 2;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="t2"/> is before <paramref name="t1"/>, or a curve can't be read.
    /// </exception>
    /// <exception cref="NanoQLException">A curve or the leverage function can't be read.</exception>
    public override void SetTime(double t1, double t2)
    {
        double r = rTS.ContinuousForwardRateAt(clock, t1, t2);
        double q = qTS.ContinuousForwardRateAt(clock, t1, t2);
        int n = l.Length;
        if (leverageFct is not null)
        {
            SetLeverageFctSlice(t1, t2);
            for (int i = 0; i < n; ++i)
            {
                lSquare[i] = l[i] * l[i];
            }

            double s = -r + q;
            if (type == FdmSquareRootFwdOpTransformationType.Log)
            {
                SetLeverageMap(s, logDrift, withCorrelationTerm: false);
            }
            else
            {
                double c = type == FdmSquareRootFwdOpTransformationType.Plain
                    ? rho * mixedSigma
                    : rho * 2.0 * kappa * theta / mixedSigma;
                for (int i = 0; i < n; ++i)
                {
                    w[i] = c * l[i];
                }

                SetLeverageMap(s, varianceValues, withCorrelationTerm: true);
            }
        }
        else
        {
            if (type == FdmSquareRootFwdOpTransformationType.Plain)
            {
                double c = -r + q + (rho * mixedSigma);
                for (int i = 0; i < n; ++i)
                {
                    drift[i] = c + varianceValues[i];
                }
            }
            else if (type == FdmSquareRootFwdOpTransformationType.Power)
            {
                double c = -r + q + (rho * 2.0 * kappa * theta / mixedSigma);
                for (int i = 0; i < n; ++i)
                {
                    drift[i] = c + varianceValues[i];
                }
            }
            else
            {
                double c = -r + q;
                for (int i = 0; i < n; ++i)
                {
                    drift[i] = c + logDrift[i];
                }
            }

            // axpyb(drift, dx, dxx, Array())
            for (int i = 0; i < n; ++i)
            {
                double a = drift[i];
                mapX.Diag(i) = dxxMap.Diag(i) + (a * dxMap.Diag(i));
                mapX.Lower(i) = dxxMap.Lower(i) + (a * dxMap.Lower(i));
                mapX.Upper(i) = dxxMap.Upper(i) + (a * dxMap.Upper(i));
            }
        }
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        mapX.Apply(r, tempX);
        mapY.Apply(r, tempY);
        if (leverageFct is not null)
        {
            for (int i = 0; i < r.Length; ++i)
            {
                lu[i] = l[i] * r[i];
            }

            correlation.Apply(lu, result);
        }
        else
        {
            correlation.Apply(r, result);
        }

        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = tempX[i] + tempY[i] + result[i];
        }
    }

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result)
    {
        if (leverageFct is not null)
        {
            for (int i = 0; i < r.Length; ++i)
            {
                lu[i] = l[i] * r[i];
            }

            correlation.Apply(lu, result);
        }
        else
        {
            correlation.Apply(r, result);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">The direction is above 1, with QuantLib's message.</exception>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == 0)
        {
            mapX.Apply(r, result);
        }
        else if (direction == 1)
        {
            mapY.Apply(r, result);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">The direction is above 1, with QuantLib's message.</exception>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == 0)
        {
            mapX.SolveSplitting(r, s, 1.0, result);
        }
        else if (direction == 1)
        {
            mapY.SolveSplitting(1, r, s, result);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
        }
    }

    /// <inheritdoc/>
    /// <remarks>QuantLib's preconditioner: the splitting solve in the variance's direction alone.</remarks>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) =>
        SolveSplitting(1, r, s, result);

    /// <inheritdoc/>
    /// <remarks>QuantLib's decomposition: the equity part, the square-root operator and the correlation map without
    /// the leverage.</remarks>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() => [mapX.ToMatrix(), mapY.ToMatrix(), correlation.ToMatrix()];

    // QuantLib's mapX.axpyb(Array(1, s), dx, Y, Array()) with Y, for Plain and Power,
    // dxx.multR(L²).add(boundary.multR(L)).add(dx.multR(w)).add(dx.mult(m).multR(L²)), m the variance values, and for
    // Log dxx.multR(L²).add(boundary.multR(L)).add(dx.mult(m).multR(L²)), m QuantLib's 0.5·exp(2.0·v/2): each
    // temporary's value written straight into the bands, in QuantLib's order (multR's neighbours i − 1 and i + 1, 1
    // past the ends; the sums left to right; axpyb's y + s·x)
    private void SetLeverageMap(double s, double[] m, bool withCorrelationTerm)
    {
        int size = l.Length;
        for (int i = 0; i < size; ++i)
        {
            double lSqM1 = i > 0 ? lSquare[i - 1] : 1.0;
            double lSq0 = lSquare[i];
            double lSqP1 = i < size - 1 ? lSquare[i + 1] : 1.0;
            double lM1 = i > 0 ? l[i - 1] : 1.0;
            double l0 = l[i];
            double lP1 = i < size - 1 ? l[i + 1] : 1.0;

            double dxL = dxMap.Lower(i), dxD = dxMap.Diag(i), dxU = dxMap.Upper(i);
            double yL = (dxxMap.Lower(i) * lSqM1) + (boundary.Lower(i) * lM1);
            double yD = (dxxMap.Diag(i) * lSq0) + (boundary.Diag(i) * l0);
            double yU = (dxxMap.Upper(i) * lSqP1) + (boundary.Upper(i) * lP1);
            if (withCorrelationTerm)
            {
                double wM1 = i > 0 ? w[i - 1] : 1.0;
                double wP1 = i < size - 1 ? w[i + 1] : 1.0;
                yL += dxL * wM1;
                yD += dxD * w[i];
                yU += dxU * wP1;
            }

            double mi = m[i];
            yL += dxL * mi * lSqM1;
            yD += dxD * mi * lSq0;
            yU += dxU * mi * lSqP1;

            mapX.Diag(i) = yD + (s * dxD);
            mapX.Lower(i) = yL + (s * dxL);
            mapX.Upper(i) = yU + (s * dxU);
        }
    }

    // QuantLib's getLeverageFctSlice, into the held slice: the leverage at the step's midpoint on its own clock, at
    // each spot of the grid clamped to the function's strikes, at least 0.01, the same down every variance column
    private void SetLeverageFctSlice(double t1, double t2)
    {
        LocalVolTermStructure leverage = leverageFct!;
        double t = 0.5 * (t1 + t2);
        double ownTime = FdmHestonLocalVolatilityVarianceMesher.LeverageTime(leverage, clock, t);
        double time = Min(leverage.MaxTime, ownTime);

        for (int i = 0; i < lRow.Length; ++i)
        {
            double spot = Min(leverage.MaxStrike(), Max(leverage.MinStrike(), xRow[i]));
            lRow[i] = Max(0.01, leverage.LocalVol(time, spot, true));
        }

        for (int i = 0; i < l.Length; ++i)
        {
            l[i] = lRow[nx[i]];
        }
    }

    // std::max and std::min
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;
}
