// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The vega-stressed process's stress reaching its standard deviation, variance and step (fixed). QuantLib's take the
/// exact forms from the term structures on a strike-independent volatility, which read no diffusion, so the stress
/// never reaches them. On QuantLib 1.44 (stress 0.1 on a flat 0.2, r − q 0.03) the diffusion is 0.3 but the deviation
/// over a year 0.2 and the step from 100 at a draw of 1 123.367806, the unstressed process's. nanoql's go through the
/// Euler discretization with the stressed diffusion inside the box and the plain one outside; with no stress they are
/// QuantLib's, and the expectation is the unstressed one, the mean of the asset reading no volatility.
/// </summary>
public class VegaStressedProcessTests
{
    private static readonly Date Today = new(15, Month.June, 2026);

    [Fact]
    public void TheStressReachesTheStepInsideTheBox()
    {
        VegaStressedBlackScholesProcess stressed = Process(0.1);
        Assert.Equal(0.3, stressed.Diffusion(0.5, 100.0), 15);
        Assert.Equal(0.3, stressed.StdDeviation(0.0, 100.0, 1.0), 15);
        Assert.Equal(0.09, stressed.Variance(0.0, 100.0, 1.0), 15);

        // the Euler step at the stressed volatility, in the log price
        double expected = 100.0 * Math.Exp(stressed.Drift(0.0, 100.0) + 0.3);
        Assert.Equal(expected, stressed.Evolve(0.0, 100.0, 1.0, 1.0), 10);
        Assert.True(Math.Abs(stressed.Evolve(0.0, 100.0, 1.0, 1.0) - 123.367806) > 9.0);
    }

    [Fact]
    public void OutsideTheBoxTheStepIsThePlainOne()
    {
        VegaStressedBlackScholesProcess stressed = Process(0.1);
        Assert.Equal(0.2, stressed.StdDeviation(0.0, 2000.0, 1.0), 15);
        double expected = 2000.0 * Math.Exp(stressed.Drift(0.0, 2000.0) + 0.2);
        Assert.Equal(expected, stressed.Evolve(0.0, 2000.0, 1.0, 1.0), 9);
    }

    [Fact]
    public void WithNoStressTheProcessIsQuantLibs()
    {
        VegaStressedBlackScholesProcess plain = Process(0.0);
        GeneralizedBlackScholesProcess bsm = Bsm();
        Assert.Equal(bsm.StdDeviation(0.0, 100.0, 1.0), plain.StdDeviation(0.0, 100.0, 1.0));
        Assert.Equal(bsm.Evolve(0.0, 100.0, 1.0, 1.0), plain.Evolve(0.0, 100.0, 1.0, 1.0));
        Assert.Equal(123.367806, plain.Evolve(0.0, 100.0, 1.0, 1.0), 6);
        Assert.Equal(bsm.Expectation(0.0, 100.0, 1.0), Process(0.1).Expectation(0.0, 100.0, 1.0));
    }

    private static GeneralizedBlackScholesProcess Bsm() => new(
        100.0,
        new FlatForward(Today, 0.0, new Actual365Fixed()),
        new FlatForward(Today, 0.03, new Actual365Fixed()),
        new BlackConstantVol(Today, new NullCalendar(), 0.2, new Actual365Fixed()));

    private static VegaStressedBlackScholesProcess Process(double stress) => new(
        100.0,
        new FlatForward(Today, 0.0, new Actual365Fixed()),
        new FlatForward(Today, 0.03, new Actual365Fixed()),
        new BlackConstantVol(Today, new NullCalendar(), 0.2, new Actual365Fixed()),
        0.0,
        1.0,
        0.0,
        1000.0,
        stress);
}
