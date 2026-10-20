// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An ordinary differential equation whose function returns a vector of another length than its argument's.
/// QuantLib's <c>AdaptiveRungeKutta</c> reads the function's value at every index of the solution vector
/// (<c>adaptiverungekutta.hpp:111-113, 219-247</c>), past the end of a shorter one, and ignores the extra values of a
/// longer one; the wheel's <c>RungeKutta</c> refuses both with its own wrapper's check, "Python function did not return
/// a list of correct size" (<c>ode.i:79-81</c>), which QuantLib doesn't make. nanoql refuses both with
/// <see cref="NanoQLException"/>, "the ODE function returned n values for m".
/// </summary>
public class AdaptiveRungeKuttaSizeTests
{
    [Fact]
    public void AFunctionOfAnotherLengthIsRefused()
    {
        var rk = new RungeKutta(1e-8, 1e-5, 0.0);

        NanoQLException shorter = Assert.Throws<NanoQLException>(
            () => rk.Evaluate((x, y) => [y[1]], [0.0, 1.0], 0.0, 1.0));
        Assert.Equal("the ODE function returned 1 values for 2", shorter.Message);
        NanoQLException longer = Assert.Throws<NanoQLException>(
            () => rk.Evaluate((x, y) => [y[1], -y[0], 0.0], [0.0, 1.0], 0.0, 1.0));
        Assert.Equal("the ODE function returned 3 values for 2", longer.Message);
        NanoQLException none = Assert.Throws<NanoQLException>(
            () => rk.Evaluate((x, y) => default(ImmutableArray<double>), [0.0, 1.0], 0.0, 1.0));
        Assert.Equal("the ODE function returned 0 values for 2", none.Message);

        // of the right length, QuantLib's solution
        Assert.Equal((double[])[0.8414709848080549, 0.54030230586824], new RungeKutta(1e-12, 1e-4, 0.0).Evaluate((x, y) => [y[1], -y[0]], [0.0, 1.0], 0.0, 1.0));
    }
}
