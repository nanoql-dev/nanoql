// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Divergences;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <c>Garch11</c> is immutable (the S-bar plan's BR3 (ii)): a calibration returns a new model with the receiver's mode
/// and leaves the receiver unchanged, where QuantLib's <c>calibrate</c> changes the object; every answer is a fresh
/// object's, so two calibrations of one series agree.
/// </summary>
public class Garch11ImmutableTests
{
    [Fact]
    public void ACalibrationLeavesTheReceiverUnchanged()
    {
        var garch = new Garch11(0.2, 0.3, 0.4);
        Garch11 calibrated = garch.Calibrate(GarchSeries.Returns);
        Assert.NotSame(garch, calibrated);
        Assert.Equal(0.2, garch.Alpha);
        Assert.Equal(0.3, garch.Beta);
        Assert.Equal(0.4, garch.LtVol);
        Assert.Equal(0.0, garch.LogLikelihood);
        Assert.Equal(Garch11Mode.BestOfTwo, calibrated.Mode);
        Assert.Equal(0.207592, calibrated.Alpha, 1e-6);
    }

    [Fact]
    public void EveryCalibrationIsAFreshObject()
    {
        var garch = new Garch11(GarchSeries.Returns, Garch11Mode.GammaGuess);
        Garch11 first = garch.Calibrate(GarchSeries.Returns, new Simplex(0.001), new EndCriteria(10000, 500, 1e-8, 1e-8, 1e-8));
        Garch11 second = garch.Calibrate(GarchSeries.Returns, new Simplex(0.001), new EndCriteria(10000, 500, 1e-8, 1e-8, 1e-8));
        Assert.NotSame(first, second);
        Assert.Equal(Garch11Mode.GammaGuess, first.Mode);
        Assert.Equal(first.Alpha, second.Alpha);
        Assert.Equal(first.Beta, second.Beta);
        Assert.Equal(first.Omega, second.Omega);
        Assert.Equal(first.LogLikelihood, second.LogLikelihood);
        Assert.Equal(garch.Alpha, new Garch11(GarchSeries.Returns, Garch11Mode.GammaGuess).Alpha);
    }
}
