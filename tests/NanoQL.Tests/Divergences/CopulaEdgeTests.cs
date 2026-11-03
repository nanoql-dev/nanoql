// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The copulas on the unit square's edges: every copula is defined on [0, 1]² with C(u, 0) = C(0, v) = 0, C(u, 1) = u
/// and C(1, v) = v, and QuantLib's checks accept the edges, but its formulas miss them: Hüsler-Reiss is NaN at (0, 0)
/// and wherever an argument is 1 but at (1, 1) (−ln 1 is −0, so the ratio's logarithm is that of −∞), and at θ 0 on
/// every edge point but (1, 1); Galambos and Ali-Mikhail-Haq are NaN at (0, 0); the Gaussian copula is NaN at (0, 0)
/// and (1, 1) at ρ 0.5, at (0, 1) and (1, 0) at −0.5, and 2e-10 off on the edges at 1; Clayton below 0 takes the power
/// of a negative base. nanoql's copulas return the boundary values before their formulas. QuantLib's values are its own
/// classes'.
/// </summary>
public class CopulaEdgeTests
{
    private static readonly double[] Edge = [0.0, 0.3, 0.7, 1.0];

    public static TheoryData<string> Copulas() =>
    [
        "AliMikhailHaq 1", "Clayton -0.5", "Clayton 2", "FarlieGumbelMorgenstern 0.5", "Frank -3", "Frank 3",
        "Galambos 0.5", "Galambos 3", "Gaussian 0.5", "Gaussian -0.5", "Gumbel 2", "HuslerReiss 0", "HuslerReiss 1",
        "Independent", "MarshallOlkin 0.3 0.6", "Max", "Min", "Plackett 0.5", "Plackett 3",
    ];

    [Theory]
    [MemberData(nameof(Copulas))]
    public void EveryCopulaTakesItsBoundaryValuesOnTheEdges(string copula)
    {
        Func<double, double, double> c = Make(copula);
        foreach (double u in Edge)
        {
            Assert.Equal(0.0, c(u, 0.0));
            Assert.Equal(0.0, c(0.0, u));
            Assert.Equal(u, c(u, 1.0));
            Assert.Equal(u, c(1.0, u));
        }
    }

    [Fact]
    public void QuantLibsFormulasMissTheEdges()
    {
        // QuantLib's values beside nanoql's boundary values
        Assert.Equal(0.0, new HuslerReissCopula(1.0).Evaluate(0.0, 0.0));   // QuantLib NaN
        Assert.Equal(0.5, new HuslerReissCopula(1.0).Evaluate(0.5, 1.0));   // QuantLib NaN
        Assert.Equal(0.5, new HuslerReissCopula(1.0).Evaluate(1.0, 0.5));   // QuantLib NaN
        Assert.Equal(1.0, new HuslerReissCopula(1.0).Evaluate(1.0, 1.0));   // QuantLib 1
        Assert.Equal(0.0, new HuslerReissCopula(0.0).Evaluate(0.3, 0.0));   // QuantLib NaN
        Assert.Equal(0.0, new HuslerReissCopula(0.0).Evaluate(0.0, 0.3));   // QuantLib NaN
        Assert.Equal(0.3, new HuslerReissCopula(0.0).Evaluate(0.3, 1.0));   // QuantLib NaN
        Assert.Equal(0.0, new GalambosCopula(0.5).Evaluate(0.0, 0.0));      // QuantLib NaN
        Assert.Equal(0.0, new GalambosCopula(3.0).Evaluate(0.0, 0.0));      // QuantLib NaN
        Assert.Equal(0.0, new AliMikhailHaqCopula(1.0).Evaluate(0.0, 0.0)); // QuantLib NaN
        Assert.Equal(0.0, new GaussianCopula(0.5).Evaluate(0.0, 0.0));      // QuantLib NaN
        Assert.Equal(1.0, new GaussianCopula(0.5).Evaluate(1.0, 1.0));      // QuantLib NaN
        Assert.Equal(0.3, new GaussianCopula(0.5).Evaluate(0.3, 1.0));      // QuantLib 0.29999999980137904
        Assert.Equal(0.0, new GaussianCopula(-0.5).Evaluate(0.0, 1.0));     // QuantLib NaN
        Assert.Equal(0.0, new GaussianCopula(-0.5).Evaluate(1.0, 0.0));     // QuantLib NaN
        Assert.Equal(0.0, new ClaytonCopula(-0.5).Evaluate(0.3, 0.0));      // QuantLib 0.20455488498966781
        Assert.Equal(0.0, new ClaytonCopula(-0.5).Evaluate(0.0, 0.3));      // QuantLib 0.20455488498966781

        // inside the square QuantLib's formula, bit for bit: Hüsler-Reiss at θ 1 as QuantLib writes it
        double x = 0.3, y = 0.6, theta = 1.0;
        var n = new CumulativeNormalDistribution();
        double quantLib = Math.Pow(x, n.Evaluate((1.0 / theta) + (0.5 * theta * Math.Log(-Math.Log(x) / -Math.Log(y)))))
            * Math.Pow(y, n.Evaluate((1.0 / theta) + (0.5 * theta * Math.Log(-Math.Log(y) / -Math.Log(x)))));
        Assert.Equal(quantLib, new HuslerReissCopula(theta).Evaluate(x, y));
    }

    private static Func<double, double, double> Make(string copula)
    {
        string[] p = copula.Split(' ');
        double At(int i) => double.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
        return p[0] switch
        {
            "AliMikhailHaq" => new AliMikhailHaqCopula(At(1)).Evaluate,
            "Clayton" => new ClaytonCopula(At(1)).Evaluate,
            "FarlieGumbelMorgenstern" => new FarlieGumbelMorgensternCopula(At(1)).Evaluate,
            "Frank" => new FrankCopula(At(1)).Evaluate,
            "Galambos" => new GalambosCopula(At(1)).Evaluate,
            "Gaussian" => new GaussianCopula(At(1)).Evaluate,
            "Gumbel" => new GumbelCopula(At(1)).Evaluate,
            "HuslerReiss" => new HuslerReissCopula(At(1)).Evaluate,
            "Independent" => new IndependentCopula().Evaluate,
            "MarshallOlkin" => new MarshallOlkinCopula(At(1), At(2)).Evaluate,
            "Max" => new MaxCopula().Evaluate,
            "Min" => new MinCopula().Evaluate,
            "Plackett" => new PlackettCopula(At(1)).Evaluate,
            _ => throw new InvalidOperationException(copula),
        };
    }
}
