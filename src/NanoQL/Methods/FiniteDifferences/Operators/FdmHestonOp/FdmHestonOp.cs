// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2014, 2015 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmhestonop.hpp ql/methods/finitedifferences/operators/fdmhestonop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Methods;

/// <summary>
/// The Heston backward operator on a mesher of the log spot in direction 0 and the variance in direction 1: the
/// equity's drift r − q − ½vL² and diffusion ½vL², the variance's ½σ_m²v and κ(θ − v), their covariance ρσ_mvL, and
/// discounting at the rate, L a leverage function's value at each point (1 without one) and σ_m the mixing factor
/// times σ.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-hes plan's HD8, the S-eq plan's SEQR1): its parts are built at construction;
/// <see cref="SetTime"/> rewrites them in place, and the apply, splitting solves and preconditioner write into the
/// caller's spans through buffers it holds, so a step allocates nothing.
/// </para>
/// <para>
/// QuantLib's engines run from the risk-free curve's reference date (<c>process-&gt;time</c>), and its operator reads
/// the dividend curve, the quanto helper's structures and the leverage function at the risk-free curve's times
/// (<c>fdmhestonop.cpp:55-89</c>), each of which measures time from its own reference date in its own day counter:
/// FD prices +0.28% on a dividend curve on Act/365 beside a risk-free curve on Act/360, and −0.38% beside a risk-free
/// curve settling T+2. Here the operator's times are <see cref="Clock"/>'s, from the earliest reference date of the
/// structures it reads, on the risk-free curve's day counter, and each structure is read at the instants they denote,
/// on its own clock, accruing nothing before its own reference date: the rates between them per unit of the clock's
/// time, the leverage, a multiplier of the Heston volatility, at its instant. Where every structure shares the
/// risk-free curve's day counter and reference date, QuantLib's reads and bits (HR7 (ii), under R8;
/// <c>Divergences/FdHestonMismatchedTimesTests.cs</c>).
/// </para>
/// </remarks>
[WorkingType]
public sealed class FdmHestonOp : FdmLinearOpComposite
{
    private readonly NinePointLinearOp correlationMap;
    private readonly FdmHestonVariancePart dyMap;
    private readonly FdmHestonEquityPart dxMap;

    // apply's direction terms and the preconditioner's first solve, held
    private readonly double[] tempX;
    private readonly double[] tempY;

    /// <summary>Initializes a new instance of the <see cref="FdmHestonOp"/> class.</summary>
    /// <param name="mesher">The mesher: the log spot in direction 0, the variance in direction 1.</param>
    /// <param name="hestonProcess">The Heston process.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <param name="leverageFct">The leverage function, or <see langword="null"/> for none.</param>
    /// <param name="mixingFactor">The factor scaling the variance's volatility σ.</param>
    /// <exception cref="ArgumentNullException">The mesher or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The mesher has fewer than two directions.</exception>
    public FdmHestonOp(
        FdmMesher mesher,
        HestonProcess hestonProcess,
        FdmQuantoHelper? quantoHelper = null,
        LocalVolTermStructure? leverageFct = null,
        double mixingFactor = 1.0)
        : this(mesher, hestonProcess, hestonProcess?.DividendYield!, quantoHelper, leverageFct, mixingFactor, null, 0.0)
    {
    }

    // FdmBatesOp's form: the Heston process's dividend curve replaced by the jumps' spreaded one, or the dividend curve
    // with a spread added per unit of the clock's time, the clock the Bates process's.
    internal FdmHestonOp(
        FdmMesher mesher,
        HestonProcess hestonProcess,
        YieldTermStructure dividendYield,
        FdmQuantoHelper? quantoHelper,
        LocalVolTermStructure? leverageFct,
        double mixingFactor,
        TermStructure? clock,
        double dividendSpread)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(hestonProcess);
        clock ??= Clock(hestonProcess, quantoHelper, leverageFct);
        correlationMap = new SecondOrderMixedDerivativeOp(0, 1, mesher)
            .Mult(hestonProcess.Rho * hestonProcess.Sigma * mixingFactor * mesher.Locations(1));
        dyMap = new FdmHestonVariancePart(
            mesher,
            hestonProcess.RiskFreeRate,
            hestonProcess.Sigma * mixingFactor,
            hestonProcess.Kappa,
            hestonProcess.Theta,
            clock);
        dxMap = new FdmHestonEquityPart(
            mesher, hestonProcess.RiskFreeRate, dividendYield, quantoHelper, leverageFct, clock, dividendSpread);
        tempX = new double[mesher.Layout.Size];
        tempY = new double[mesher.Layout.Size];
    }

    /// <inheritdoc/>
    public override int Size => 2;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="t2"/> is before <paramref name="t1"/>, or a curve can't be read.
    /// </exception>
    /// <exception cref="NanoQLException">A curve, the quanto helper's volatility or the leverage function can't be
    /// read.</exception>
    public override void SetTime(double t1, double t2)
    {
        dxMap.SetTime(t1, t2);
        dyMap.SetTime(t1, t2);
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        dyMap.Map.Apply(r, tempY);
        dxMap.Map.Apply(r, tempX);
        correlationMap.Apply(r, result);

        // dy + dx + L*correlation
        double[] l = dxMap.L;
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = tempY[i] + tempX[i] + (l[i] * result[i]);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">The direction is above 1, with QuantLib's message.</exception>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == 0)
        {
            dxMap.Map.Apply(r, result);
        }
        else if (direction == 1)
        {
            dyMap.Map.Apply(r, result);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
        }
    }

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result)
    {
        correlationMap.Apply(r, result);
        double[] l = dxMap.L;
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = l[i] * result[i];
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">The direction is above 1, with QuantLib's message.</exception>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == 0)
        {
            dxMap.Map.SolveSplitting(r, s, 1.0, result);
        }
        else if (direction == 1)
        {
            dyMap.Map.SolveSplitting(r, s, 1.0, result);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result)
    {
        // solve_splitting(1, solve_splitting(0, r, dt), dt)
        SolveSplitting(0, r, s, tempX);
        SolveSplitting(1, tempX, s, result);
    }

    /// <inheritdoc/>
    /// <remarks>QuantLib's decomposition: the equity part, the variance part and the correlation map without the
    /// leverage.</remarks>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() =>
        [dxMap.Map.ToMatrix(), dyMap.Map.ToMatrix(), correlationMap.ToMatrix()];

    /// <summary>
    /// The finite-difference Heston engine's clock: the times its meshers, this operator, the solver's maturity and the
    /// step conditions run on. Its day counter is the risk-free curve's and its reference date the earliest of the
    /// structures the engine reads, the process's curves, the quanto helper's and the leverage function, so the days
    /// before a later-dated structure's date are time on the grid, each structure accruing nothing before its own date
    /// (the S-hes plan's HR7 (ii), <see cref="FdmBlackScholesOp.Clock"/>'s form). Where no structure is dated before the
    /// risk-free curve, the clock is the risk-free curve itself, whose times are the process's.
    /// </summary>
    /// <param name="process">The Heston process.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <param name="leverageFct">The leverage function, or <see langword="null"/> for none.</param>
    /// <returns>A term structure whose reference date and day counter give the engine's times.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    public static TermStructure Clock(
        HestonProcess process, FdmQuantoHelper? quantoHelper = null, LocalVolTermStructure? leverageFct = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        YieldTermStructure rTS = process.RiskFreeRate;
        Date origin = process.EarliestReferenceDate();
        if (quantoHelper is not null)
        {
            foreach (TermStructure structure in (TermStructure[])[quantoHelper.RTS, quantoHelper.FTS, quantoHelper.FxVolTS])
            {
                if (structure.ReferenceDate < origin)
                {
                    origin = structure.ReferenceDate;
                }
            }
        }

        if (leverageFct is not null && leverageFct.ReferenceDate < origin)
        {
            origin = leverageFct.ReferenceDate;
        }

        return origin == rTS.ReferenceDate ? rTS : new FlatForward(origin, 0.0, rTS.DayCounter);
    }
}
