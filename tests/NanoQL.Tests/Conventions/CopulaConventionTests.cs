// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The copulas' conventions: QuantLib's checks of their parameters and arguments, its texts, as
/// <see cref="ArgumentException"/>s naming the parameter; Marshall-Olkin's parameters above 1 refused, where QuantLib's
/// accepts them and returns values above the Fréchet-Hoeffding bound; and every copula between the Fréchet-Hoeffding
/// bounds, max(u + v − 1, 0) ≤ C(u, v) ≤ min(u, v), and increasing in each argument, on a grid (§2.6's second route,
/// independent of QuantLib).
/// </summary>
public class CopulaConventionTests
{
    private static readonly double[] Grid = [0.0, 0.01, 0.1, 0.25, 0.4, 0.5, 0.6, 0.75, 0.9, 0.99, 1.0];

    public static TheoryData<string, double> Parameterized() => new()
    {
        { "AliMikhailHaq", -1.0 }, { "AliMikhailHaq", 0.5 }, { "AliMikhailHaq", 1.0 },
        { "Clayton", -1.0 }, { "Clayton", -0.5 }, { "Clayton", 1e-6 }, { "Clayton", 2.0 }, { "Clayton", 100.0 },
        { "FarlieGumbelMorgenstern", -1.0 }, { "FarlieGumbelMorgenstern", 1.0 },
        { "Frank", -2000.0 }, { "Frank", -10.0 }, { "Frank", 1e-9 }, { "Frank", 10.0 }, { "Frank", 2000.0 },
        { "Galambos", 0.0 }, { "Galambos", 3.0 }, { "Galambos", 1e4 },
        { "Gaussian", -1.0 }, { "Gaussian", -0.5 }, { "Gaussian", 0.9 }, { "Gaussian", 1.0 },
        { "Gumbel", 1.0 }, { "Gumbel", 3.0 }, { "Gumbel", 1e4 },
        { "HuslerReiss", 0.0 }, { "HuslerReiss", 1.0 }, { "HuslerReiss", 50.0 },
        { "Plackett", 0.0 }, { "Plackett", 0.5 }, { "Plackett", 1.000001 }, { "Plackett", 1e6 },
    };

    [Theory]
    [MemberData(nameof(Parameterized))]
    public void EveryCopulaLiesBetweenTheFrechetHoeffdingBoundsAndIncreases(string name, double parameter)
    {
        Func<double, double, double> c = Make(name, parameter);

        // the Gaussian copula reads the inverse normal's approximation, accurate to about 1e-9 (QuantLib's design,
        // kept); the others to rounding
        double tolerance = name == "Gaussian" ? 2e-9 : 1e-15;
        foreach (double u in Grid)
        {
            for (int j = 0; j < Grid.Length; j++)
            {
                double v = Grid[j], value = c(u, v);
                double lower = Math.Max(u + v - 1.0, 0.0), upper = Math.Min(u, v);
                Assert.True(
                    value >= lower - tolerance && value <= upper + tolerance, $"{name} {parameter} at ({u}, {v}): {value}");
                if (j > 0)
                {
                    Assert.True(value >= c(u, Grid[j - 1]) - tolerance, $"{name} {parameter} decreases at ({u}, {v})");
                }
            }
        }
    }

    [Fact]
    public void TheParameterlessCopulasAreTheBoundsAndIndependence()
    {
        foreach (double u in Grid)
        {
            foreach (double v in Grid)
            {
                // the edges' boundary values exact, the formulas within their rounding
                Assert.Equal(Math.Min(u, v), new MaxCopula().Evaluate(u, v), 1e-15);
                Assert.Equal(Math.Max(u + v - 1.0, 0.0), new MinCopula().Evaluate(u, v), 1e-15);
                Assert.Equal(u * v, new IndependentCopula().Evaluate(u, v), 1e-15);
            }
        }
    }

    [Fact]
    public void TheParametersAndArgumentsAreQuantLibs()
    {
        Refused(() => new AliMikhailHaqCopula(1.5), "theta", "theta (1.5) must be in [-1,1]");
        Refused(() => new ClaytonCopula(-2.0), "theta", "theta (-2) must be greater or equal to -1");
        Refused(() => new ClaytonCopula(0.0), "theta", "theta (0) must be different from 0");
        Refused(() => new FarlieGumbelMorgensternCopula(-1.5), "theta", "theta (-1.5) must be in [-1,1]");
        Refused(() => new FrankCopula(0.0), "theta", "theta (0) must be different from 0");
        Refused(() => new GalambosCopula(-1.0), "theta", "theta (-1) must be greater or equal to 0");
        Refused(() => new GumbelCopula(0.5), "theta", "theta (0.5) must be greater or equal to 1");
        Refused(() => new HuslerReissCopula(-1.0), "theta", "theta (-1) must be greater or equal to 0");
        Refused(() => new PlackettCopula(-1.0), "theta", "theta (-1) must be greater or equal to 0");
        Refused(() => new PlackettCopula(1.0), "theta", "theta (1) must be different from 1");
        Refused(() => new MarshallOlkinCopula(-0.1, 0.5), "a1", "1st parameter (-0.1) must be non-negative");
        Refused(() => new MarshallOlkinCopula(0.5, -0.1), "a2", "2nd parameter (-0.1) must be non-negative");

        // the Gaussian copula's: the bivariate normal's check first, QuantLib's member order
        Refused(() => new GaussianCopula(1.5), "rho", "rho must be <= 1.0 (1.5 not allowed)");

        // every copula's arguments, NaN refused too
        Func<double, double, double>[] all =
        [
            new AliMikhailHaqCopula(0.5).Evaluate, new ClaytonCopula(2.0).Evaluate,
            new FarlieGumbelMorgensternCopula(0.5).Evaluate, new FrankCopula(2.0).Evaluate, new GalambosCopula(2.0).Evaluate,
            new GaussianCopula(0.5).Evaluate, new GumbelCopula(2.0).Evaluate, new HuslerReissCopula(1.0).Evaluate,
            new IndependentCopula().Evaluate, new MarshallOlkinCopula(0.3, 0.6).Evaluate, new MaxCopula().Evaluate,
            new MinCopula().Evaluate, new PlackettCopula(2.0).Evaluate,
        ];
        foreach (Func<double, double, double> c in all)
        {
            Refused(() => c(1.5, 0.5), "x", "1st argument (1.5) must be in [0,1]");
            Refused(() => c(0.5, -0.5), "y", "2nd argument (-0.5) must be in [0,1]");
            Refused(() => c(double.NaN, 0.5), "x", "1st argument (NaN) must be in [0,1]");
        }
    }

    [Fact]
    public void MarshallOlkinsParametersAboveOneAreRefused()
    {
        // QuantLib accepts them and, at (2, 2), C(0.2, 0.9) is 0.22222222222222224, above min(x, y) = 0.2, the
        // Fréchet-Hoeffding bound every copula meets
        Refused(() => new MarshallOlkinCopula(2.0, 0.5), "a1", "1st parameter (2) must not exceed 1");
        Refused(() => new MarshallOlkinCopula(0.5, 2.0), "a2", "2nd parameter (2) must not exceed 1");
        Assert.True(new MarshallOlkinCopula(1.0, 1.0).Evaluate(0.2, 0.9) <= 0.2);
    }

    private static void Refused(Action action, string paramName, string message)
    {
        ArgumentException e = Assert.ThrowsAny<ArgumentException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message);
    }

    private static Func<double, double, double> Make(string name, double p) => name switch
    {
        "AliMikhailHaq" => new AliMikhailHaqCopula(p).Evaluate,
        "Clayton" => new ClaytonCopula(p).Evaluate,
        "FarlieGumbelMorgenstern" => new FarlieGumbelMorgensternCopula(p).Evaluate,
        "Frank" => new FrankCopula(p).Evaluate,
        "Galambos" => new GalambosCopula(p).Evaluate,
        "Gaussian" => new GaussianCopula(p).Evaluate,
        "Gumbel" => new GumbelCopula(p).Evaluate,
        "HuslerReiss" => new HuslerReissCopula(p).Evaluate,
        "Plackett" => new PlackettCopula(p).Evaluate,
        _ => throw new InvalidOperationException(name),
    };
}
