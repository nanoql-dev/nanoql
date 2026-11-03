// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Documented divergences of <see cref="ExtendedOrnsteinUhlenbeckProcess"/>'s expectation. The trapezoidal rule's (1 −
/// e^(−a·dt))/(a·dt) is 0/0 at a speed of 0 (QuantLib's NaN) and cancels below (2.2e-5 lost at 1e-12); here it is a
/// series below a·dt of 1e-2. QuantLib's Gauss-Lobatto rule integrates b(x)e^(ax) to an absolute 1e-4, out of reach
/// once e^(a(t₀+dt)) is large: it throws from a(t₀+dt) of 35; here it integrates the bounded b(x)e^(−a(t₀+dt−x)) past
/// 30, at a relative 1e-10 beside the absolute accuracy. Both against the closed form x₀e^(−a·dt) + (c₀ + c₁T)(1 −
/// e^(−a·dt)) − c₁((1 − e^(−a·dt))/a − dt·e^(−a·dt)) for b(t) = c₀ + c₁t, T = t₀ + dt, which the trapezoidal rule
/// matches exactly for linear b.
/// </summary>
public class ExtendedOrnsteinUhlenbeckExpectationTests
{
    [Fact]
    public void AtASpeedOfZeroTheTrapezoidalExpectationIsTheValue()
    {
        var process = new ExtendedOrnsteinUhlenbeckProcess(
            0.0, 0.3, 0.0, t => 1.0 + (0.5 * t), ExtendedOrnsteinUhlenbeckProcessDiscretization.Trapezodial);
        Assert.Equal(2.5, process.Expectation(0.25, 2.5, 1.0));
    }

    [Theory]
    [InlineData(1e-12, 1.0)]
    [InlineData(1e-6, 1.0)]
    [InlineData(1e-3, 2.0)]
    [InlineData(0.00999, 1.0)]
    [InlineData(0.01, 1.0)]
    [InlineData(0.5, 1.0)]
    [InlineData(3.0, 0.25)]
    public void TheTrapezoidalExpectationIsTheClosedFormForLinearLevels(double speed, double dt)
    {
        // below a·dt of 1e-2 the series, from it QuantLib's quotient: each the closed form, so continuous at the cut
        const double t0 = 0.5, x0 = 2.0, c0 = 1.0, c1 = 0.5;
        var process = new ExtendedOrnsteinUhlenbeckProcess(
            speed, 0.3, 0.0, t => c0 + (c1 * t), ExtendedOrnsteinUhlenbeckProcessDiscretization.Trapezodial);
        double expected = ClosedForm(speed, x0, t0, dt, c0, c1);
        Assert.Equal(expected, process.Expectation(t0, x0, dt), 1e-14);
    }

    [Theory]
    [InlineData(10.0, 2.0, 1.0, 0.0)]
    [InlineData(35.0, 0.5, 0.5, 0.0)]
    [InlineData(100.0, 0.95, 0.05, 1.0)]
    [InlineData(50.0, 1.0, 1.0, 0.5)]
    [InlineData(0.5, 99.0, 5.0, 0.5)]
    [InlineData(1000.0, 0.0, 1.0, 0.25)]
    public void PastTheCutTheGaussLobattoExpectationIsTheClosedForm(double speed, double t0, double dt, double c1)
    {
        // a(t₀ + dt) from 30 to 1000, where QuantLib's rule throws from 35
        const double x0 = 3.0, c0 = 1.2;
        var process = new ExtendedOrnsteinUhlenbeckProcess(
            speed, 0.3, 0.0, t => c0 + (c1 * t), ExtendedOrnsteinUhlenbeckProcessDiscretization.GaussLobatto);
        double expected = ClosedForm(speed, x0, t0, dt, c0, c1);
        double actual = process.Expectation(t0, x0, dt);
        Assert.True(Math.Abs(actual - expected) <= 1e-12 * Math.Abs(expected), $"{actual} against {expected}");
    }

    [Theory]
    [InlineData(30.0, 0.0, 1.0, 0.0)]
    [InlineData(30.0, 0.0, 1.0, 0.5)]
    [InlineData(100.0, 0.2, 0.1, 0.0)]
    [InlineData(100.0, 0.2, 0.1, 0.5)]
    public void AtTheCutTheGaussLobattoExpectationIsContinuous(double speed, double t0, double dt, double c1)
    {
        // the last dt whose a(t₀ + dt) is 30 or less, QuantLib's integrand, and the next double, the bounded one past
        // the cut, each the closed form (speed 30 to an end of 1, and speed 100 with a dt of 0.1)
        const double x0 = 3.0, c0 = 1.2;
        var process = new ExtendedOrnsteinUhlenbeckProcess(
            speed, 0.3, 0.0, t => c0 + (c1 * t), ExtendedOrnsteinUhlenbeckProcessDiscretization.GaussLobatto);
        double below = dt;
        for (int i = 0; i < 64 && speed * (t0 + below) > 30.0; ++i)
        {
            below = Math.BitDecrement(below);
        }

        for (int i = 0; i < 64 && speed * (t0 + Math.BitIncrement(below)) <= 30.0; ++i)
        {
            below = Math.BitIncrement(below);
        }

        double above = Math.BitIncrement(below);
        for (int i = 0; i < 64 && speed * (t0 + above) <= 30.0; ++i)
        {
            above = Math.BitIncrement(above);
        }

        Assert.True(speed * (t0 + below) <= 30.0 && speed * (t0 + above) > 30.0, "the two points straddle the cut");
        foreach (double step in new[] { below, above })
        {
            double expected = ClosedForm(speed, x0, t0, step, c0, c1);
            double actual = process.Expectation(t0, x0, step);
            Assert.True(Math.Abs(actual - expected) <= 1e-12 * Math.Abs(expected), $"dt {step:R}: {actual} against {expected}");
        }
    }

    private static double ClosedForm(double a, double x0, double t0, double dt, double c0, double c1)
    {
        double ex = Math.Exp(-a * dt);
        double oneMinusEx = OneMinusExp(a * dt);
        double ratio = a * dt == 0.0 ? dt : oneMinusEx / a;
        return (x0 * ex) + ((c0 + (c1 * (t0 + dt))) * oneMinusEx) - (c1 * (ratio - (dt * ex)));
    }

    // 1 − e^(−x): its Taylor series below 0.1, to twelve terms, cancellation-free; the difference above
    private static double OneMinusExp(double x)
    {
        if (x >= 0.1)
        {
            return 1.0 - Math.Exp(-x);
        }

        double term = x, sum = 0.0;
        for (int k = 1; k <= 12; ++k)
        {
            sum += term;
            term *= -x / (k + 1);
        }

        return sum;
    }
}
