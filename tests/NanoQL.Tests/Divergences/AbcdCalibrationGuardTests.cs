// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using static NanoQL.Tests.Divergences.AbcdTermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The abcd calibration's guard (the S-g1d plan's G1R6 (xxv)): QuantLib's transformation leaves b free, so its
/// optimizer can step to a trial point the abcd function's validation refuses, and the fit throws. On set 6, a dip
/// whose total variance grows, QuantLib refuses with "b (-0.374029) less than -0.267779: negative function value at
/// stationary point 2.45429" [the harness]; nanoql's guard fits it validly, at a minimum (each parameter's gradient
/// within the end criteria's tolerance) that a restart from a second guess reaches too. And on the sets QuantLib
/// completes, 0, 1, 4 and 5, the guard never acts: their fits are QuantLib's bit for bit, set 1's among them although
/// its gradient is above the tolerance.
/// </summary>
public class AbcdCalibrationGuardTests
{
    [Fact]
    public void ADipQuantLibRefusesIsFittedAtAMinimum()
    {
        double[] vols = Sets[6];
        AbcdCalibration fit = new AbcdCalibration(T, vols).Compute();
        AbcdCalibration restart = new AbcdCalibration(T, vols, 0.05, -0.05, 0.30, 0.15).Compute();

        Assert.Null(Record.Exception(() => AbcdMathFunction.Validate(fit.A, fit.B, fit.C, fit.D)));
        Assert.All(Gradient(vols, fit, 0, 1, 2, 3), g => Assert.True(Math.Abs(g) < GradientTolerance, $"gradient {g}"));
        Assert.Equal(fit.Error(), restart.Error(), 1e-9);
        Assert.Equal(fit.B, restart.B, 1e-4);
        Assert.Equal(fit.C, restart.C, 1e-4);
    }

    [Theory]
    [InlineData(0, 0.1387858671364557, 0.3667470041279092, 1.8583081433972803, 0.15864883121849888)]
    [InlineData(1, -0.15352166122464672, -6.272607700069665e-06, 0.7122139487679388, 0.22812353043922487)]
    [InlineData(4, 0.16748015547747552, -0.49205287090967603, 1.4429114037656492, 0.2519239300445927)]
    [InlineData(5, 0.45336334464724903, -1.4254877636528123, 1.5935876342523034, 0.26597942707915356)]
    public void TheFitsQuantLibCompletesKeepItsBits(int set, double a, double b, double c, double d)
    {
        AbcdCalibration fit = new AbcdCalibration(T, Sets[set]).Compute();

        Assert.Equal(new[] { a, b, c, d }, new[] { fit.A, fit.B, fit.C, fit.D });
        if (set == 1)
        {
            Assert.Contains(Gradient(Sets[1], fit, 0, 1, 2, 3), g => Math.Abs(g) > GradientTolerance);
        }
    }
}
