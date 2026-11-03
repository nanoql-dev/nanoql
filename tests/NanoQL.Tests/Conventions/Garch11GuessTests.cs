// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Divergences;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <c>Garch11</c>'s calibrations from an initial guess read its ω, α and β, as QuantLib's cost function reads them past
/// a shorter guess's end, and the core form reads the optimum, of the guess's length, when a method returns without
/// evaluating the cost: a guess of fewer than three values is refused by every form, before the minimization.
/// </summary>
public class Garch11GuessTests
{
    [Fact]
    public void AGuessOfFewerThanThreeValuesIsRefused()
    {
        var method = new Simplex(0.001);
        var criteria = new EndCriteria(10000, 500, 1e-8, 1e-8, 1e-8);
        var constraint = new NoConstraint();
        var garch = new Garch11(GarchSeries.Returns, Garch11Mode.GammaGuess);
        double[] r2 = [1e-4, 4e-4, 2.5e-5, 9e-4];
        foreach (int n in new[] { 0, 2 })
        {
            double[] guess = [.. Enumerable.Repeat(0.1, n)];
            foreach (Action call in new Action[]
                     {
                         () => garch.Calibrate(GarchSeries.Returns, method, criteria, guess),
                         () => Garch11.CalibrateR2(r2, 2e-4, method, criteria, guess),
                         () => Garch11.CalibrateR2(r2, method, criteria, guess),
                         () => Garch11.CalibrateR2(r2, 2e-4, method, constraint, criteria, guess),
                         () => Garch11.CalibrateR2(r2, method, constraint, criteria, guess),
                         () => Garch11.CalibrateR2([], method, constraint, criteria, guess),
                     })
            {
                ArgumentException e = Assert.Throws<ArgumentException>(call);
                Assert.Equal("initialGuess", e.ParamName);
                Assert.StartsWith($"{n} initial values given for ω, α and β", e.Message, StringComparison.Ordinal);
            }
        }

        Garch11 calibrated = garch.Calibrate(GarchSeries.Returns, method, criteria, [garch.Omega, garch.Alpha, garch.Beta]);
        Assert.True(calibrated.Alpha + calibrated.Beta < 1.0);
    }
}
