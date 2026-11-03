// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Hull-White process at small mean reversion: QuantLib's drift's σ²/(2a)(1 − e^(−2at))
/// (<c>hullwhiteprocess.cpp:39</c>) has no a → 0 branch, so at a = 0, which the constructor accepts, the drift is NaN,
/// and below it cancels: on the wheel, on a flat 4% curve with σ = 0.01, the drift at (1, 0.04) is NaN at a = 0 and
/// 9.99982319171977e-05 at a = 1e-12, −1.8e-5 relative off its limit σ²t + f′(t), 1.00000444e-4, and the one-year
/// expectation 0.04004999779003289 at 1e-12 against QuantLib's own a = 0 expectation, 0.04005000000218044 (its variance
/// 0.0001 at both). nanoql writes the term σ²t·(1 − e^(−x))/x, x = 2at. QuantLib's check: the limit, σ²t + f′(t) with
/// f′ QuantLib's difference quotient of the curve's forward over 1e-4 (<c>:40-44</c>), and QuantLib's own a = 0
/// expectation and variance.
/// </summary>
public class HullWhiteProcessSmallReversionTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.04, new Actual365Fixed());

    [Fact]
    public void TheDriftAtZeroReversionIsItsLimit()
    {
        double drift = new HullWhiteProcess(Curve, 0.0, 0.01).Drift(1.0, 0.04);
        Assert.True(Math.Abs(drift - Limit(1.0)) <= 1e-17, $"{drift} against {Limit(1.0)}");
        Assert.True(Math.Abs((drift / new HullWhiteProcess(Curve, 1e-15, 0.01).Drift(1.0, 0.04)) - 1.0) <= 1e-12);
    }

    [Fact]
    public void TheProcessAtSmallReversionIsItsLimit()
    {
        var p = new HullWhiteProcess(Curve, 1e-12, 0.01);
        Assert.True(Math.Abs(p.Drift(1.0, 0.04) - Limit(1.0)) <= 1e-15, $"{p.Drift(1.0, 0.04)}");
        Assert.True(Math.Abs(p.Expectation(0.0, 0.04, 1.0) - 0.04005000000218044) <= 1e-13, $"{p.Expectation(0.0, 0.04, 1.0)}");
        Assert.True(Math.Abs(p.Variance(0.0, 0.04, 1.0) - 0.0001) <= 1e-15, $"{p.Variance(0.0, 0.04, 1.0)}");
    }

    // sigma^2 t + f'(t), the drift's a -> 0 limit at x = 0.04, on the flat curve's forward f = 0.04
    internal static double Limit(double t) => (0.01 * 0.01 * t) + ForwardSlope(t);

    // QuantLib's f'(t): the forward's difference quotient over 1e-4
    internal static double ForwardSlope(double t)
    {
        double f = Curve.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency).Rate;
        double fup = Curve.ForwardRate(t + 0.0001, t + 0.0001, Compounding.Continuous, Frequency.NoFrequency).Rate;
        return (fup - f) / 0.0001;
    }
}
