// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="GJRGARCHProcess.Apply"/> and <see cref="HybridHestonHullWhiteProcess.Apply"/>, QuantLib's
/// (<c>gjrgarchprocess.cpp:114-117</c>, <c>hybridhestonhullwhiteprocess.cpp:79-88</c>): the spot moves by e^dx, the
/// variance and the short rate by dx.
/// </summary>
public class HestonHullWhiteAndGjrGarchApplyTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.05, A365);
    private static readonly YieldTermStructure Dividends = new FlatForward(Today, 0.02, A365);

    [Fact]
    public void TheGjrGarchSpotMovesByTheExponentialAndTheVarianceByTheStep()
    {
        var process = new GJRGARCHProcess(RiskFree, Dividends, 50.0, 2.0e-4, 2.0e-6, 0.024, 0.93, 0.059, 0.1, 365.0);
        Vector y = process.Apply(new Vector([50.0, 2.0e-4]), new Vector([0.03, -5.0e-5]));
        Assert.Equal([50.0 * Math.Exp(0.03), 2.0e-4 + -5.0e-5], y.ToArray());
    }

    [Fact]
    public void TheHybridsSpotMovesByTheExponentialAndTheVarianceAndShortRateByTheStep()
    {
        var heston = new HestonProcess(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.04, 0.5, -0.7);
        var hybrid = new HybridHestonHullWhiteProcess(
            heston, new HullWhiteForwardProcess(RiskFree, 0.1, 0.01).WithForwardMeasureTime(2.0), -0.4);
        Vector y = hybrid.Apply(new Vector([100.0, 0.04, 0.03]), new Vector([-0.02, 0.005, -0.001]));
        Assert.Equal([100.0 * Math.Exp(-0.02), 0.04 + 0.005, 0.03 + -0.001], y.ToArray());
    }
}
