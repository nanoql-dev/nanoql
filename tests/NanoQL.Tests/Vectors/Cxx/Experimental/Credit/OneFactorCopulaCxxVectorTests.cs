// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's one-factor copula vectors, <c>tests/vectors/cxx/experimental/credit/onefactorcopula.json</c>: the
/// moments' check, the bucketed loss distribution integrated over the factor and the integral of the conditional
/// probabilities' sum, on the four forms; and the members every form's vectors read.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OneFactorCopulaCxxVectorTests
{
    [Fact]
    public void OneFactorCopulaCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/onefactorcopula");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("copula");
            OneFactorCopula copula = PortfolioInputs.OneFactorCopulaOf(spec);

            // a Student factor's range isn't QuantLib's [-5, 10]: those outputs are null
            bool gaussian = copula is OneFactorGaussianCopula;
            check.Output(c, "checkMoments", Each(c.Input("tolerances").EnumerateArray(), t => gaussian ? copula.CheckMoments(VectorCase.Real(t)) : null));
            JsonElement[] integrals = [.. c.Input("integrals").EnumerateArray()];
            check.Output(c, "integral", Each(integrals, i => gaussian ? DistributionCxxVectorTests.Summary(
                copula.Integral(new LossDistBucketing(i[0].GetInt32(), VectorCase.Real(i[1])), Reals(i[2]), Reals(i[3]))) : null));
            check.Output(c, "integralOfSum", Each(integrals, i => gaussian ? copula.Integral(p => p.Sum(), Reals(i[3])) : null));
        }

        check.Complete();
    }

    /// <summary>Checks a vector file of a copula's members, as the harness's <c>OneFactorCopula::members</c> lists
    /// them.</summary>
    /// <param name="name">The vector file's name.</param>
    internal static void CheckMembers(string name)
    {
        var file = VectorFile.Load(name);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            OneFactorCopula copula = PortfolioInputs.OneFactorCopulaOf(c.Input("copula"));
            double[] ms = Reals(c.Input("ms")), zs = Reals(c.Input("zs")), ys = Reals(c.Input("ys")), ps = Reals(c.Input("ps"));
            double[] probs = Reals(c.Input("probabilities"));
            var gaussian = copula as OneFactorGaussianCopula;
            check.Output(c, "members", new object?[]
            {
                ms.Select(copula.Density).ToArray(),
                zs.Select(copula.CumulativeZ).ToArray(),
                ys.Select(copula.CumulativeY).ToArray(),
                ps.Select(copula.InverseCumulativeY).ToArray(),
                probs.SelectMany(p => ms.Select(m => copula.ConditionalProbability(p, m))).ToArray(),
                ms.Select(m => copula.ConditionalProbability(probs, m)).ToArray(),
                gaussian is null ? null : probs.Select(copula.Integral).ToArray(),
                ys.Select(y => gaussian is null ? (object?)null : gaussian.TestCumulativeY(y)).ToArray(),
            });
        }

        check.Complete();
    }

    private static double[] Reals(JsonElement e) => [.. e.EnumerateArray().Select(VectorCase.Real)];

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
