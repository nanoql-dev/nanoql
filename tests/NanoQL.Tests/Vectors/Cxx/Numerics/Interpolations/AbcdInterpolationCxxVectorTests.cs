// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's abcd interpolation vectors, <c>tests/vectors/cxx/math/interpolations/abcdinterpolation.json</c>:
/// <see cref="AbcdInterpolation"/>'s fits through its factory <see cref="Abcd"/> with null, given and fixed guesses and
/// vega weights, their parameters, factors, errors, end criteria, values and interpolated factors where stable under
/// one-ulp moves of the inputs, the factory's traits, and the refusals; and <see cref="AbcdSquared"/>'s covariances.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AbcdInterpolationCxxVectorTests
{
    [Fact]
    public void AbcdInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/abcdinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "interpolation":
                    CheckInterpolation(check, c, InterpolationOf(c.Input("fit")));
                    break;
                case "refusal":
                    double x = c.Double("x");
                    check.Output(c, "value", () => InterpolationOf(c.Input("fit")).Evaluate(x, true));
                    break;
                case "traits":
                    check.Output(c, "global", Abcd.Global);
                    check.Output(c, "requiredPoints", Abcd.RequiredPoints);
                    break;
                case "squared":
                    double[] p = VolatilityInputs.Reals(c.Input("parameters"));
                    var f = new AbcdSquared(p[0], p[1], p[2], p[3], c.Double("T"), c.Double("S"));
                    check.Output(c, "value", VolatilityInputs.Reals(c.Input("times")).Select(f.Evaluate).ToArray());
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    // The interpolation a harness fit spec describes, through QuantLib's factory, a null guess left to the default;
    // the factory checks the points' count first, as an interpolated curve's build does.
    private static AbcdInterpolation InterpolationOf(JsonElement spec)
    {
        double?[] guess = [.. spec.GetProperty("guess").EnumerateArray().Select(CouponInputs.NullableReal)];
        bool[] fixedFlags = [.. spec.GetProperty("fixed").EnumerateArray().Select(x => x.GetBoolean())];
        double[] t = VolatilityInputs.Reals(spec.GetProperty("t"));
        var factory = new Abcd(
            guess[0],
            guess[1],
            guess[2],
            guess[3],
            fixedFlags[0],
            fixedFlags[1],
            fixedFlags[2],
            fixedFlags[3],
            spec.GetProperty("vegaWeighted").GetBoolean(),
            AbcdCalibrationCxxVectorTests.EndCriteriaOf(spec.GetProperty("endCriteria")),
            null);
        factory.CheckPoints(t.Length);
        return (AbcdInterpolation)factory.Interpolate(t, VolatilityInputs.Reals(spec.GetProperty("blackVols")));
    }

    private static void CheckInterpolation(VectorAssert check, VectorCase c, AbcdInterpolation f)
    {
        double[] t = VolatilityInputs.Reals(c.Input("fit").GetProperty("t"));
        double[] points = VolatilityInputs.Reals(c.Input("points"));
        double[] kPoints = VolatilityInputs.Reals(c.Input("kPoints"));
        Check(check, c, "a", () => f.A);
        Check(check, c, "b", () => f.B);
        Check(check, c, "c", () => f.C);
        Check(check, c, "d", () => f.D);
        Check(check, c, "k", () => f.K);
        Check(check, c, "rmsError", () => f.RmsError);
        Check(check, c, "maxError", () => f.MaxError);
        Check(check, c, "endCriteria", () => f.EndCriteria);
        Check(check, c, "value", () => points.Select(x => f.Evaluate(x, true)).ToArray());
        Check(check, c, "kAt", () => kPoints.Select(x => f.KAt(x, t)).ToArray());
    }

    private static void Check(VectorAssert check, VectorCase c, string name, Func<object?> value)
    {
        if (c.Expected.TryGetProperty(name, out _))
        {
            check.Output(c, name, value);
        }
    }
}
