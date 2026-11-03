// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/hestonslvmcmodel.hpp ql/models/equity/hestonslvmcmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The Heston stochastic-local volatility model calibrated by Monte Carlo: at each time step the calibration paths'
/// spots are sorted into bins, and the leverage at each bin's midpoint is the local volatility over the root of the
/// bin's mean variance.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: the calibration runs at construction, where QuantLib's model calibrates at its first read; its draws are
/// kept per path and step in one array as QuantLib keeps them. The leverage function is
/// <see cref="FixedLocalVolSurface"/> on the calibrated values.
/// </para>
/// <para>
/// The calibration's paths read the leverage at each step's start, a node of the surface being calibrated, as QuantLib
/// 1.44 reads it there, beyond the time's strikes by the time's line (<see cref="FixedLocalVolWorkingSurface"/>), so the
/// calibration is QuantLib's bit for bit; the surface the model returns holds a strike beyond a time's strikes at the
/// nearest one, at a node as between nodes. The local volatility is read at each step's end, where QuantLib 1.44 reads
/// it at the step's start (QuantLib's #2860).
/// </para>
/// <para>
/// The model's times are a clock's, from the earliest reference date of the curves and the local volatility on the
/// risk-free curve's day counter, and each structure is read at the instants they denote: QuantLib's grid runs from the
/// risk-free curve's reference date and reads the dividend curve and the local volatility at its times. Where every
/// structure shares the risk-free curve's day counter and reference date, QuantLib's reads and bits.
/// </para>
/// </remarks>
public sealed class HestonSLVMCModel
{
    private readonly LocalVolTermStructure localVol;
    private readonly HestonModel hestonModel;
    private readonly FixedLocalVolSurface leverageFunction;

    /// <summary>Initializes a new instance of the <see cref="HestonSLVMCModel"/> class, calibrating it.</summary>
    /// <param name="localVol">The local volatility.</param>
    /// <param name="hestonModel">The Heston model.</param>
    /// <param name="brownianGeneratorFactory">The factory of the calibration's Brownian generator.</param>
    /// <param name="endDate">The final calibration date.</param>
    /// <param name="timeStepsPerYear">The time steps per year.</param>
    /// <param name="nBins">The number of bins, at least 2.</param>
    /// <param name="calibrationPaths">The number of calibration paths, at least the number of bins.</param>
    /// <param name="mandatoryDates">Dates the time grid holds, or <see langword="null"/> for none.</param>
    /// <param name="mixingFactor">The factor scaling the variance's volatility σ.</param>
    /// <exception cref="ArgumentNullException">A structure, the model or the factory is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="timeStepsPerYear"/> is negative, <paramref name="nBins"/> below 2, or
    /// <paramref name="calibrationPaths"/> below <paramref name="nBins"/>, where QuantLib divides by an empty bin.
    /// </exception>
    /// <exception cref="ArgumentException">The end date is the default date, or the time grid can't be built.</exception>
    /// <exception cref="NanoQLException">A structure can't be read.</exception>
    public HestonSLVMCModel(
        LocalVolTermStructure localVol,
        HestonModel hestonModel,
        BrownianGeneratorFactory brownianGeneratorFactory,
        Date endDate,
        int timeStepsPerYear = 365,
        int nBins = 201,
        int calibrationPaths = 1 << 15,
        IEnumerable<Date>? mandatoryDates = null,
        double mixingFactor = 1.0)
    {
        ArgumentNullException.ThrowIfNull(localVol);
        ArgumentNullException.ThrowIfNull(hestonModel);
        ArgumentNullException.ThrowIfNull(brownianGeneratorFactory);
        Date.ThrowIfDefault(endDate, nameof(endDate));
        ArgumentOutOfRangeException.ThrowIfNegative(timeStepsPerYear);
        ArgumentOutOfRangeException.ThrowIfLessThan(nBins, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(calibrationPaths, nBins);
        this.localVol = localVol;
        this.hestonModel = hestonModel;

        HestonProcess process = hestonModel.Process;
        TermStructure clock = FdmHestonOp.Clock(process, null, localVol);
        DayCounter dc = clock.DayCounter;
        Date refDate = clock.ReferenceDate;

        var gridTimes = new List<double>();
        foreach (Date mandatoryDate in mandatoryDates ?? [])
        {
            gridTimes.Add(dc.YearFraction(refDate, mandatoryDate));
        }

        gridTimes.Add(dc.YearFraction(refDate, endDate));

        var timeGrid = new TimeGrid(gridTimes.ToArray(), Math.Max(2, (int)(gridTimes[^1] * timeStepsPerYear)));

        leverageFunction = Calibrate(
            localVol, process, clock, brownianGeneratorFactory, timeGrid, nBins, calibrationPaths, mixingFactor);
    }

    /// <summary>Gets the Heston process.</summary>
    public HestonProcess HestonProcess => hestonModel.Process;

    /// <summary>Gets the local volatility.</summary>
    public LocalVolTermStructure LocalVol => localVol;

    /// <summary>Gets the calibrated leverage function.</summary>
    public LocalVolTermStructure LeverageFunction => leverageFunction;

    private static FixedLocalVolSurface Calibrate(
        LocalVolTermStructure localVol,
        HestonProcess process,
        TermStructure clock,
        BrownianGeneratorFactory brownianGeneratorFactory,
        TimeGrid timeGrid,
        int nBins,
        int calibrationPaths,
        double mixingFactor)
    {
        HestonProcess hestonProcess = process.OnClock(clock);
        LocalVolTermStructure localVolOnClock = localVol.SharesTimes(clock)
            ? localVol
            : new LocalVolTermStructureOnClock(localVol, clock);
        double spot = hestonProcess.S0;

        double v0 = hestonProcess.V0;
        DayCounter dc = clock.DayCounter;
        Date referenceDate = clock.ReferenceDate;

        double lv0 = localVolOnClock.LocalVol(0.0, spot) / Math.Sqrt(v0);

        var l = new Matrix(nBins, timeGrid.Size);

        double[][] vStrikes = new double[timeGrid.Size][];
        for (int i = 0; i < timeGrid.Size; ++i)
        {
            int u = nBins / 2;
            double dx = spot * Math.Sqrt(NumericLimits.QL_EPSILON);

            vStrikes[i] = new double[nBins];

            for (int j = 0; j < nBins; ++j)
            {
                vStrikes[i][j] = spot + ((j - u) * dx);
            }
        }

        for (int j = 0; j < nBins; ++j)
        {
            l[j, 0] = lv0;
        }

        var leverage = new FixedLocalVolWorkingSurface(referenceDate, [.. timeGrid], vStrikes, l, dc);

        var slvProcess = new HestonSLVProcess(hestonProcess, leverage, mixingFactor);

        double[] spots = new double[calibrationPaths];
        double[] variances = new double[calibrationPaths];
        Array.Fill(spots, spot);
        Array.Fill(variances, v0);

        int k = calibrationPaths / nBins;
        int m = calibrationPaths % nBins;

        int timeSteps = timeGrid.Size - 1;

        // QuantLib's boost::multi_array of the draws, path by path, step by step, factor by factor
        double[] paths = new double[checked((long)calibrationPaths * timeSteps * 2)];

        BrownianGenerator brownianGenerator = brownianGeneratorFactory.Create(2, timeSteps);

        Span<double> tmp = stackalloc double[2];
        for (int i = 0; i < calibrationPaths; ++i)
        {
            brownianGenerator.NextPath();
            for (int j = 0; j < timeSteps; ++j)
            {
                brownianGenerator.NextStep(tmp);
                long at = (((long)i * timeSteps) + j) * 2;
                paths[at] = tmp[0];
                paths[at + 1] = tmp[1];
            }
        }

        int[] order = new int[calibrationPaths];
        double[] sortedSpots = new double[calibrationPaths];
        double[] sortedVariances = new double[calibrationPaths];
        Span<double> x0 = stackalloc double[2];
        Span<double> x1 = stackalloc double[2];
        for (int n = 1; n < timeGrid.Size; ++n)
        {
            double t0 = timeGrid[n - 1];
            double t1 = timeGrid[n];
            double dt = timeGrid.Dt(n - 1);

            for (int i = 0; i < calibrationPaths; ++i)
            {
                x0[0] = spots[i];
                x0[1] = variances[i];

                long at = (((long)i * timeSteps) + (n - 1)) * 2;
                slvProcess.Evolve(t0, x0, dt, paths.AsSpan((int)at, 2), x1);

                spots[i] = x1[0];
                variances[i] = x1[1];
            }

            // std::sort of the (spot, variance) pairs: by spot, then by variance
            for (int i = 0; i < calibrationPaths; ++i)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int c = spots[a].CompareTo(spots[b]);
                return c != 0 ? c : variances[a].CompareTo(variances[b]);
            });

            for (int i = 0; i < calibrationPaths; ++i)
            {
                sortedSpots[i] = spots[order[i]];
                sortedVariances[i] = variances[order[i]];
            }

            (spots, sortedSpots) = (sortedSpots, spots);
            (variances, sortedVariances) = (sortedVariances, variances);

            double[] strikes = leverage.StrikesAt(n);
            int s = 0, e;
            for (int i = 0; i < nBins; ++i)
            {
                int inc = k + (i < m ? 1 : 0);
                e = s + inc;

                double sum = 0.0;
                for (int j = s; j < e; ++j)
                {
                    sum += variances[j];
                }

                sum /= inc;

                strikes[i] = 0.5 * (spots[e - 1] + spots[s]);

                // the conditional variance and leverage slice are both at the end of the step (QuantLib's #2860)
                double lvValue = localVolOnClock.LocalVol(t1, strikes[i], true);
                l[i, n] = Math.Sqrt(lvValue * lvValue / sum);

                s = e;
            }

            leverage.Reinterpolate(n);
        }

        return leverage.ToSurface();
    }
}
