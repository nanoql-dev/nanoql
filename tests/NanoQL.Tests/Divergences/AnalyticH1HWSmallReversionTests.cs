// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// H1HW's I₄ at small reversion and at c = λ (the S-hes plan's HR7 (xvi)). QuantLib's I₄ = −1/λ (b/c (1 − e^(−cT)) + aT +
/// a/λ (e^(−λT) − 1) + b/(c − λ) e^(−cT)(1 − e^(−T(λ − c)))) (<c>analytich1hwengine.cpp:126-130</c>) cancels as λT
/// falls to order λ, its relative error about 2ε/(λT)², 1e-12 at λT 1e-2, and its last term is 0/0 at c = λ, a NaN price.
/// nanoql evaluates the expression in double-double below λT = 2e-2, in quad-double below 1e-10 and by the cubic below
/// 1e-12, QuantLib's double above (D3-4 of the S-hes run record), and writes the last term
/// b·e^(−cT)·(−expm1((c − λ)T))/(c − λ), exact everywhere, −b·T·e^(−cT) at c = λ. Checked against the expression in 50
/// digits (<c>scratchpad\c3\refs_c3.py</c>) at a 0.2, b 0.03, c 0.4 and T 10.
/// </summary>
public class AnalyticH1HWSmallReversionTests
{
    [Fact]
    public void BelowTheCutI4HoldsTheExactExpression()
    {
        // λT 1e-2 (where QuantLib's double is off by 1.0e-12), 1e-3, 1e-6 and 1e-9
        (double Lambda, string Exact)[] points =
        [
            (1e-3, "-1.0530355498877916935853189E+1"),
            (1e-4, "-1.0562368232276866402970419E+1"),
            (1e-7, "-1.0565930615442750364626857E+1"),
            (1e-10, "-1.0565934178724788384151848E+1"),
        ];
        foreach ((double lambda, string exact) in points)
        {
            double expected = double.Parse(exact, CultureInfo.InvariantCulture);
            double i4 = I4(lambda, 0.2, 0.03, 0.4, 10.0);
            Assert.True(Math.Abs(i4 - expected) <= 1e-13 * Math.Abs(expected), $"lambda {lambda}: {i4} for {exact}");
        }
    }

    [Fact]
    public void AtCEqualToLambdaI4IsItsLimit()
    {
        // QuantLib's 0/0, NaN
        double expected = double.Parse("-3.9432236371527294722126087E+0", CultureInfo.InvariantCulture);
        double i4 = I4(0.4, 0.2, 0.03, 0.4, 10.0);
        Assert.True(Math.Abs(i4 - expected) <= 1e-14 * Math.Abs(expected), $"{i4} for {expected}");
    }

    private static double Constant(string name) =>
        (double)typeof(AnalyticH1HWEngine).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    // the engine's private I4Factor in its dispatch, on its cut and floors, the fit a decaying exponential
    private static double I4(double lambda, double a, double b, double c, double term)
    {
        Assembly asm = typeof(AnalyticH1HWEngine).Assembly;
        Type factor = typeof(AnalyticH1HWEngine).GetNestedType("I4Factor", BindingFlags.NonPublic)!;
        Type fit = typeof(AnalyticH1HWEngine).GetNestedType("Fit", BindingFlags.NonPublic)!;
        object expression = Activator.CreateInstance(factor, [a, b, c, term, Enum.ToObject(fit, 0)])!;
        MethodInfo evaluate = asm.GetType("NanoQL.Numerics.IKappaTExpression", true)!
            .GetMethod("Evaluate", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(factor);
        double cut = Constant("I4Cut"), floor = Constant("I4Floor"), quadFloor = Constant("I4QuadFloor");
        return (double)evaluate.Invoke(null, [expression, lambda, term, cut, floor, quadFloor, double.PositiveInfinity])!;
    }
}
