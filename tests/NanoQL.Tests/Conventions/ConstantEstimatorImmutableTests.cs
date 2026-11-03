// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <c>ConstantEstimator</c> is immutable: its calibration, empty in QuantLib, returns the estimator itself, unchanged,
/// so its series after a calibration is the one before.
/// </summary>
public class ConstantEstimatorImmutableTests
{
    [Fact]
    public void ACalibrationReturnsTheEstimatorUnchanged()
    {
        double[] values = [0.12, 0.31, 0.25, 0.18, 0.22, 0.40, 0.15, 0.27];
        var series = new TimeSeries<double>([.. Enumerable.Range(0, values.Length).Select(i => new Date(7, July, 1962) + i)], values);
        var estimator = new ConstantEstimator(3);
        TimeSeries<double> before = estimator.Calculate(series);
        VolatilityCompositor calibrated = estimator.Calibrate(series);
        Assert.Same(estimator, calibrated);
        Assert.Equal(before.Dates(), calibrated.Calculate(series).Dates());
        Assert.Equal(before.Values(), calibrated.Calculate(series).Values());
    }
}
