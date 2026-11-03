// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The QD fixed-point engine's tanh-sinh scheme with no fixed-point step. QuantLib's Gauss-Legendre scheme refuses m =
/// 0 ("at least one fixed point iteration step is needed", <c>qdfpamericanengine.cpp:44</c>), but its tanh-sinh scheme
/// checks nothing, and its m − 1 naive steps wrap to 2⁶⁴ − 1, so the engine never ends (QuantLib-Python 1.44 in a child
/// process, killed after 30 seconds). nanoql refuses m = 0 with the Gauss-Legendre scheme's text. With no node
/// QuantLib's engine refuses when it prices ("not enough points to interpolate: at least 2 required, 1 provided"), and
/// nanoql's likewise.
/// </summary>
public class QdFpTanhSinhSchemeTests
{
    [Fact]
    public void NoFixedPointStepIsRefused()
    {
        NanoQLException e = Assert.Throws<NanoQLException>(() => new QdFpTanhSinhIterationScheme(0, 8, 1e-8));
        Assert.Equal("at least one fixed point iteration step is needed", e.Message);
    }

    [Fact]
    public void NoNodeIsRefusedWhenPricing()
    {
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        var process = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(today, 0.02, new Actual365Fixed()),
            new FlatForward(today, 0.05, new Actual365Fixed()),
            new BlackConstantVol(today, new NullCalendar(), 0.25, new Actual365Fixed()));
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new AmericanExercise(today, today + 365));
        var scheme = new QdFpTanhSinhIterationScheme(8, 0, 1e-8);
        Exception e = Assert.ThrowsAny<Exception>(() => QdFpAmericanEngine.Calculate(option, context, process, scheme));
        Assert.True(e is ArgumentException or NanoQLException, e.GetType().Name);
        Assert.Contains("not enough points to interpolate: at least 2 required, 1 provided", e.Message);
    }
}
