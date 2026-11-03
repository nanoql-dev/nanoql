// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The square-root volatility process's number of steps (fixed). QuantLib's <c>SquareRootAndersen::numberSteps</c> is
/// <c>dt_.size()*numberSubSteps_</c> (<c>squarerootandersen.cpp:80</c>), where <c>dt_</c> already holds every sub-step:
/// 48 for 3 evolution times of 4 sub-steps, though <c>variatesPerStep</c> counts per evolution step. nanoql's is the
/// evolution steps a path takes. Checks: the <c>nextstep</c> calls a path takes, as QuantLib's own sizing of its path
/// (<c>vPath_</c>, one value per sub-step and the start) allows, the next refused.
/// </summary>
public class SquareRootAndersenStepsTests
{
    [Fact]
    public void ThePathTakesOneStepPerEvolutionTime()
    {
        var process = new SquareRootAndersen(0.04, 1.0, 0.5, 0.04, [0.5, 1.0, 1.5], 4, 0.5, 0.5);
        Assert.Equal(3, process.NumberSteps);
        Assert.Equal(4, process.VariatesPerStep);
        Assert.NotEqual(48, process.NumberSteps);

        double[] z = [0.1, -0.2, 0.3, -0.4];
        process.NextPath();
        for (int s = 0; s < process.NumberSteps; ++s)
        {
            process.Nextstep(z);
        }

        var past = Assert.Throws<NanoQLException>(() => process.Nextstep(z));
        Assert.Equal("the path's steps are taken", past.Message);
    }
}
