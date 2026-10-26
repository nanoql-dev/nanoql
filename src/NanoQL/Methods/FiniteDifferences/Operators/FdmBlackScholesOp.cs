// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmblackscholesop.hpp ql/methods/finitedifferences/operators/fdmblackscholesop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Methods;

/// <summary>
/// The Black-Scholes operator in the logarithm of the underlying, in one direction: the drift r − q − σ²/2 on the first
/// derivative, half the variance on the second and the discounting −r, each over a time step, the variance the Black
/// forward variance or, on request, the local variance at each point.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-eq plan's SEQR1): <see cref="SetTime"/> rewrites the operator's bands in place, through
/// buffers it holds, by QuantLib's arithmetic in QuantLib's order, so a time step allocates nothing where QuantLib
/// builds new bands and arrays. The forward rates come from the curves' compound factors as QuantLib's
/// <c>forwardRate</c> computes them, without building the rate object.
/// </para>
/// <para>
/// The finite-difference engine's times are the risk-free curve's, measured from its reference date in its day counter
/// (<c>blackscholesprocess.cpp:149-152</c>), and QuantLib reads the dividend curve, the volatility and the quanto
/// helper's structures at those times (<c>fdmblackscholesop.cpp:51-66</c>), each of which measures time from its own
/// reference date in its own day counter: a one-year put on a risk-free curve on Act/360 prices 0.93% above the same
/// market restated on one day counter, and one on a risk-free curve settling two business days after its dividend
/// curve's and volatility's date 0.74% below QuantLib's analytic engine, which counts each structure from its own date.
/// Here the engine's times, the ones <see cref="SetTime"/> takes, run on the risk-free curve's day counter from the
/// earliest reference date of the structures it reads (<see cref="Clock"/>), and each structure, the risk-free curve
/// among them, is read at the instants the step's times denote, on its own clock, accruing nothing before its own
/// reference date, as the analytic engine counts it: its forward rate or variance between them per unit of the engine's
/// time, the local volatility at the step's midpoint scaled to that time. Between Actual/N day counters the instants
/// are exact, a reference date's offset a constant shift; on others a time is read through its date, linear within the
/// day. Where no structure is dated before the risk-free curve, the times are its own, and a structure that shares its
/// day counter and reference date is read as QuantLib reads it. A documented divergence (the S-eq plan's rulings of
/// 2026-10-20 and 2026-10-21, under R8).
/// </para>
/// </remarks>
public sealed class FdmBlackScholesOp : FdmLinearOpComposite
{
    private const double NullReal = 3.4028234663852886e38;

    private readonly FdmMesher mesher;
    private readonly YieldTermStructure rTS;
    private readonly TermStructure clock;
    private readonly YieldTermStructure qTS;
    private readonly BlackVolTermStructure volTS;
    private readonly LocalVolTermStructure? localVol;
    private readonly double[] x;
    private readonly FirstDerivativeOp dxMap;
    private readonly TripleBandLinearOp dxxMap;
    private readonly TripleBandLinearOp mapT;
    private readonly double strike;
    private readonly double illegalLocalVolOverwrite;
    private readonly int direction;
    private readonly FdmQuantoHelper? quantoHelper;

    // the local-volatility step's arrays, held: the variance, its half, the drift and the quanto adjustment
    private readonly double[] localVariance;
    private readonly double[] halfV;
    private readonly double[] drift;
    private readonly double[] adjustment;

    /// <summary>Initializes a new instance of the <see cref="FdmBlackScholesOp"/> class.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="bsProcess">The Black-Scholes process.</param>
    /// <param name="strike">The strike the Black forward variance is read at.</param>
    /// <param name="localVol">Whether to use the process's local volatility at each point.</param>
    /// <param name="illegalLocalVolOverwrite">
    /// The volatility to use where the local volatility can't be computed; <see langword="null"/> (QuantLib's
    /// <c>-Null&lt;Real&gt;()</c>) or a negative value to let the failure through.
    /// </param>
    /// <param name="direction">The direction of the underlying.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException">The mesher or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    public FdmBlackScholesOp(
        FdmMesher mesher,
        GeneralizedBlackScholesProcess bsProcess,
        double strike,
        bool localVol = false,
        double? illegalLocalVolOverwrite = null,
        int direction = 0,
        FdmQuantoHelper? quantoHelper = null)
        : this(mesher, bsProcess, strike, localVol, illegalLocalVolOverwrite, direction, quantoHelper, null)
    {
    }

    // The operator on a clock given, the basket engines' across their processes (the S-exo plan's ED10), or on the
    // process's own (Clock) where none is given.
    internal FdmBlackScholesOp(
        FdmMesher mesher,
        GeneralizedBlackScholesProcess bsProcess,
        double strike,
        bool localVol,
        double? illegalLocalVolOverwrite,
        int direction,
        FdmQuantoHelper? quantoHelper,
        TermStructure? clock)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(bsProcess);
        this.mesher = mesher;
        rTS = bsProcess.RiskFreeRate;
        this.clock = clock ?? Clock(bsProcess, quantoHelper);
        qTS = bsProcess.DividendYield;
        volTS = bsProcess.BlackVolatility;
        this.localVol = localVol ? bsProcess.LocalVolatility : null;
        x = localVol ? [.. ArrayFunctions.Exp(mesher.Locations(direction)).AsSpan()] : [];
        dxMap = new FirstDerivativeOp(direction, mesher);
        dxxMap = new SecondDerivativeOp(direction, mesher);
        mapT = new TripleBandLinearOp(direction, mesher);
        this.strike = strike;
        this.illegalLocalVolOverwrite = illegalLocalVolOverwrite ?? -NullReal;
        this.direction = direction;
        this.quantoHelper = quantoHelper;

        int n = localVol ? mesher.Layout.Size : 0;
        localVariance = new double[n];
        halfV = new double[n];
        drift = new double[n];
        adjustment = new double[quantoHelper is not null ? n : 0];
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
            // the local volatility at the step's midpoint on its own clock, its variance scaled to the risk-free
            // curve's time by its accrual over the step (TermStructure.AccrualTime), so nothing accrues before its
            // reference date; QuantLib's read where the two share their times
            bool shares = localVol.SharesTimes(clock);
            double tMid = 0.5 * (t1 + t2);
            double lvTime = shares ? tMid : localVol.AccrualTime(clock, tMid);
            double scale = shares ? 1.0 : localVol.AccruedPerClockTime(clock, t1, t2);

            // QuantLib walks the layout for each point's index, in storage order
            int n = mesher.Layout.Size;
            for (int i = 0; i < n; ++i)
            {
                if (illegalLocalVolOverwrite < 0.0)
                {
                    double lv = localVol.LocalVol(lvTime, x[i], true);
                    localVariance[i] = shares ? lv * lv : lv * lv * scale;
                }
                else
                {
                    try
                    {
                        double lv = localVol.LocalVol(lvTime, x[i], true);
                        localVariance[i] = shares ? lv * lv : lv * lv * scale;
                    }
                    catch (Exception e) when (e is NanoQLException or ArgumentException)
                    {
                        double overwrite = illegalLocalVolOverwrite * illegalLocalVolOverwrite;
                        localVariance[i] = shares ? overwrite : overwrite * scale;
                    }
                }
            }

            // r - q - 0.5*v, less the quanto adjustment at Sqrt(v); and 0.5*v for dxxMap_.mult
            if (quantoHelper is not null)
            {
                for (int i = 0; i < n; ++i)
                {
                    adjustment[i] = Math.Sqrt(localVariance[i]);
                }

                quantoHelper.QuantoAdjustmentAt(clock, adjustment, t1, t2, adjustment);
                for (int i = 0; i < n; ++i)
                {
                    drift[i] = r - q - (0.5 * localVariance[i]) - adjustment[i];
                }
            }
            else
            {
                for (int i = 0; i < n; ++i)
                {
                    drift[i] = r - q - (0.5 * localVariance[i]);
                }
            }

            for (int i = 0; i < n; ++i)
            {
                halfV[i] = 0.5 * localVariance[i];
            }

            mapT.SetAxpybOfMult(drift, dxMap, dxxMap, halfV, -r);
        }
        else
        {
            double v = volTS.BlackForwardVarianceAt(clock, t1, t2, strike) / (t2 - t1);

            double s;
            if (quantoHelper is not null)
            {
                double sqrtV = Math.Sqrt(v);
                double adj = 0.0;
                quantoHelper.QuantoAdjustmentAt(clock, new ReadOnlySpan<double>(in sqrtV), t1, t2, new Span<double>(ref adj));
                s = r - q - (0.5 * v) - adj;
            }
            else
            {
                s = r - q - (0.5 * v);
            }

            double h = 0.5 * v;
            mapT.SetAxpybOfMult(new ReadOnlySpan<double>(in s), dxMap, dxxMap, new ReadOnlySpan<double>(in h), -r);
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

    /// <summary>
    /// The finite-difference Black-Scholes engine's clock: the times its mesher, this operator, the solver's maturity,
    /// the step conditions and the escrowed dividend adjustment run on. Its day counter is the risk-free curve's and
    /// its reference date the earliest of the structures the engine reads, the process's curves and volatilities and
    /// the quanto helper's, so the days before a later-dated structure's date are time on the grid, each structure
    /// accruing nothing before its own date (the S-eq plan's rulings of 2026-10-21). Where no structure is dated before
    /// the risk-free curve, the clock is the risk-free curve itself, whose times are the process's.
    /// </summary>
    /// <remarks>
    /// A solver assembled from these types, as QuantLib's other finite-difference engines assemble theirs, takes its
    /// maturity and its other times from the clock's <see cref="TermStructure.TimeFromReference"/>, and the step
    /// conditions' reference date and day counter from the clock, where QuantLib takes them from the process. Its only
    /// use is its times.
    /// </remarks>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <returns>A term structure whose reference date and day counter give the engine's times.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    public static TermStructure Clock(GeneralizedBlackScholesProcess process, FdmQuantoHelper? quantoHelper = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        YieldTermStructure rTS = process.RiskFreeRate;
        Date origin = process.EarliestReferenceDate();
        TermStructure[] structures = quantoHelper is null
            ? [process.LocalVolatility]
            : [process.LocalVolatility, quantoHelper.RTS, quantoHelper.FTS, quantoHelper.FxVolTS];
        foreach (TermStructure structure in structures)
        {
            if (structure.ReferenceDate < origin)
            {
                origin = structure.ReferenceDate;
            }
        }

        return origin == rTS.ReferenceDate ? rTS : new FlatForward(origin, 0.0, rTS.DayCounter);
    }

    // The clock of an engine reading several Black-Scholes processes (the S-exo plan's ER5 (i), ED10): Clock's form
    // over all of them, the earliest reference date of every process's curves, volatilities and local volatility, on
    // the first process's risk-free day counter; the first process's risk-free curve itself where none is dated
    // before it, so a market on one clock keeps QuantLib's bits.
    internal static TermStructure ClockAcross(IReadOnlyList<GeneralizedBlackScholesProcess> processes)
    {
        YieldTermStructure rTS = processes[0].RiskFreeRate;
        Date origin = rTS.ReferenceDate;
        foreach (GeneralizedBlackScholesProcess process in processes)
        {
            Date earliest = process.EarliestReferenceDate();
            if (earliest < origin)
            {
                origin = earliest;
            }

            if (process.LocalVolatility.ReferenceDate < origin)
            {
                origin = process.LocalVolatility.ReferenceDate;
            }
        }

        return origin == rTS.ReferenceDate ? rTS : new FlatForward(origin, 0.0, rTS.DayCounter);
    }
}
