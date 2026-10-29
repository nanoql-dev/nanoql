// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Geman-Roncoroni process as a working type, QuantLib's semantics kept (the S-xrt plan's XRR3, ruled 2026-10-28),
/// and its two route (§2.5): the one-dimensional step draws its jump uniforms from a generator the process builds on its
/// first call, seeded from that call's draw, and continues for every later call, so a process's steps depend on its
/// history and a copy from <see cref="GemanRoncoroniProcess.Clone"/> starts its own; the five-argument step taking the
/// uniforms is stateless; and with no jump intensity, below the threshold, the step is the diffusion's Euler step at the
/// interval's midpoint.
/// </summary>
public class GemanRoncoroniWorkingTypeTests
{
    [Fact]
    public void TheStepContinuesTheProcesssGenerator()
    {
        double[] first = Steps(Process(), 6);
        double[] again = Steps(Process(), 6);
        Assert.Equal(first, again);

        // a used process's next steps aren't a fresh one's first
        GemanRoncoroniProcess used = Process();
        Steps(used, 6);
        Assert.NotEqual(first, Steps(used, 6));

        // a clone starts its own generator
        Assert.Equal(first, Steps(used.Clone(), 6));
    }

    [Fact]
    public void TheFiveArgumentStepIsStateless()
    {
        GemanRoncoroniProcess process = Process();
        double[] du = [0.3, 0.6];
        double step = process.Evolve(0.1, 3.3, 0.5, 0.2, du);
        Steps(process, 4);
        Assert.Equal(step, process.Evolve(0.1, 3.3, 0.5, 0.2, du));
    }

    [Fact]
    public void WithNoJumpsTheStepIsTheDiffusionsEulerStep()
    {
        GemanRoncoroniProcess process = Process(theta2: 0.0);
        double t0 = 0.1, x0 = 3.3, dt = 0.01, dw = 0.7;
        double t = t0 + (0.5 * dt);
        double expected = x0 + (process.Drift(t, x0) * dt) + (process.StdDeviation(t, x0, dt) * dw);
        Assert.Equal(expected, process.Evolve(t0, x0, dt, dw, [0.3, 0.6]), 14);
    }

    // vpp's testGemanRoncoroniProcess's parameters
    private static GemanRoncoroniProcess Process(double theta2 = 9.0) =>
        new(3.3, 3.1, 0.05, -0.09, 0.07, -0.40, -0.40, 1.6, 1.0, 0.5, 10.0, -7.0, -0.3, 35.0, theta2, 0.10, 1.9);

    private static double[] Steps(GemanRoncoroniProcess process, int n)
    {
        double[] x = new double[n];
        for (int i = 0; i < n; i++)
        {
            x[i] = process.Evolve(0.004 * i, 3.3, 0.5, 0.3 * (i - 2));
        }

        return x;
    }
}
