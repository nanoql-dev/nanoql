// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="CubicInterpolation"/>'s harness vectors, <c>tests/vectors/cxx/math/interpolations/cubicinterpolation.json</c>:
/// the constructor over the derivative approximations and boundary conditions, with the value, derivatives, primitive
/// and the coefficient accessors; <see cref="CubicInterpolation.UpdateLeftConditionValue"/> and
/// <see cref="CubicInterpolation.UpdateRightConditionValue"/>; the named classes the wheel lacks; and the edges, every
/// configuration QuantLib defines on 2 and on 4 points and the monotonic spline's condition pairs on 8.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CubicInterpolationCxxVectorTests
{
    private static readonly string[] Members = ["value", "derivative", "secondDerivative", "primitive"];

    [Fact]
    public void CubicInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/cubicinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("conditions", out JsonElement conditions))
            {
                CheckEdges(check, c, conditions);
                continue;
            }

            JsonElement spec = c.Input("interpolation");
            Interpolation Build() => InterpolationInputs.Build(spec);
            if (c.Inputs.TryGetProperty("update", out JsonElement side))
            {
                CheckUpdate(check, c, Build, side.GetString()!);
                continue;
            }

            InterpolationInputs.CheckMembers(check, c, Build);
            if (c.Expected.TryGetProperty("aCoefficients", out _))
            {
                // Each accessor computed when compared, so that a throw is checked.
                CubicInterpolation Cubic() => (CubicInterpolation)Build();
                check.Output(c, "primitiveConstants", (Func<object?>)(() => Cubic().PrimitiveConstants()));
                check.Output(c, "aCoefficients", (Func<object?>)(() => Cubic().ACoefficients()));
                check.Output(c, "bCoefficients", (Func<object?>)(() => Cubic().BCoefficients()));
                check.Output(c, "cCoefficients", (Func<object?>)(() => Cubic().CCoefficients()));
                check.Output(c, "monotonicityAdjustments", (Func<object?>)(() => Cubic().MonotonicityAdjustments()));
            }
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"cxx/math/interpolations/cubicinterpolation: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }

    // The edges: one size, approximation and filter, an entry per condition pair of each output, the four members at
    // the interior points and the five accessors. A pair QuantLib refuses holds a throw in each output, checked against
    // nanoql's construction, built anew.
    private static void CheckEdges(VectorAssert check, VectorCase c, JsonElement conditions)
    {
        double[] x = InterpolationInputs.Numbers(c.Input("x")), y = InterpolationInputs.Numbers(c.Input("y"));
        double[] points = InterpolationInputs.Numbers(c.Input("points"));
        var da = (CubicInterpolationDerivativeApprox)c.Int("derivativeApprox");
        bool monotonic = c.Bool("monotonic");
        Func<CubicInterpolation>[] builds =
        [
            .. conditions.EnumerateArray().Select(pair =>
            {
                JsonElement[] p = [.. pair.EnumerateArray()];
                var left = (CubicInterpolationBoundaryCondition)p[0].GetInt32();
                var right = (CubicInterpolationBoundaryCondition)p[2].GetInt32();
                double leftValue = InterpolationInputs.Number(p[1]), rightValue = InterpolationInputs.Number(p[3]);
                return (Func<CubicInterpolation>)(() => new CubicInterpolation(x, y, da, monotonic, left, leftValue, right, rightValue));
            }),
        ];
        CubicInterpolation?[] built = [.. builds.Select(TryBuild)];

        // Each pair's entry: the interpolation's values, or, where nanoql refuses, the refusal itself.
        object?[] Entries(Func<CubicInterpolation, object?> read) =>
            [.. Enumerable.Range(0, builds.Length).Select(i => built[i] is CubicInterpolation f
                ? read(f)
                : (object?)(Func<object?>)(() => read(builds[i]())))];

        object?[] AtPoints(CubicInterpolation f, Func<CubicInterpolation, double, double> member) =>
            InterpolationInputs.Deferred(points, p => member(f, p));

        check.Output(c, "value", Entries(f => AtPoints(f, (g, p) => g.Evaluate(p))));
        check.Output(c, "derivative", Entries(f => AtPoints(f, (g, p) => g.Derivative(p))));
        check.Output(c, "secondDerivative", Entries(f => AtPoints(f, (g, p) => g.SecondDerivative(p))));
        check.Output(c, "primitive", Entries(f => AtPoints(f, (g, p) => g.Primitive(p))));
        check.Output(c, "primitiveConstants", Entries(f => f.PrimitiveConstants()));
        check.Output(c, "aCoefficients", Entries(f => f.ACoefficients()));
        check.Output(c, "bCoefficients", Entries(f => f.BCoefficients()));
        check.Output(c, "cCoefficients", Entries(f => f.CCoefficients()));
        check.Output(c, "monotonicityAdjustments", Entries(f => f.MonotonicityAdjustments()));
    }

    private static CubicInterpolation? TryBuild(Func<CubicInterpolation> build)
    {
        try
        {
            return build();
        }
        catch (Exception e) when (e is ArgumentException or NanoQLException)
        {
            return null;
        }
    }

    // The update on an interpolation with extrapolation enabled, read at the points without the call's flag.
    private static void CheckUpdate(VectorAssert check, VectorCase c, Func<Interpolation> build, string side)
    {
        double value = c.Double("value");
        double[] points = InterpolationInputs.Numbers(c.Input("points"));
        CubicInterpolation Updated()
        {
            CubicInterpolation f = ((CubicInterpolation)build()).EnableExtrapolation();
            return side == "left" ? f.UpdateLeftConditionValue(value) : f.UpdateRightConditionValue(value);
        }

        if (c.Expected.GetProperty("value").ValueKind != JsonValueKind.Array)
        {
            foreach (string member in Members)
            {
                check.Output(c, member, (Func<object?>)(() => Updated().Evaluate(points[0])));
            }

            return;
        }

        CubicInterpolation updated = Updated();
        check.Output(c, "value", InterpolationInputs.Deferred(points, p => updated.Evaluate(p)));
        check.Output(c, "derivative", InterpolationInputs.Deferred(points, p => updated.Derivative(p)));
        check.Output(c, "secondDerivative", InterpolationInputs.Deferred(points, p => updated.SecondDerivative(p)));
        check.Output(c, "primitive", InterpolationInputs.Deferred(points, p => updated.Primitive(p)));
    }
}
