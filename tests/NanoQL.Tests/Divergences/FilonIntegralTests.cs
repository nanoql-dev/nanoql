// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Filon's coefficients at small θ = t·h: QuantLib's closed forms for α, β and γ cancel as θ falls, so its ∫₀¹ cos(tx)
/// dx on 100 intervals is 5.7e-13 off at t 1, 2.4e-4 off at 1e-4, 5.7e-16 for 1 at 1e-6 and NaN at 0; nanoql takes
/// their Taylor series below |θ| 1.7, within 1e-15 of sin t/t, and QuantLib's closed forms, bit for bit, from 1.7. The
/// references are sin t/t and (1 − cos t)/t at 40 digits; QuantLib's values are its own.
/// </summary>
public class FilonIntegralTests
{
    private static double Cosine(double t) => new FilonIntegral(FilonIntegralType.Cosine, t, 100).Integrate(x => 1.0, 0.0, 1.0);

    private static double Sine(double t) => new FilonIntegral(FilonIntegralType.Sine, t, 100).Integrate(x => 1.0, 0.0, 1.0);

    private static void Near(double exact, double actual) =>
        Assert.True(Math.Abs(actual - exact) <= 1e-15 * Math.Abs(exact), $"{actual:R} for {exact:R}");

    [Fact]
    public void AtSmallThetaTheCoefficientsKeepTheirDigits()
    {
        Near(0.84147098480789651, Cosine(1.0));           // QuantLib 0.84147098480732241
        Near(0.45969769413186028, Sine(1.0));             // QuantLib 0.45969769413154693
        Near(0.99999999833333333, Cosine(1e-4));          // QuantLib 0.99975585770874076
        Near(4.9999999958333336e-5, Sine(1e-4));          // QuantLib 4.998779292709356e-05
        Near(0.99999999999983333, Cosine(1e-6));          // QuantLib 5.6843418860808016e-16
        Near(4.9999999999995831e-7, Sine(1e-6));          // QuantLib 5.4210108624275223e-22
    }

    [Fact]
    public void AtThetaZeroTheRuleIsSimpsons()
    {
        // QuantLib's NaN: at t 0 the cosine integral is Simpson's rule on f, the sine one 0
        Assert.True(Math.Abs(Cosine(0.0) - 1.0) <= 2.2e-16);
        Assert.Equal(0.0, Sine(0.0));
    }

    [Fact]
    public void FromThetaOnePointSevenQuantLibsClosedFormsAreKept()
    {
        // t 400 on 100 intervals: θ 4, QuantLib's values, bit for bit
        Assert.Equal(-0.0021272983990981904, Cosine(400.0));
        Assert.Equal(0.0038132408466055442, Sine(400.0));
    }
}
