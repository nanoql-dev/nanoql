// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdm2dblackscholesop.hpp ql/methods/finitedifferences/operators/fdm2dblackscholesop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The two-dimensional Black-Scholes operator: each asset's one-dimensional operator in the log of its price, and
/// the mixed term of their correlation.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the playbook's finite-difference row): mutable, owned by one rollback and not thread-safe. Its time
/// step rewrites its operators in place, so a step on Hundsdorfer's scheme, the engine's default, allocates nothing.
/// </para>
/// <para>
/// Its times run on the clock spanning both processes' structures (the internal clock across processes beside
/// <see cref="FdmBlackScholesOp.Clock(GeneralizedBlackScholesProcess, FdmQuantoHelper)"/>), each structure read at
/// the instants those times denote and accruing nothing before its own date, where QuantLib runs on the first
/// process's risk-free times and reads the second's structures at them; on one clock its arithmetic is QuantLib's
/// (<c>Divergences/Fd2dMismatchedTimesTests.cs</c>).
/// </para>
/// <para>
/// Each asset's operator discounts at its own risk-free rate, and the mixed term adds back the second's, so the price
/// discounts on the first process's curve, as every analytic two-asset engine does; QuantLib's mixed term adds back
/// the first's, so its price discounts at the second's rate (<c>Divergences/Fd2dTwoRiskFreeCurvesTests.cs</c>). On
/// one risk-free curve the two are the same.
/// </para>
/// </remarks>
[WorkingType]
public sealed class Fdm2dBlackScholesOp : FdmLinearOpComposite
{
    private const double NullReal = 3.4028234663852886e38;

    private readonly FdmMesher mesher;
    private readonly GeneralizedBlackScholesProcess p1;
    private readonly GeneralizedBlackScholesProcess p2;
    private readonly TermStructure clock;
    private readonly LocalVolTermStructure? localVol1;
    private readonly LocalVolTermStructure? localVol2;
    private readonly double[] x;
    private readonly double[] y;
    private readonly FdmBlackScholesOp opX;
    private readonly FdmBlackScholesOp opY;
    private readonly NinePointLinearOp corrMapT;
    private readonly NinePointLinearOp corrMapTemplate;
    private readonly double illegalLocalVolOverwrite;
    private double currentForwardRate;

    // the step's held arrays: the local volatilities' product and the apply's partial sum
    private readonly double[] volProduct;
    private readonly double[] partial;

    /// <summary>Initializes a new instance of the <see cref="Fdm2dBlackScholesOp"/> class.</summary>
    /// <param name="mesher">The two-dimensional mesher, the first asset's log price in direction 0.</param>
    /// <param name="p1">The first asset's process.</param>
    /// <param name="p2">The second asset's process.</param>
    /// <param name="correlation">The assets' correlation.</param>
    /// <param name="maturity">The maturity, unused, as in QuantLib.</param>
    /// <param name="localVol">Whether to use the processes' local volatilities at each point.</param>
    /// <param name="illegalLocalVolOverwrite">
    /// The volatility to use where a local volatility can't be computed; <see langword="null"/> (QuantLib's
    /// <c>-Null&lt;Real&gt;()</c>) or a negative value to let the failure through.
    /// </param>
    /// <exception cref="ArgumentNullException">The mesher or a process is <see langword="null"/>.</exception>
    public Fdm2dBlackScholesOp(
        FdmMesher mesher,
        GeneralizedBlackScholesProcess p1,
        GeneralizedBlackScholesProcess p2,
        double correlation,
        double maturity,
        bool localVol = false,
        double? illegalLocalVolOverwrite = null)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(p1);
        ArgumentNullException.ThrowIfNull(p2);
        _ = maturity;
        this.mesher = mesher;
        this.p1 = p1;
        this.p2 = p2;
        clock = FdmBlackScholesOp.ClockAcross([p1, p2]);
        localVol1 = localVol ? p1.LocalVolatility : null;
        localVol2 = localVol ? p2.LocalVolatility : null;
        x = localVol ? [.. ArrayFunctions.Exp(mesher.Locations(0)).AsSpan()] : [];
        y = localVol ? [.. ArrayFunctions.Exp(mesher.Locations(1)).AsSpan()] : [];
        opX = new FdmBlackScholesOp(mesher, p1, p1.X0, localVol, illegalLocalVolOverwrite, 0, null, clock);
        opY = new FdmBlackScholesOp(mesher, p2, p2.X0, localVol, illegalLocalVolOverwrite, 1, null, clock);
        corrMapT = new NinePointLinearOp(0, 1, mesher);
        corrMapTemplate = new SecondOrderMixedDerivativeOp(0, 1, mesher)
            .Mult(new Vector(mesher.Layout.Size, correlation));
        this.illegalLocalVolOverwrite = illegalLocalVolOverwrite ?? -NullReal;

        int n = mesher.Layout.Size;
        volProduct = new double[localVol ? n : 0];
        partial = new double[n];
    }

    /// <summary>Gets the number of directions, 2.</summary>
    public override int Size => 2;

    /// <summary>
    /// Sets the operators for a time step: each asset's operator, the mixed term at the step's forward volatilities
    /// (or the local volatilities at its midpoint), and the second risk-free curve's forward rate the mixed term adds
    /// back.
    /// </summary>
    /// <param name="t1">The step's start.</param>
    /// <param name="t2">The step's end.</param>
    /// <exception cref="NanoQLException">A structure can't be read at the step's times.</exception>
    public override void SetTime(double t1, double t2)
    {
        opX.SetTime(t1, t2);
        opY.SetTime(t1, t2);

        if (localVol1 is not null)
        {
            // each local volatility at the step's midpoint on its own clock, scaled to the clock's time by its
            // accrual over the step (FdmBlackScholesOp's local-volatility read); QuantLib's where they share times
            double tMid = 0.5 * (t1 + t2);
            bool shares1 = localVol1.SharesTimes(clock);
            bool shares2 = localVol2!.SharesTimes(clock);
            double lvTime1 = shares1 ? tMid : localVol1.AccrualTime(clock, tMid);
            double lvTime2 = shares2 ? tMid : localVol2.AccrualTime(clock, tMid);
            double scale1 = shares1 ? 1.0 : Math.Sqrt(localVol1.AccruedPerClockTime(clock, t1, t2));
            double scale2 = shares2 ? 1.0 : Math.Sqrt(localVol2.AccruedPerClockTime(clock, t1, t2));

            int n = mesher.Layout.Size;
            for (int i = 0; i < n; ++i)
            {
                double vol1, vol2;
                if (illegalLocalVolOverwrite < 0.0)
                {
                    vol1 = localVol1.LocalVol(lvTime1, x[i], true);
                    vol2 = localVol2.LocalVol(lvTime2, y[i], true);
                }
                else
                {
                    try
                    {
                        vol1 = localVol1.LocalVol(lvTime1, x[i], true);
                    }
                    catch (Exception e) when (e is NanoQLException or ArgumentException)
                    {
                        vol1 = illegalLocalVolOverwrite;
                    }

                    try
                    {
                        vol2 = localVol2.LocalVol(lvTime2, y[i], true);
                    }
                    catch (Exception e) when (e is NanoQLException or ArgumentException)
                    {
                        vol2 = illegalLocalVolOverwrite;
                    }
                }

                volProduct[i] = shares1 && shares2 ? vol1 * vol2 : vol1 * scale1 * (vol2 * scale2);
            }

            corrMapT.SetMult(corrMapTemplate, volProduct);
        }
        else
        {
            double vol1 = ForwardVol(p1.BlackVolatility, t1, t2, p1.X0);
            double vol2 = ForwardVol(p2.BlackVolatility, t1, t2, p2.X0);
            corrMapT.SetMult(corrMapTemplate, vol1 * vol2);
        }

        currentForwardRate = p2.RiskFreeRate.ContinuousForwardRateAt(clock, t1, t2);
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        // QuantLib's opX_.apply(x) + opY_.apply(x) + apply_mixed(x)
        Span<double> b = partial.AsSpan(0, r.Length);
        opX.Apply(r, result);
        opY.Apply(r, b);
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = result[i] + b[i];
        }

        corrMapT.Apply(r, b);
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = result[i] + (b[i] + (currentForwardRate * r[i]));
        }
    }

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result)
    {
        corrMapT.Apply(r, result);
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = result[i] + (currentForwardRate * r[i]);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The direction is neither 0 nor 1.</exception>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == 0)
        {
            opX.Apply(r, result);
        }
        else if (direction == 1)
        {
            opY.Apply(r, result);
        }
        else
        {
            throw new NanoQLException("direction is too large");
        }
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The direction is neither 0 nor 1.</exception>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == 0)
        {
            opX.SolveSplitting(direction, r, s, result);
        }
        else if (direction == 1)
        {
            opY.SolveSplitting(direction, r, s, result);
        }
        else
        {
            throw new NanoQLException("direction is too large");
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) =>
        SolveSplitting(0, r, s, result);

    /// <inheritdoc/>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp()
    {
        int n = mesher.Layout.Size;
        var identity = new SparseMatrix(n, n, n);
        for (int i = 0; i < n; ++i)
        {
            identity[i, i] = currentForwardRate * 1.0;
        }

        return [opX.ToMatrix(), opY.ToMatrix(), corrMapT.ToMatrix() + identity];
    }

    // A volatility per unit of the clock's time over [t1, t2]: QuantLib's blackForwardVol where the structure shares
    // the clock's times, otherwise the forward variance accrued between the instants they denote over the step.
    private double ForwardVol(BlackVolTermStructure volTS, double t1, double t2, double strike) =>
        volTS.SharesTimes(clock)
            ? volTS.BlackForwardVol(t1, t2, strike)
            : Math.Sqrt(volTS.BlackForwardVarianceAt(clock, t1, t2, strike) / (t2 - t1));
}
