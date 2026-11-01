// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2008, 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmhestonhullwhiteop.hpp ql/methods/finitedifferences/operators/fdmhestonhullwhiteop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The Heston-Hull-White linear operator: the Heston equity and variance directions, a Hull-White short rate, the
/// equity-variance correlation and the equity-rate correlation.
/// </summary>
/// <remarks>
/// <para>
/// A working type (master §2.2's finite-difference row): its time-dependent maps are set in place by
/// <see cref="SetTime"/>, and a step on the default scheme allocates nothing (the S-hes plan's HD8).
/// </para>
/// <para>
/// Its times run on the Heston process's model clock (<see cref="FdmHestonOp.Clock"/>), from the earliest curve date
/// on the risk-free curve's day counter, the dividend curve read at the instants they denote and the Hull-White model
/// on the risk-free curve restated on the clock; QuantLib's operator reads the dividend curve at the risk-free curve's
/// times (<c>fdmhestonhullwhiteop.cpp:62</c>; HR7 (xix)), and its reads stand where the curves share the clock.
/// </para>
/// </remarks>
[WorkingType]
public sealed class FdmHestonHullWhiteOp : FdmLinearOpComposite
{
    private readonly NinePointLinearOp hestonCorrMap;
    private readonly NinePointLinearOp equityIrCorrMap;
    private readonly TripleBandLinearOp dyMap;
    private readonly FdmHestonHullWhiteEquityPart dxMap;
    private readonly FdmHullWhiteOp hullWhiteOp;

    // apply's terms, held
    private readonly double[] tempDy;
    private readonly double[] tempDx;
    private readonly double[] tempHw;
    private readonly double[] tempCorr;

    /// <summary>Initializes a new instance of the <see cref="FdmHestonHullWhiteOp"/> class.</summary>
    /// <param name="mesher">The three-dimensional mesher: the log spot, the variance, the short rate's state.</param>
    /// <param name="hestonProcess">The Heston process.</param>
    /// <param name="hwProcess">The Hull-White process, whose reversion and volatility are read.</param>
    /// <param name="equityShortRateCorrelation">The equity-rate correlation.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The correlations' squares sum past 1 ("correlation matrix has negative
    /// eigenvalues").</exception>
    public FdmHestonHullWhiteOp(
        FdmMesher mesher, HestonProcess hestonProcess, HullWhiteProcess hwProcess, double equityShortRateCorrelation)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(hestonProcess);
        ArgumentNullException.ThrowIfNull(hwProcess);
        double rho = hestonProcess.Rho;
        double sigma = hestonProcess.Sigma;
        double kappa = hestonProcess.Kappa;
        double theta = hestonProcess.Theta;

        TermStructure clock = FdmHestonOp.Clock(hestonProcess);
        YieldTermStructure riskFree = clock.SharesTimes(hestonProcess.RiskFreeRate)
            ? hestonProcess.RiskFreeRate
            : new YieldTermStructureOnClock(hestonProcess.RiskFreeRate, clock);
        var hwModel = new HullWhite(riskFree, hwProcess.A, hwProcess.Sigma);

        Vector locations1 = mesher.Locations(1);
        hestonCorrMap = new SecondOrderMixedDerivativeOp(0, 1, mesher).Mult(rho * sigma * locations1);
        var volatility = new Vector(locations1.Size);
        for (int i = 0; i < volatility.Size; ++i)
        {
            volatility[i] = Math.Sqrt(locations1[i]) * hwProcess.Sigma * equityShortRateCorrelation;
        }

        equityIrCorrMap = new SecondOrderMixedDerivativeOp(0, 2, mesher).Mult(volatility);
        var drift = new Vector(locations1.Size);
        for (int i = 0; i < drift.Size; ++i)
        {
            drift[i] = kappa * (theta - locations1[i]);
        }

        dyMap = new SecondDerivativeOp(1, mesher)
            .Mult(0.5 * sigma * sigma * locations1)
            .Add(new FirstDerivativeOp(1, mesher).Mult(drift));
        dxMap = new FdmHestonHullWhiteEquityPart(mesher, hwModel, hestonProcess.DividendYield, clock);
        hullWhiteOp = new FdmHullWhiteOp(mesher, hwModel, 2);

        if (!((equityShortRateCorrelation * equityShortRateCorrelation) + (hestonProcess.Rho * hestonProcess.Rho) <= 1.0))
        {
            throw new ArgumentException("correlation matrix has negative eigenvalues", nameof(equityShortRateCorrelation));
        }

        int n = mesher.Layout.Size;
        tempDy = new double[n];
        tempDx = new double[n];
        tempHw = new double[n];
        tempCorr = new double[n];
    }

    /// <inheritdoc/>
    public override int Size => 3;

    /// <inheritdoc/>
    public override void SetTime(double t1, double t2)
    {
        dxMap.SetTime(t1, t2);
        hullWhiteOp.SetTime(t1, t2);
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        dyMap.Apply(r, tempDy);
        dxMap.Map.Apply(r, tempDx);
        hullWhiteOp.Apply(r, tempHw);
        hestonCorrMap.Apply(r, tempCorr);
        equityIrCorrMap.Apply(r, result);
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = tempDy[i] + tempDx[i] + tempHw[i] + tempCorr[i] + result[i];
        }
    }

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result)
    {
        hestonCorrMap.Apply(r, tempCorr);
        equityIrCorrMap.Apply(r, result);
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = tempCorr[i] + result[i];
        }
    }

    /// <inheritdoc/>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == 0)
        {
            dxMap.Map.Apply(r, result);
        }
        else if (direction == 1)
        {
            dyMap.Apply(r, result);
        }
        else if (direction == 2)
        {
            hullWhiteOp.Apply(r, result);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
        }
    }

    /// <inheritdoc/>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == 0)
        {
            dxMap.Map.SolveSplitting(r, s, 1.0, result);
        }
        else if (direction == 1)
        {
            dyMap.SolveSplitting(r, s, 1.0, result);
        }
        else if (direction == 2)
        {
            hullWhiteOp.SolveSplitting(2, r, s, result);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) => SolveSplitting(0, r, s, result);

    /// <inheritdoc/>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() =>
    [
        dxMap.Map.ToMatrix(),
        dyMap.ToMatrix(),
        hullWhiteOp.ToMatrixDecomp()[0],
        hestonCorrMap.ToMatrix() + equityIrCorrMap.ToMatrix(),
    ];
}
