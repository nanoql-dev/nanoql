// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's SABR interpolation vectors, <c>tests/vectors/cxx/math/interpolations/sabrinterpolation.json</c>:
/// the members of <see cref="SABRInterpolation"/> the wheel doesn't bind, <see cref="SABRInterpolation.Expiry"/>,
/// <see cref="SABRInterpolation.Forward"/>, <see cref="SABRInterpolation.EndCriteria"/>,
/// <see cref="SABRInterpolation.InterpolationWeights"/>, the errors and the refused primitive and derivatives; and the
/// <see cref="SABR"/> factory's fits, its <see cref="SABR.Global"/> and its refusals.
/// </summary>
/// <remarks>
/// An output the stability filter dropped isn't in the case's <c>expected</c>, and a value it dropped isn't among its
/// points, so each fitted output is compared only where the file holds it.
/// </remarks>
[Trait("Oracle", "QuantLib")]
public class SABRInterpolationCxxVectorTests
{
    [Fact]
    public void SABRInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/sabrinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "fit":
                    CheckFit(check, c, SABRInterpolationVectorTests.InterpolationOf(c.Input("interpolation")));
                    break;
                case "factory":
                    Interpolation f = Interpolate(c);
                    check.Output(
                        c, "values", VolatilityInputs.Reals(c.Input("points")).Select(x => (object?)f.Evaluate(x, true)).ToArray());
                    break;
                case "global":
                    check.Output(c, "global", SABR.Global);
                    break;
                case "refusal":
                    check.Output(c, "values", () => Interpolate(c).Evaluate(VolatilityInputs.Reals(c.Input("points"))[0], true));
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckFit(VectorAssert check, VectorCase c, SABRInterpolation s)
    {
        double at = c.Double("at");
        check.Output(c, "expiry", s.Expiry);
        check.Output(c, "forward", s.Forward);
        if (c.Expected.TryGetProperty("endCriteria", out _))
        {
            check.Output(c, "endCriteria", s.EndCriteria);
        }

        if (c.Expected.TryGetProperty("rmsError", out _))
        {
            check.Output(c, "rmsError", s.RmsError);
        }

        if (c.Expected.TryGetProperty("maxError", out _))
        {
            check.Output(c, "maxError", s.MaxError);
        }

        check.Output(c, "interpolationWeights", s.InterpolationWeights());
        check.Output(c, "primitive", () => s.Primitive(at, true));
        check.Output(c, "derivative", () => s.Derivative(at, true));
        check.Output(c, "secondDerivative", () => s.SecondDerivative(at, true));
    }

    // The factory's interpolation of a case's data, its points checked first, as a generic caller of a factory checks
    // them.
    private static Interpolation Interpolate(VectorCase c)
    {
        SABR factory = FactoryOf(c.Input("factory"));
        double[] x = VolatilityInputs.Reals(c.Input("x"));
        factory.CheckPoints(x.Length);
        return factory.Interpolate(x, VolatilityInputs.Reals(c.Input("y")));
    }

    // The factory a spec describes: the interpolation's arguments but the data and the volatility type.
    private static SABR FactoryOf(JsonElement spec) =>
        new(
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
            SABRInterpolationVectorTests.EndCriteriaOf(spec.GetProperty("endCriteria")),
            SABRInterpolationVectorTests.MethodOf(spec.GetProperty("method")),
            IndexInputs.Real(spec.GetProperty("errorAccept")),
            spec.GetProperty("useMaxError").GetBoolean(),
            spec.GetProperty("maxGuesses").GetInt32(),
            IndexInputs.Real(spec.GetProperty("shift")));
}
