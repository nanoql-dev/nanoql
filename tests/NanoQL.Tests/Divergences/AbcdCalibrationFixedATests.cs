// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using static NanoQL.Tests.Divergences.AbcdTermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The abcd calibration's fixed a: QuantLib's transformation gives a as a positive a + d less d
/// (<c>abcdcalibration.cpp</c>'s <c>AbcdParametersTransformation</c>), so with a fixed and d free it fixes a + d, and
/// the fit moves a. On set 1 with a fixed at −0.06, QuantLib returns a = −0.11449872096226477 and d =
/// 0.22449872096226478, a + d held at 0.11 [the harness]. nanoql keeps a at −0.06, d above max(0, −a); the fit is
/// valid, at a minimum of the cost over b, c and d (each gradient within the end criteria's tolerance), and a restart
/// from a second guess reaches it too.
/// </summary>
public class AbcdCalibrationFixedATests
{
    [Fact]
    public void AFixedAKeepsItsGuess()
    {
        double[] vols = Sets[1];
        AbcdCalibration fit = new AbcdCalibration(T, vols, aIsFixed: true).Compute();
        AbcdCalibration restart = new AbcdCalibration(T, vols, -0.06, -0.05, 0.30, 0.15, aIsFixed: true).Compute();

        Assert.Equal(-0.06, fit.A);
        Assert.Null(Record.Exception(() => AbcdMathFunction.Validate(fit.A, fit.B, fit.C, fit.D)));
        Assert.All(Gradient(vols, fit, 1, 2, 3), g => Assert.True(Math.Abs(g) < GradientTolerance, $"gradient {g}"));
        Assert.Equal(fit.Error(), restart.Error(), 1e-9);
        Assert.Equal(fit.D, restart.D, 1e-5);
    }
}
