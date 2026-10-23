// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A forward-measure process is immutable (the S-sr plan's SSD6): QuantLib's <c>setForwardMeasureTime</c>, which changes
/// the process, is <c>WithForwardMeasureTime</c>, which returns a copy of the process's own type at the new time and
/// leaves the receiver as it was, its drift and expectation those of its own time, or refused where it has none.
/// </summary>
public class ForwardMeasureProcessTests
{
    [Fact]
    public void WithForwardMeasureTimeReturnsACopyAndLeavesTheReceiver()
    {
        var today = new Date(19, Month.May, 2026);
        var curve = new FlatForward(today, 0.03, new Actual365Fixed());

        var hullWhite = new HullWhiteForwardProcess(curve, 0.1, 0.01);
        HullWhiteForwardProcess atFive = hullWhite.WithForwardMeasureTime(5.0);
        HullWhiteForwardProcess atTen = atFive.WithForwardMeasureTime(10.0);
        Assert.NotSame(hullWhite, atFive);
        Assert.Equal("forward measure time not set", Assert.Throws<NanoQLException>(() => hullWhite.ForwardMeasureTime).Message);
        Assert.Equal(5.0, atFive.ForwardMeasureTime);
        Assert.Equal(10.0, atTen.ForwardMeasureTime);
        Assert.Equal(atFive.Expectation(0.0, 0.03, 1.0), hullWhite.WithForwardMeasureTime(5.0).Expectation(0.0, 0.03, 1.0));
        Assert.NotEqual(atFive.Expectation(0.0, 0.03, 1.0), atTen.Expectation(0.0, 0.03, 1.0));

        var g2 = new G2ForwardProcess(0.1, 0.01, 0.2, 0.013, -0.5, curve);
        G2ForwardProcess g2AtFive = g2.WithForwardMeasureTime(5.0);
        Assert.NotSame(g2, g2AtFive);
        Assert.Throws<NanoQLException>(() => g2.ForwardMeasureTime);
        Assert.Equal(5.0, g2AtFive.ForwardMeasureTime);
        Vector x0 = g2AtFive.InitialValues();
        Assert.Throws<NanoQLException>(() => g2.Drift(0.5, x0));
        Assert.Equal(g2AtFive.Drift(0.5, x0)[0], g2.WithForwardMeasureTime(5.0).Drift(0.5, x0)[0]);
    }
}
