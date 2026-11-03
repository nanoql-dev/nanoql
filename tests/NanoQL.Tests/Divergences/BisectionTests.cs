// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The bisection's exit on a midpoint whose value is close to 0: QuantLib returns its running root, the bracket's
/// low side, where the midpoint's value is above 0, so x − 1.5 + 1e-30 on [0, 3] returns 0; nanoql returns the
/// midpoint, 1.5. Where the midpoint's value is at or below 0 the two are QuantLib's own exit. QuantLib's value is
/// its own.
/// </summary>
public class BisectionTests
{
    [Fact]
    public void AMidpointCloseToZeroAboveItIsTheRoot()
    {
        int calls = 0;
        double last = double.NaN;
        double root = new Bisection().Solve(
            x =>
            {
                ++calls;
                last = x;
                return x - 1.5 + 1e-30;
            },
            1e-8,
            1.0,
            0.0,
            3.0);

        // QuantLib 0
        Assert.Equal(1.5, root);

        // the function's last call is at the root returned, as QuantLib's exit makes it
        Assert.Equal(root, last);
        Assert.Equal(4, calls);
    }

    [Fact]
    public void AMidpointAtOrBelowZeroIsQuantLibsExit()
    {
        // x − 1.5 on [0, 3]: the midpoint's value is exactly 0, QuantLib's own root
        Assert.Equal(1.5, new Bisection().Solve(x => x - 1.5, 1e-8, 1.0, 0.0, 3.0));
    }
}
