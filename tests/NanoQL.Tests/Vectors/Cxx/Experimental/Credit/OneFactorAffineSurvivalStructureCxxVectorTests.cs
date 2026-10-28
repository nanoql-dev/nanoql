// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's one-factor affine survival structure vectors,
/// <c>tests/vectors/cxx/experimental/credit/onefactoraffinesurvival.json</c>:
/// <see cref="OneFactorAffineSurvivalStructure"/> on Vasicek and Cox-Ingersoll-Ross models, its survival probability,
/// default density, hazard rate and conditional survival probability.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OneFactorAffineSurvivalStructureCxxVectorTests
{
    [Fact]
    public void OneFactorAffineSurvivalStructureCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/onefactoraffinesurvival");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            var structure = new OneFactorAffineSurvivalStructure(ModelOf(a[0]), new Date(a[1].GetInt32()), new Actual365Fixed());
            CheckReads(check, c, structure, a[2], a[3]);
        }

        check.Complete();
    }

    // A model given as ["Vasicek", r0, a, b, sigma] or ["CIR", r0, theta, k, sigma].
    internal static OneFactorAffineModel ModelOf(JsonElement spec)
    {
        double[] p = [.. spec.EnumerateArray().Skip(1).Select(VectorCase.Real)];
        return spec[0].GetString() == "Vasicek"
            ? new Vasicek(p[0], p[1], p[2], p[3])
            : new CoxIngersollRoss(p[0], p[1], p[2], p[3]);
    }

    // The structure's reads at each time, extrapolated, and its conditional survival at each [tFwd, tTarget, y].
    internal static void CheckReads(VectorAssert check, VectorCase c, OneFactorAffineSurvivalStructure s, JsonElement times, JsonElement conditionals)
    {
        double[] ts = [.. times.EnumerateArray().Select(VectorCase.Real)];
        check.Output(c, "survivalProbability", ts.Select(t => Lazy(() => s.SurvivalProbability(t, true))).ToArray());
        check.Output(c, "defaultDensity", ts.Select(t => Lazy(() => s.DefaultDensity(t, true))).ToArray());
        check.Output(c, "hazardRate", ts.Select(t => Lazy(() => s.HazardRate(t, true))).ToArray());
        check.Output(
            c,
            "conditionalSurvivalProbability",
            conditionals.EnumerateArray()
                .Select(x => x.EnumerateArray().Select(VectorCase.Real).ToArray())
                .Select(x => Lazy(() => s.ConditionalSurvivalProbability(x[0], x[1], x[2], true)))
                .ToArray());
    }

    // A read the comparison runs, refusals included.
    private static Func<object?> Lazy(Func<double> read) => () => read();
}
