// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The SABR interpolation vectors, <c>tests/vectors/math/interpolations/sabrinterpolation.json</c>: on QuantLib's
/// <c>testSabrInterpolation</c> fits and its single case, the defaults, one guess, the maximum error, a shift, normal
/// volatilities and forced restarts, the fitted parameters, the values and the errors that the stability filter keeps,
/// and the values outside the range without extrapolation; and the interpolations QuantLib refuses to build or fit.
/// </summary>
/// <remarks>
/// An output the filter dropped isn't in the case's <c>expected</c>, and a value it dropped isn't among its points, so
/// each output is compared only where the file holds it, and <see cref="VectorAssert.Complete"/> checks that every one
/// it holds was.
/// </remarks>
[Trait("Oracle", "QuantLib")]
public class SABRInterpolationVectorTests
{
    [Fact]
    public void SABRInterpolationVector()
    {
        var file = VectorFile.Load("math/interpolations/sabrinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "fit":
                    CheckFit(check, c, InterpolationOf(c.Inputs));
                    break;
                case "refusal":
                    check.Output(c, "interpolation", () => InterpolationOf(c.Inputs).Alpha);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    /// <summary>The interpolation a spec describes, fitted: a case's inputs, or a harness case's.</summary>
    /// <param name="spec">The spec, with the interpolation's arguments by name.</param>
    /// <returns>The interpolation.</returns>
    internal static SABRInterpolation InterpolationOf(JsonElement spec) =>
        new(
            VolatilityInputs.Reals(spec.GetProperty("x")),
            VolatilityInputs.Reals(spec.GetProperty("y")),
            IndexInputs.Real(spec.GetProperty("t")),
            IndexInputs.Real(spec.GetProperty("forward")),
            SmileSectionInputs.NullableReal(spec.GetProperty("alpha")),
            SmileSectionInputs.NullableReal(spec.GetProperty("beta")),
            SmileSectionInputs.NullableReal(spec.GetProperty("nu")),
            SmileSectionInputs.NullableReal(spec.GetProperty("rho")),
            spec.GetProperty("alphaIsFixed").GetBoolean(),
            spec.GetProperty("betaIsFixed").GetBoolean(),
            spec.GetProperty("nuIsFixed").GetBoolean(),
            spec.GetProperty("rhoIsFixed").GetBoolean(),
            spec.GetProperty("vegaWeighted").GetBoolean(),
            EndCriteriaOf(spec.GetProperty("endCriteria")),
            MethodOf(spec.GetProperty("method")),
            IndexInputs.Real(spec.GetProperty("errorAccept")),
            spec.GetProperty("useMaxError").GetBoolean(),
            spec.GetProperty("maxGuesses").GetInt32(),
            IndexInputs.Real(spec.GetProperty("shift")),
            (VolatilityType)spec.GetProperty("volatilityType").GetInt32());

    /// <summary>End criteria from a spec, or <see langword="null"/> for QuantLib's default.</summary>
    /// <param name="spec">The spec, or JSON null.</param>
    /// <returns>The end criteria.</returns>
    internal static EndCriteria? EndCriteriaOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : OptimizationSpecs.EndCriteria(spec);

    /// <summary>An optimizer from a spec, or <see langword="null"/> for QuantLib's default.</summary>
    /// <param name="spec">The spec, or JSON null.</param>
    /// <returns>The optimizer.</returns>
    internal static OptimizationMethod? MethodOf(JsonElement spec)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return spec.GetProperty("type").GetString() switch
        {
            "Simplex" => new Simplex(IndexInputs.Real(spec.GetProperty("lambda"))),
            "LevenbergMarquardt" => new LevenbergMarquardt(
                IndexInputs.Real(spec.GetProperty("epsfcn")),
                IndexInputs.Real(spec.GetProperty("xtol")),
                IndexInputs.Real(spec.GetProperty("gtol")),
                spec.GetProperty("useCostFunctionsJacobian").GetBoolean()),
            string type => throw new InvalidOperationException($"unknown optimizer {type}"),
            null => throw new InvalidOperationException("an optimizer spec without a type"),
        };
    }

    private static void CheckFit(VectorAssert check, VectorCase c, SABRInterpolation s)
    {
        // The outputs the stability filter kept.
        foreach ((string name, double value) in new[]
                 {
                     ("alpha", s.Alpha), ("beta", s.Beta), ("nu", s.Nu), ("rho", s.Rho), ("rmsError", s.RmsError),
                     ("maxError", s.MaxError),
                 })
        {
            if (c.Expected.TryGetProperty(name, out _))
            {
                check.Output(c, name, value);
            }
        }

        double[] evalAt = VolatilityInputs.Reals(c.Input("evalAt"));
        check.Output(c, "values", evalAt.Select(x => (object?)s.Evaluate(x, true)).ToArray());
        check.Output(
            c,
            "outsideRange",
            VolatilityInputs.Reals(c.Input("outside")).Select(x => (Func<object?>)(() => s.Evaluate(x))).ToArray());
    }
}
