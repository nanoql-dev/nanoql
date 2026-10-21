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
/// The process's curves and volatilities, and the quanto helper's, must share the risk-free curve's day counter and
/// reference date, or the operator refuses them, "all term structures must share one day counter" and "... one
/// reference date". The finite-difference engine's times are the risk-free curve's, measured from its reference date in
/// its day counter (<c>blackscholesprocess.cpp:149-152</c>), and QuantLib reads the dividend curve, the volatility and
/// the quanto helper's structures at those times (<c>fdmblackscholesop.cpp:51-66</c>), each of which measures time from
/// its own reference date in its own day counter. A put on a risk-free curve on Act/360 prices 0.93% above the analytic
/// engine's value, one on a curve settling two days after its volatility's date 0.74% below, where the same structures
/// dated alike on one day counter price within the grid's error. A dividend curve that is a flat forward at zero, which
/// QuantLib's <c>BlackScholesProcess</c> builds on Act/365 and the evaluation date whatever its other structures,
/// discounts by 1 at any time and is taken however it is dated. A documented divergence.
/// </para>
/// </remarks>
public sealed class FdmBlackScholesOp : FdmLinearOpComposite
{
    private const double NullReal = 3.4028234663852886e38;

    private readonly FdmMesher mesher;
    private readonly YieldTermStructure rTS;
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
    /// <exception cref="ArgumentException">
    /// The process's curves and volatilities, or the quanto helper's, don't share the risk-free curve's day counter and
    /// reference date.
    /// </exception>
    public FdmBlackScholesOp(
        FdmMesher mesher,
        GeneralizedBlackScholesProcess bsProcess,
        double strike,
        bool localVol = false,
        double? illegalLocalVolOverwrite = null,
        int direction = 0,
        FdmQuantoHelper? quantoHelper = null)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(bsProcess);
        CheckSharedTimes(bsProcess, localVol, quantoHelper, nameof(bsProcess));
        this.mesher = mesher;
        rTS = bsProcess.RiskFreeRate;
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
        double r = rTS.ContinuousForwardRate(t1, t2);
        double q = qTS.ContinuousForwardRate(t1, t2);

        if (localVol is not null)
        {
            // QuantLib walks the layout for each point's index, in storage order
            int n = mesher.Layout.Size;
            for (int i = 0; i < n; ++i)
            {
                if (illegalLocalVolOverwrite < 0.0)
                {
                    double lv = localVol.LocalVol(0.5 * (t1 + t2), x[i], true);
                    localVariance[i] = lv * lv;
                }
                else
                {
                    try
                    {
                        double lv = localVol.LocalVol(0.5 * (t1 + t2), x[i], true);
                        localVariance[i] = lv * lv;
                    }
                    catch (Exception e) when (e is NanoQLException or ArgumentException)
                    {
                        localVariance[i] = illegalLocalVolOverwrite * illegalLocalVolOverwrite;
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

                quantoHelper.QuantoAdjustment(adjustment, t1, t2, adjustment);
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
            double v = volTS.BlackForwardVariance(t1, t2, strike) / (t2 - t1);

            double s;
            if (quantoHelper is not null)
            {
                double sqrtV = Math.Sqrt(v);
                double adj = 0.0;
                quantoHelper.QuantoAdjustment(new ReadOnlySpan<double>(in sqrtV), t1, t2, new Span<double>(ref adj));
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
    /// Refuses a process whose dividend curve (unless a flat forward at zero), Black volatility or, where it is read,
    /// local volatility, or a quanto helper whose curves or exchange-rate volatility, isn't on the process's risk-free
    /// day counter and reference date, at whose times the finite-difference engine reads them all.
    /// </summary>
    /// <param name="process">The process.</param>
    /// <param name="localVol">Whether the process's local volatility is read.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/>.</param>
    /// <param name="paramName">The process's parameter name.</param>
    /// <exception cref="ArgumentException">A structure is on another day counter, or dated otherwise.</exception>
    internal static void CheckSharedTimes(
        GeneralizedBlackScholesProcess process, bool localVol, FdmQuantoHelper? quantoHelper, string paramName)
    {
        TermStructure riskFree = process.RiskFreeRate;

        // a flat dividend curve at zero, as QuantLib's BlackScholesProcess builds on Act/365 and the evaluation date,
        // discounts by 1 at any time, so no day counter or reference date misreads it
        bool timeless = process.DividendYield is FlatForward { Forward: 0.0 };
        TermStructure?[] read =
        [
            timeless ? null : process.DividendYield,
            process.BlackVolatility,
            localVol ? process.LocalVolatility : null,
            quantoHelper?.RTS,
            quantoHelper?.FTS,
            quantoHelper?.FxVolTS,
        ];
        foreach (TermStructure? structure in read)
        {
            if (structure is not null && structure.DayCounter != riskFree.DayCounter)
            {
                throw new ArgumentException("all term structures must share one day counter", paramName);
            }
        }

        foreach (TermStructure? structure in read)
        {
            if (structure is not null && structure.ReferenceDate != riskFree.ReferenceDate)
            {
                throw new ArgumentException("all term structures must share one reference date", paramName);
            }
        }
    }
}
