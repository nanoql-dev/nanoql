// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's copula vectors, <c>tests/vectors/cxx/math/copulas/*.json</c>: each copula's values on interior
/// points, the rows of C(x, y), at parameters where QuantLib's formula holds; the edges, and the parameters where a fix
/// departs from QuantLib's formula, are the divergence facts'.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CopulaCxxVectorTests
{
    [Fact]
    public void AliMikhailHaqCopulaCxxVector() => Check("alimikhailhaqcopula", c => new AliMikhailHaqCopula(c.Double("theta")).Evaluate);

    [Fact]
    public void ClaytonCopulaCxxVector() => Check("claytoncopula", c => new ClaytonCopula(c.Double("theta")).Evaluate);

    [Fact]
    public void FarlieGumbelMorgensternCopulaCxxVector() =>
        Check("farliegumbelmorgensterncopula", c => new FarlieGumbelMorgensternCopula(c.Double("theta")).Evaluate);

    [Fact]
    public void FrankCopulaCxxVector() => Check("frankcopula", c => new FrankCopula(c.Double("theta")).Evaluate);

    [Fact]
    public void GalambosCopulaCxxVector() => Check("galamboscopula", c => new GalambosCopula(c.Double("theta")).Evaluate);

    [Fact]
    public void GaussianCopulaCxxVector() => Check("gaussiancopula", c => new GaussianCopula(c.Double("rho")).Evaluate);

    [Fact]
    public void GumbelCopulaCxxVector() => Check("gumbelcopula", c => new GumbelCopula(c.Double("theta")).Evaluate);

    [Fact]
    public void HuslerReissCopulaCxxVector() => Check("huslerreisscopula", c => new HuslerReissCopula(c.Double("theta")).Evaluate);

    [Fact]
    public void IndependentCopulaCxxVector() => Check("independentcopula", _ => new IndependentCopula().Evaluate);

    [Fact]
    public void MarshallOlkinCopulaCxxVector() =>
        Check("marshallolkincopula", c => new MarshallOlkinCopula(c.Double("a1"), c.Double("a2")).Evaluate);

    [Fact]
    public void MaxCopulaCxxVector() => Check("maxcopula", _ => new MaxCopula().Evaluate);

    [Fact]
    public void MinCopulaCxxVector() => Check("mincopula", _ => new MinCopula().Evaluate);

    [Fact]
    public void PlackettCopulaCxxVector() => Check("plackettcopula", c => new PlackettCopula(c.Double("theta")).Evaluate);

    private static void Check(string name, Func<VectorCase, Func<double, double, double>> copula)
    {
        var file = VectorFile.Load($"cxx/math/copulas/{name}");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Func<double, double, double> f = copula(c);
            double[] xs = c.Doubles("xs"), ys = c.Doubles("ys");
            check.Output(c, "values", xs.Select(x => ys.Select(y => f(x, y)).ToArray()).ToArray());
        }

        check.Complete();
    }
}
