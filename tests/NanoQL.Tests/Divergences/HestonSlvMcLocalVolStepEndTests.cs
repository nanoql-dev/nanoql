// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo SLV calibration reads the local volatility at each step's end, where QuantLib 1.44 reads it at the
/// step's start (<c>hestonslvmcmodel.cpp:181</c>), so a time-dependent local volatility's leverage lags it by a step;
/// QuantLib's master takes the step's end (#2860, the S-hes plan's HR8 (i); its case is
/// <see cref="TestSuite.HestonSlvModelTests.TestMonteCarloCalibrationTimeDependentLocalVol"/>). On that case's market,
/// at the grid's third time, QuantLib 1.44's leverage is the second time's local volatility over √v₀ (QuantLib-SWIG
/// 1.44), nanoql's the third's.
/// </summary>
public class HestonSlvMcLocalVolStepEndTests
{
    [Fact]
    public void TheLeverageFollowsTheLocalVolatilityAtTheStepsEnd()
    {
        var todaysDate = new Date(5, January, 2016);
        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var localVolMatrix = new Matrix(2, 3);
        double[] times = [0.0, 0.5, 1.0];
        for (int i = 0; i < 2; ++i)
        {
            for (int j = 0; j < 3; ++j)
            {
                localVolMatrix[i, j] = 0.2 + (0.2 * times[j]);
            }
        }

        var localVol = new FixedLocalVolSurface(todaysDate, times, [50.0, 150.0], localVolMatrix, dc);
        var process = new HestonProcess(
            new FlatForward(todaysDate, 0.05, dc), new FlatForward(todaysDate, 0.02, dc), 100.0, 0.04, 1.0, 0.04, 1e-6, 0.0);
        LocalVolTermStructure leverage = new HestonSLVMCModel(
            localVol, new HestonModel(process), new MTBrownianGeneratorFactory(1234), todaysDate + new Period(1, TimeUnit.Years), 4, 16, 2048,
            [todaysDate + 73]).LeverageFunction;

        const double t = 0.46631234872869726;
        double calculated = leverage.LocalVol(t, 100.0, true);

        // QuantLib 1.44: 1.1994537216713541, the local volatility at the grid's second time, 0.19945..., over 0.2
        Assert.Equal(localVol.LocalVol(t, 100.0, true) / 0.2, calculated, 1e-4);
        Assert.True(calculated - 1.1994537216713541 > 0.25, $"{calculated}");
    }
}
