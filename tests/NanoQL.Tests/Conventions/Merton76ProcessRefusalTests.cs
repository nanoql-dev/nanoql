// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="Merton76Process"/> carries the jump-diffusion engine's parameters and implements no dynamics: QuantLib's
/// drift, diffusion and apply fail with their own texts (<c>merton76process.hpp:50-52</c>), and its discretization's
/// expectation, standard deviation and variance, and the evolved state, which read them, fail with theirs. nanoql's
/// refuse with the same texts; its state variable, curves and volatility are the Black-Scholes-Merton process's it wraps.
/// </summary>
public class Merton76ProcessRefusalTests
{
    [Fact]
    public void TheDynamicsAreRefusedWithQuantLibsTexts()
    {
        Merton76Process p = Process();
        Assert.Equal("Merton76Process does not implement drift", Assert.Throws<NanoQLException>(() => p.Drift(0.5, 100.0)).Message);
        Assert.Equal(
            "Merton76Process does not implement diffusion", Assert.Throws<NanoQLException>(() => p.Diffusion(0.5, 100.0)).Message);
        Assert.Equal("Merton76Process does not implement apply", Assert.Throws<NanoQLException>(() => p.Apply(100.0, 0.1)).Message);
        Assert.Equal(
            "Merton76Process does not implement drift", Assert.Throws<NanoQLException>(() => p.Expectation(0.5, 100.0, 0.25)).Message);
        Assert.Equal(
            "Merton76Process does not implement diffusion",
            Assert.Throws<NanoQLException>(() => p.StdDeviation(0.5, 100.0, 0.25)).Message);
        Assert.Equal(
            "Merton76Process does not implement diffusion", Assert.Throws<NanoQLException>(() => p.Variance(0.5, 100.0, 0.25)).Message);
        Assert.Throws<NanoQLException>(() => p.Evolve(0.5, 100.0, 0.25, 0.3));
    }

    [Fact]
    public void ItsMarketIsTheWrappedProcesss()
    {
        Merton76Process p = Process();
        Assert.Equal(95.0, p.X0);
        Assert.Equal(95.0, p.StateVariable);
        Assert.Equal((1.0, -0.1, 0.25), (p.JumpIntensity, p.LogMeanJump, p.LogJumpVolatility));
        Assert.Equal(new Actual365Fixed().YearFraction(new Date(15, Month.October, 2026), new Date(15, Month.October, 2027)), p.Time(new Date(15, Month.October, 2027)));
    }

    private static Merton76Process Process()
    {
        var today = new Date(15, Month.October, 2026);
        return new Merton76Process(
            95.0,
            new FlatForward(today, 0.02, new Actual365Fixed()),
            new FlatForward(today, 0.05, new Actual365Fixed()),
            new BlackConstantVol(today, new NullCalendar(), 0.25, new Actual365Fixed()),
            1.0,
            -0.1,
            0.25);
    }
}
