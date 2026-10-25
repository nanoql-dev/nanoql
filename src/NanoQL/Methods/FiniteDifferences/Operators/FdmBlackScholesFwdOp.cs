// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2014 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmblackscholesfwdop.hpp ql/methods/finitedifferences/operators/fdmblackscholesfwdop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The Black-Scholes forward (Fokker-Planck) operator in the logarithm of the underlying, in one direction: the
/// density's drift −(r − q − σ²/2) and half the variance, each on the columns of the first and second derivatives, over
/// a time step, the variance the Black forward variance or, on request, the local variance at each point.
/// </summary>
/// <remarks>
/// A working type (the S-van plan's VD11): <see cref="SetTime"/> rewrites the operator's bands in place, through
/// buffers it holds, by QuantLib's arithmetic in QuantLib's order, so a time step allocates nothing. Its times and
/// reads are <see cref="FdmBlackScholesOp"/>'s: each structure read at the instants the step's times denote on
/// <see cref="FdmBlackScholesOp.Clock"/>, on its own clock, where QuantLib reads them at the risk-free curve's times
/// (the S-van plan's VR4 (xxiii), the mismatched-dates class); structures that share the risk-free curve's times keep
/// QuantLib's bits. A documented divergence.
/// </remarks>
public sealed class FdmBlackScholesFwdOp : FdmLinearOpComposite
{
    private const double NullReal = 3.4028234663852886e38;

    private readonly FdmMesher mesher;
    private readonly YieldTermStructure rTS;
    private readonly YieldTermStructure qTS;
    private readonly TermStructure clock;
    private readonly BlackVolTermStructure volTS;
    private readonly LocalVolTermStructure? localVol;
    private readonly double[] x;
    private readonly FirstDerivativeOp dxMap;
    private readonly TripleBandLinearOp dxxMap;
    private readonly TripleBandLinearOp mapT;
    private readonly double strike;
    private readonly double illegalLocalVolOverwrite;
    private readonly int direction;

    // the local-volatility step's columns' scales, held: the drift's and half the variance
    private readonly double[] driftScale;
    private readonly double[] halfV;

    /// <summary>Initializes a new instance of the <see cref="FdmBlackScholesFwdOp"/> class.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="bsProcess">The Black-Scholes process.</param>
    /// <param name="strike">The strike the Black forward variance is read at.</param>
    /// <param name="localVol">Whether to use the process's local volatility at each point.</param>
    /// <param name="illegalLocalVolOverwrite">
    /// The volatility to use where the local volatility can't be computed; <see langword="null"/> (QuantLib's
    /// <c>-Null&lt;Real&gt;()</c>) or a negative value to let the failure through.
    /// </param>
    /// <param name="direction">The direction of the underlying.</param>
    /// <exception cref="ArgumentNullException">The mesher or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    public FdmBlackScholesFwdOp(
        FdmMesher mesher,
        GeneralizedBlackScholesProcess bsProcess,
        double strike,
        bool localVol = false,
        double? illegalLocalVolOverwrite = null,
        int direction = 0)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(bsProcess);
        this.mesher = mesher;
        rTS = bsProcess.RiskFreeRate;
        qTS = bsProcess.DividendYield;
        clock = FdmBlackScholesOp.Clock(bsProcess);
        volTS = bsProcess.BlackVolatility;
        this.localVol = localVol ? bsProcess.LocalVolatility : null;
        x = localVol ? [.. ArrayFunctions.Exp(mesher.Locations(direction)).AsSpan()] : [];
        dxMap = new FirstDerivativeOp(direction, mesher);
        dxxMap = new SecondDerivativeOp(direction, mesher);
        mapT = new TripleBandLinearOp(direction, mesher);
        this.strike = strike;
        this.illegalLocalVolOverwrite = illegalLocalVolOverwrite ?? -NullReal;
        this.direction = direction;

        int n = localVol ? mesher.Layout.Size : 0;
        driftScale = new double[n];
        halfV = new double[n];
    }

    /// <inheritdoc/>
    public override int Size => 1;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="t2"/> is before <paramref name="t1"/>, or a curve or the volatility can't be read.
    /// </exception>
    /// <exception cref="NanoQLException">A curve, the volatility or the local volatility can't be read.</exception>
    public override void SetTime(double t1, double t2)
    {
        double r = rTS.ContinuousForwardRateAt(clock, t1, t2);
        double q = qTS.ContinuousForwardRateAt(clock, t1, t2);

        if (localVol is not null)
        {
            // the local volatility at the step's midpoint on its own clock, its variance scaled to the clock's time by
            // its accrual over the step, as FdmBlackScholesOp reads it; QuantLib's read where the two share their times
            bool shares = localVol.SharesTimes(clock);
            double tMid = 0.5 * (t1 + t2);
            double lvTime = shares ? tMid : localVol.AccrualTime(clock, tMid);
            double scale = shares ? 1.0 : localVol.AccruedPerClockTime(clock, t1, t2);

            int n = mesher.Layout.Size;
            for (int i = 0; i < n; ++i)
            {
                double v;
                if (illegalLocalVolOverwrite < 0.0)
                {
                    double lv = localVol.LocalVol(lvTime, x[i], true);
                    v = shares ? lv * lv : lv * lv * scale;
                }
                else
                {
                    try
                    {
                        double lv = localVol.LocalVol(lvTime, x[i], true);
                        v = shares ? lv * lv : lv * lv * scale;
                    }
                    catch (Exception e) when (e is NanoQLException or ArgumentException)
                    {
                        double overwrite = illegalLocalVolOverwrite * illegalLocalVolOverwrite;
                        v = shares ? overwrite : overwrite * scale;
                    }
                }

                driftScale[i] = -r + q + (0.5 * v);
                halfV[i] = 0.5 * v;
            }

            mapT.SetSumOfMultR(dxMap, driftScale, dxxMap, halfV);
        }
        else
        {
            double v = volTS.BlackForwardVarianceAt(clock, t1, t2, strike) / (t2 - t1);
            double s = -r + q + (0.5 * v);
            double h = 0.5 * v;
            mapT.SetAxpybOfMult(new ReadOnlySpan<double>(in s), dxMap, dxxMap, new ReadOnlySpan<double>(in h), 0.0);
        }
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result) => mapT.Apply(r, result);

    /// <inheritdoc/>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == this.direction)
        {
            mapT.Apply(r, result);
        }
        else
        {
            result[..r.Length].Clear();
        }
    }

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result) => result[..r.Length].Clear();

    /// <inheritdoc/>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == this.direction)
        {
            mapT.SolveSplitting(r, s, 1.0, result);
        }
        else
        {
            r.CopyTo(result);
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) =>
        SolveSplitting(direction, r, s, result);

    /// <inheritdoc/>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() => [mapT.ToMatrix()];
}
