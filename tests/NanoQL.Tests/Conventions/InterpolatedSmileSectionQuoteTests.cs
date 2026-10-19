// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// An interpolated smile section's quotes are their values (playbook §2): QuantLib's quote form, whose section observes
/// its quotes and recalculates when one changes (<c>testHandlesUpdatePropagates</c>), is the value form, and a changed
/// quote is a new section built from the new value, which answers it. The section built first is unchanged.
/// </summary>
public class InterpolatedSmileSectionQuoteTests
{
    [Fact]
    public void ASectionBuiltFromTheNewValueAnswersIt()
    {
        double expiry = 0.25;
        double sqrtT = Math.Sqrt(expiry);
        double[] strikes = [80.0, 90.0, 100.0];
        double[] stdDevs = [0.20 * sqrtT, 0.15 * sqrtT, 0.18 * sqrtT];

        var before = new InterpolatedSmileSection<Linear>(expiry, strikes, stdDevs, 95.0);
        double expectedBefore = Linear(95.0, 90.0, stdDevs[1] / sqrtT, 100.0, stdDevs[2] / sqrtT);
        Assert.Equal(expectedBefore, before.Volatility(95.0), 1e-14);

        // QuantLib's q1->setValue(0.20 * sqrtT): a section built from the new value.
        stdDevs[1] = 0.20 * sqrtT;
        var after = new InterpolatedSmileSection<Linear>(expiry, strikes, stdDevs, 95.0);
        double expectedAfter = Linear(95.0, 90.0, stdDevs[1] / sqrtT, 100.0, stdDevs[2] / sqrtT);
        Assert.Equal(expectedAfter, after.Volatility(95.0), 1e-14);

        // The first section copied its inputs.
        Assert.Equal(expectedBefore, before.Volatility(95.0), 1e-14);
    }

    private static double Linear(double x, double x1, double y1, double x2, double y2) =>
        y1 + ((y2 - y1) * ((x - x1) / (x2 - x1)));
}
