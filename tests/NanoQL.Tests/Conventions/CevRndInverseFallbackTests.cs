// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="CEVRNDCalculator.InvCdf"/>'s fallback: QuantLib's <c>catch (...)</c> around the Sankaran guess's Brent
/// (<c>cevrndcalculator.cpp:111-120</c>) is a catch of <see cref="NanoQLException"/>, the type Brent's failures throw
/// here, and the inverse then starts from the forward.
/// </summary>
public class CevRndInverseFallbackTests
{
    [Fact]
    public void WhereTheGuessesBrentFailsTheInverseStartsFromTheForward()
    {
        // β 0.95, σ 200%, five years, the median: the guess's Brent can't bracket its root in 20 evaluations
        const double f0 = 100.0, beta = 0.95, t = 5.0, q = 0.5;
        double alpha = 2.0 * Math.Pow(f0, 1 - beta);
        var calculator = new CEVRNDCalculator(f0, alpha, beta);

        MethodInfo sankaran = typeof(CEVRNDCalculator).GetMethod(
            "SankaranApprox", BindingFlags.NonPublic | BindingFlags.Instance)!;
        double x = new InverseCumulativeNormal().Evaluate(1 - q);
        double y0 = Math.Pow(f0, 2 * (1 - beta)) / Math.Pow(alpha * (1 - beta), 2) / t;
        var e = Assert.Throws<NanoQLException>(() => new Brent().SetMaxEvaluations(20).Solve(
            c => (double)sankaran.Invoke(calculator, [c, t, x])!, 1e-8, y0, 0.02 * y0));
        Assert.StartsWith("unable to bracket root in 20 function evaluations", e.Message, StringComparison.Ordinal);

        double f = calculator.InvCdf(q, t);
        Assert.True(calculator.Cdf(Math.Max(0.0, f - 1e-8), t) <= q && q <= calculator.Cdf(f + 1e-8, t), $"{f}");
    }
}
