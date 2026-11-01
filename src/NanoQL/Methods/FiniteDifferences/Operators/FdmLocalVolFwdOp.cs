// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmlocalvolfwdop.hpp ql/methods/finitedifferences/operators/fdmlocalvolfwdop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Methods;

/// <summary>
/// The local-volatility forward (Fokker-Planck) operator in the logarithm of the underlying, in one direction: the
/// density's drift −(r − q − σ²/2) and half the local variance at each point, each on the columns of the first and second
/// derivatives, over a time step.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-van plan's VD11): <see cref="SetTime"/> rewrites the operator's bands in place, through
/// buffers it holds, by QuantLib's arithmetic in QuantLib's order, so a time step allocates nothing. The spot is
/// QuantLib's constructor's argument, which QuantLib's operator doesn't read.
/// </para>
/// <para>
/// Its times run on the risk-free curve's day counter from the earliest reference date of its three structures, as
/// <see cref="FdmBlackScholesOp.Clock"/>'s, and each structure is read at the instants the step's times denote, on its
/// own clock, where QuantLib reads them at the risk-free curve's times (the S-van plan's VR4 (xxiii), the
/// mismatched-dates class); structures that share the risk-free curve's times keep QuantLib's bits. A documented
/// divergence.
/// </para>
/// </remarks>
[WorkingType]
public sealed class FdmLocalVolFwdOp : FdmLinearOpComposite
{
    private readonly FdmMesher mesher;
    private readonly YieldTermStructure rTS;
    private readonly YieldTermStructure qTS;
    private readonly TermStructure clock;
    private readonly LocalVolTermStructure localVol;
    private readonly double[] x;
    private readonly FirstDerivativeOp dxMap;
    private readonly TripleBandLinearOp dxxMap;
    private readonly TripleBandLinearOp mapT;
    private readonly int direction;

    // the step's columns' scales, held: the drift's and half the variance
    private readonly double[] driftScale;
    private readonly double[] halfV;

    /// <summary>Initializes a new instance of the <see cref="FdmLocalVolFwdOp"/> class.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="spot">The spot, which the operator doesn't read, as QuantLib's.</param>
    /// <param name="rTS">The risk-free curve.</param>
    /// <param name="qTS">The dividend curve.</param>
    /// <param name="localVol">The local volatility.</param>
    /// <param name="direction">The direction of the underlying.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    public FdmLocalVolFwdOp(
        FdmMesher mesher, double spot, YieldTermStructure rTS, YieldTermStructure qTS, LocalVolTermStructure localVol, int direction = 0)
        : this(mesher, rTS, qTS, localVol, Clock(rTS, qTS, localVol), direction)
    {
        _ = spot;
    }

    // the operator on a given clock: LocalVolRNDCalculator's, whose times are its local volatility's
    internal FdmLocalVolFwdOp(
        FdmMesher mesher, YieldTermStructure rTS, YieldTermStructure qTS, LocalVolTermStructure localVol, TermStructure clock, int direction)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        this.mesher = mesher;
        this.rTS = rTS;
        this.qTS = qTS;
        this.localVol = localVol;
        this.clock = clock;
        x = [.. ArrayFunctions.Exp(mesher.Locations(direction)).AsSpan()];
        dxMap = new FirstDerivativeOp(direction, mesher);
        dxxMap = new SecondDerivativeOp(direction, mesher);
        mapT = new TripleBandLinearOp(direction, mesher);
        this.direction = direction;
        driftScale = new double[mesher.Layout.Size];
        halfV = new double[mesher.Layout.Size];
    }

    /// <inheritdoc/>
    public override int Size => 1;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="t2"/> is before <paramref name="t1"/>, or a curve can't be read.
    /// </exception>
    /// <exception cref="NanoQLException">A curve or the local volatility can't be read.</exception>
    public override void SetTime(double t1, double t2)
    {
        double r = rTS.ContinuousForwardRateAt(clock, t1, t2);
        double q = qTS.ContinuousForwardRateAt(clock, t1, t2);

        // the local volatility at the step's midpoint on its own clock, its variance scaled to the clock's time by its
        // accrual over the step, as FdmBlackScholesOp reads it; QuantLib's read where the two share their times
        bool shares = localVol.SharesTimes(clock);
        double tMid = 0.5 * (t1 + t2);
        double lvTime = shares ? tMid : localVol.AccrualTime(clock, tMid);
        double scale = shares ? 1.0 : localVol.AccruedPerClockTime(clock, t1, t2);

        int n = mesher.Layout.Size;
        for (int i = 0; i < n; ++i)
        {
            double lv = localVol.LocalVol(lvTime, x[i], true);
            double v = shares ? lv * lv : lv * lv * scale;
            driftScale[i] = -r + q + (0.5 * v);
            halfV[i] = 0.5 * v;
        }

        mapT.SetSumOfMultR(dxMap, driftScale, dxxMap, halfV);
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

    // FdmBlackScholesOp.Clock's form over the three structures: the risk-free curve's day counter from the earliest
    // reference date, the curve itself where none is dated before it
    private static TermStructure Clock(YieldTermStructure rTS, YieldTermStructure qTS, LocalVolTermStructure localVol)
    {
        ArgumentNullException.ThrowIfNull(rTS);
        ArgumentNullException.ThrowIfNull(qTS);
        ArgumentNullException.ThrowIfNull(localVol);
        Date origin = rTS.ReferenceDate;
        foreach (TermStructure structure in new TermStructure[] { qTS, localVol })
        {
            if (structure.ReferenceDate < origin)
            {
                origin = structure.ReferenceDate;
            }
        }

        return origin == rTS.ReferenceDate ? rTS : new FlatForward(origin, 0.0, rTS.DayCounter);
    }
}
