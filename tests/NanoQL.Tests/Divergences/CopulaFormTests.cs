// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The copulas where QuantLib's formulas fail: Clayton's maximum taken after the power, so a negative base gives a
/// positive value; Clayton's, Frank's and Plackett's formulas cancelling near independence and near the edges; Frank's
/// overflowing at large |θ|; Gumbel's and Galambos' powers leaving the doubles' range at large θ; Clayton's overflowing
/// at large θ near 0; Ali-Mikhail-Haq's denominator cancelling as θ nears 1 and its arguments 0; and Galambos' and
/// Ali-Mikhail-Haq's product x·y underflowing near (0, 0), Galambos' to NaN where x and y are subnormal. nanoql's forms
/// are within a few ulps of a high-precision evaluation of QuantLib's own formula (at 60 digits and more, and at 800),
/// where QuantLib's double formula gives the values beside them.
/// </summary>
public class CopulaFormTests
{
    private static void Near(double exact, double actual, double relative = 4e-15) =>
        Assert.True(Math.Abs(actual - exact) <= relative * Math.Abs(exact), $"{actual} for {exact}");

    [Fact]
    public void ClaytonsMaximumIsTakenBeforeThePower()
    {
        // θ −0.5 at (0.2, 0.2): the base 2√0.2 − 1 is negative, and QuantLib squares it, 0.011145618000168249
        Assert.Equal(0.0, new ClaytonCopula(-0.5).Evaluate(0.2, 0.2));
    }

    [Fact]
    public void AliMikhailHaqKeepsItsDigitsNearItsBound()
    {
        Near(5e-18, new AliMikhailHaqCopula(1.0).Evaluate(1e-17, 1e-17));                   // QuantLib ∞
        Near(5.000000000000025e-15, new AliMikhailHaqCopula(1.0).Evaluate(1e-14, 1e-14));   // QuantLib 5.0039995859672178e-15
    }

    [Fact]
    public void FrankKeepsItsDigitsAtEveryTheta()
    {
        Near(0.9833640686849763, new FrankCopula(50.0).Evaluate(0.99, 0.99));       // QuantLib ∞
        Near(6.118043590140166e-09, new FrankCopula(-50.0).Evaluate(0.3, 0.4));     // QuantLib 6.1180435885665413e-09
        Near(6.22459330026836e-09, new FrankCopula(1.0).Evaluate(1e-8, 0.5));       // QuantLib 6.2245934156484611e-09
        Near(0.180000000252, new FrankCopula(1e-8).Evaluate(0.3, 0.6));             // QuantLib 0.18000000395303656
        Near(0.3, new FrankCopula(800.0).Evaluate(0.3, 0.6));                       // QuantLib ∞

        // below θ 0 the quotient takes the larger argument, so it stays normal: within |θ|·ε at θ -700
        Near(9.118819658733783e-16, new FrankCopula(-700.0).Evaluate(0.99, 1e-12), 1e-13);   // QuantLib 9.1196891308465884e-16
    }

    [Fact]
    public void GumbelAndGalambosFactorTheirLargerTermAtLargeTheta()
    {
        Near(0.7, new GumbelCopula(1000.0).Evaluate(0.9, 0.7));       // QuantLib 1
        Near(0.7, new GalambosCopula(1000.0).Evaluate(0.9, 0.7));     // QuantLib 0.63, x·y
    }

    [Fact]
    public void GalambosAndAliMikhailHaqKeepTheirProductBelowTheNormalRange()
    {
        // x·y below the normal range: the product formed last, and Galambos' e^t, overflowing where x and y are both
        // subnormal, as e^(t + ln x + ln y)
        Near(6.0981601698133e-311, new GalambosCopula(1000.0).Evaluate(1e-310, 1e-310), 1e-9);   // QuantLib NaN
        Near(5.496740023150023e-242, new GalambosCopula(3.0).Evaluate(1e-200, 1e-200), 1e-12);   // QuantLib 0
        Near(5e-201, new AliMikhailHaqCopula(1.0).Evaluate(1e-200, 1e-200));                     // QuantLib NaN
    }

    [Fact]
    public void ClaytonKeepsItsDigitsNearIndependenceAndAtLargeTheta()
    {
        Near(5.0000006384121255e-09, new ClaytonCopula(1e-8).Evaluate(1e-8, 0.5));   // QuantLib 5.0000006052355184e-09
        Near(1e-08, new ClaytonCopula(50.0).Evaluate(1e-8, 0.5));                    // QuantLib 0, its power overflowing
    }

    [Fact]
    public void PlackettTakesTheStableRoot()
    {
        Near(0.12000005039997681, new PlackettCopula(1.000001).Evaluate(0.3, 0.4));   // QuantLib 0.12000005035083462
        Near(3.333333348148148e-09, new PlackettCopula(0.5).Evaluate(1e-8, 0.5));       // QuantLib 3.3333332760676626e-09
    }

    [Fact]
    public void WhereQuantLibsFormulaHoldsItsValueIsKept()
    {
        // Clayton above θ 0.1, Gumbel and Galambos to θ 19, Ali-Mikhail-Haq at θ ½ and below: QuantLib's formulas as
        // QuantLib writes them, bit for bit
        double x = 0.3, y = 0.6;
        Assert.Equal(Math.Max(Math.Pow(Math.Pow(x, -2.0) + Math.Pow(y, -2.0) - 1.0, -1.0 / 2.0), 0.0), new ClaytonCopula(2.0).Evaluate(x, y));
        Assert.Equal(
            Math.Exp(-Math.Pow(Math.Pow(-Math.Log(x), 3.0) + Math.Pow(-Math.Log(y), 3.0), 1 / 3.0)),
            new GumbelCopula(3.0).Evaluate(x, y));
        Assert.Equal(
            x * y * Math.Exp(Math.Pow(Math.Pow(-Math.Log(x), -3.0) + Math.Pow(-Math.Log(y), -3.0), -1 / 3.0)),
            new GalambosCopula(3.0).Evaluate(x, y));
        Assert.Equal(x * y / (1.0 - (0.5 * (1.0 - x) * (1.0 - y))), new AliMikhailHaqCopula(0.5).Evaluate(x, y));
    }
}
