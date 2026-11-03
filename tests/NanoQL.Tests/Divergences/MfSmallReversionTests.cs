// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Markov-functional state process at small reversions: QuantLib's variance over a step, (e^{2au} −
/// e^{2as})/(2a)·σ², cancels as a falls below 1e-2, and cuts to its zero formula only below its epsilon
/// (<c>mfstateprocess.cpp:26-27</c>, <c>:81-84</c>, <c>:98-110</c>). On the harness, the variance over [0, 3] without
/// step times at a volatility of 1 is 2.9976021664879227 at a = 1e-14 (−8.0e-4 from the limit, 3), 3.000044657142098 at
/// 1e-12 (+1.5e-5) and 3.0000000927898895 at 1e-8. nanoql's is σ²e^{2as}·expm1(2a(u − s))/(2a) below 1e-2, its zero
/// formula kept below QuantLib's epsilon, and QuantLib's form and bits from 1e-2. QuantLib's checks: its own form at
/// the switch, which the expm1 form meets, and its own zero formula, the forms' limit, which the variance meets as a →
/// 0 in proportion to a.
/// </summary>
public class MfSmallReversionTests
{
    private static readonly double[] Times = [1.0, 2.0, 5.0];
    private static readonly double[] Vols = [0.8, 1.0, 1.2, 0.9];

    [Fact]
    public void TheVarianceMeetsQuantLibsFormAtTheSwitch()
    {
        // QuantLib's form at ±1e-2, on the harness: (no steps, steps) × (var(0, 3), var(1.5, 2)).
        (double A, double[] Steps, double[] Vols, double Over03, double Over152)[] quantLib =
        [
            (1e-2, [], [1.0], 3.091827327267982, 2.10268236503498),
            (1e-2, Times, Vols, 3.1907701985517494, 2.8000253203952035),
            (-1e-2, [], [1.0], 2.911773320787564, 1.902585682127994),
            (-1e-2, Times, Vols, 2.9738973628067966, 2.527289305548242),
        ];
        foreach ((double a, double[] steps, double[] vols, double over03, double over152) in quantLib)
        {
            var below = new MfStateProcess(a * (1.0 - 1e-12), steps, vols);
            var at = new MfStateProcess(a, steps, vols);
            Assert.True(Math.Abs((below.Variance(0.0, 0.0, 3.0) / over03) - 1.0) <= 1e-13, $"{a}: {below.Variance(0.0, 0.0, 3.0)}");
            Assert.True(Math.Abs((below.Variance(1.5, 0.0, 2.0) / over152) - 1.0) <= 1e-13, $"{a}: {below.Variance(1.5, 0.0, 2.0)}");
            Assert.True(Math.Abs((at.Variance(0.0, 0.0, 3.0) / over03) - 1.0) <= 1e-15, $"{a}: {at.Variance(0.0, 0.0, 3.0)}");
        }
    }

    [Fact]
    public void TheVarianceTendsToTheZeroReversion()
    {
        foreach ((double[] steps, double[] vols) in new[] { (Array.Empty<double>(), new[] { 1.0 }), (Times, Vols) })
        {
            var zero = new MfStateProcess(0.0, steps, vols);
            foreach (double a in new[] { 1e-14, 1e-12, 1e-8, 1e-6 })
            {
                var small = new MfStateProcess(a, steps, vols);
                foreach ((double t0, double dt) in new[] { (0.0, 3.0), (1.5, 2.0), (4.0, 3.0) })
                {
                    double v = small.Variance(t0, 0.0, dt), z = zero.Variance(t0, 0.0, dt);
                    Assert.True(Math.Abs((v / z) - 1.0) <= Math.Max(2e-15, 3.0 * a * (t0 + dt)), $"{a} ({t0}, {dt}): {v} against {z}");
                }
            }
        }

        // QuantLib's at 1e-14 without steps over [0, 3].
        Assert.True(Math.Abs((2.9976021664879227 / 3.0) - 1.0) > 7e-4);
    }
}
