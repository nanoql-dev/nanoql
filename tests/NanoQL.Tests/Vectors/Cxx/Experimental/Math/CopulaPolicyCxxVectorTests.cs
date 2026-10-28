// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's copula policy vectors, <c>tests/vectors/cxx/experimental/math/tcopulapolicy.json</c>: the
/// Gaussian and Student t policies' members, with the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CopulaPolicyCxxVectorTests
{
    [Fact]
    public void CopulaPolicyCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/math/tcopulapolicy");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name == "refused policies")
            {
                check.Output(c, "gaussian", Each(c.Input("gaussian").EnumerateArray(), w =>
                    GaussianCopulaPolicy.Create(Weights(w)).NumFactors));
                check.Output(c, "t", Each(c.Input("t").EnumerateArray(), p =>
                    TCopulaPolicy.Create(Weights(p[0]), PortfolioInputs.TraitsOf(p[1])).NumFactors));
                continue;
            }

            ImmutableArray<ImmutableArray<double>> w = Weights(c.Input("factorWeights"));
            if (c.Name.StartsWith("Gaussian", StringComparison.Ordinal))
            {
                GaussianCopulaPolicy g = GaussianCopulaPolicy.Create(w);
                check.Output(c, "members", Members(c, g, w.Length, w[0].Length, [], []));
            }
            else
            {
                TCopulaPolicy t = TCopulaPolicy.Create(w, PortfolioInputs.TraitsOf(c.Input("tOrders")));
                object[] orders = [.. t.GetInitTraits().TOrders.Select(x => (object)x)];
                object[] vf = [.. t.VarianceFactors.Select(x => (object)x)];
                check.Output(c, "members", Members(c, t, w.Length, w[0].Length, orders, vf));
            }
        }

        check.Complete();
    }

    private static object[] Members<TCopula, TInit>(VectorCase c, TCopula p, int n, int k, object[] orders, object[] vf)
        where TCopula : struct, ICopulaPolicy<TCopula, TInit>
        where TInit : struct
    {
        double[] values = [.. c.Input("values").EnumerateArray().Select(VectorCase.Real)];
        double[] probs = [.. c.Input("probabilities").EnumerateArray().Select(VectorCase.Real)];
        double[][] samples = PortfolioInputs.MatrixOf(c.Input("samples"));
        double[][] uniforms = PortfolioInputs.MatrixOf(c.Input("uniforms"));
        object[] cy = [.. values.SelectMany(v => Enumerable.Range(0, n).Select(i => (object)p.CumulativeY(v, i)))];
        object[] cz = [.. values.Select(v => (object)p.CumulativeZ(v))];
        object[] icy = [.. probs.SelectMany(q => Enumerable.Range(0, n).Select(i => (object)p.InverseCumulativeY(q, i)))];
        object[] icz = [.. probs.Select(q => (object)p.InverseCumulativeZ(q))];
        object[] icd = [.. probs.SelectMany(q => Enumerable.Range(0, k).Select(j => (object)p.InverseCumulativeDensity(q, j)))];
        object[] dens = [.. samples.Select(s => (object)p.Density(s.AsSpan(0, k)))];
        object[] inv = [.. uniforms.Select(u => (object)p.AllFactorCumulInverter(u).Select(x => (object)x).ToArray())];
        return [p.NumFactors, cy, cz, icy, icz, icd, dens, inv, orders, vf];
    }

    private static object[] Members(VectorCase c, GaussianCopulaPolicy p, int n, int k, object[] orders, object[] vf) =>
        Members<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(c, p, n, k, orders, vf);

    private static object[] Members(VectorCase c, TCopulaPolicy p, int n, int k, object[] orders, object[] vf) =>
        Members<TCopulaPolicy, TCopulaPolicyInitTraits>(c, p, n, k, orders, vf);

    private static ImmutableArray<ImmutableArray<double>> Weights(JsonElement w) =>
        [.. PortfolioInputs.MatrixOf(w).Select(r => r.ToImmutableArray())];

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
