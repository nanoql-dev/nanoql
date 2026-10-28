// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's multidimensional integration vectors,
/// <c>tests/vectors/cxx/experimental/math/multidimquadrature.json</c>: the Gauss-Hermite rule's scalar integrals in one
/// to three dimensions and the trapezoid rule's on boxes.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MultidimQuadratureCxxVectorTests
{
    [Fact]
    public void MultidimQuadratureCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/math/multidimquadrature");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[][] gh = PortfolioInputs.MatrixOf(c.Input("gaussHermite"));
            double[][] trapezoid = PortfolioInputs.MatrixOf(c.Input("trapezoid"));
            string[] functions = [.. c.Input("functions").EnumerateArray().Select(f => f.GetString()!)];
            check.Output(c, "gaussHermite", Each(
                gh.SelectMany(g => functions.Select(f => (g, f))),
                p => GaussHermite(p.g, p.f)));
            check.Output(c, "gaussHermiteM0M1", Each(gh.Where(g => g[0] > 1), g => GaussHermite(g, "m0m1")));
            check.Output(c, "trapezoid", Each(
                trapezoid.SelectMany(t => functions.Select(f => (t, f))),
                p =>
                {
                    int dim = (int)p.t[0];
                    var integral = new MultidimIntegral(
                        Enumerable.Range(0, dim).Select(_ => (Integrator)new TrapezoidIntegral<Default>(p.t[3], (int)p.t[4])));
                    double[] a = [.. Enumerable.Repeat(p.t[1], dim)], b = [.. Enumerable.Repeat(p.t[2], dim)];
                    return integral.Integrate(LatentModelCxxVectorTests.Integrand(p.f), a, b);
                }));
        }

        check.Complete();
    }

    private static object[] GaussHermite(double[] g, string f)
    {
        var integrator = new GaussianQuadMultidimIntegrator((int)g[0], (int)g[1], g[2]);
        return [integrator.Integrate(LatentModelCxxVectorTests.Integrand(f)), integrator.Order];
    }

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
