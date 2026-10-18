// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The mixed-interpolation harness vectors, <c>tests/vectors/cxx/math/interpolations/mixedinterpolation.json</c>:
/// <see cref="MixedLinearCubicInterpolation"/> and its six named classes over their switch points and both behaviors,
/// with the value at 20 points and the derivative, second derivative and primitive at the first 8, or their
/// refusals; the <see cref="MixedLinearCubic"/> factory's interpolation, read back through its values; and its traits.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MixedInterpolationCxxVectorTests
{
    // The points the derivative, second derivative and primitive are read at, the first of each case's.
    private const int MemberPoints = 8;

    [Fact]
    public void MixedInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/mixedinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("interpolation", out _))
            {
                InterpolationInputs.CheckMembers(
                    check, c, () => InterpolationInputs.Build(c.Input("interpolation")), MemberPoints);
            }
            else if (c.Inputs.TryGetProperty("interpolator", out JsonElement spec))
            {
                double[] x = InterpolationInputs.Numbers(c.Input("x"));
                double[] y = InterpolationInputs.Numbers(c.Input("y"));
                double[] points = InterpolationInputs.Numbers(c.Input("points"));
                MixedLinearCubic factory = FactoryOf(spec);
                if (c.Expected.GetProperty("values").ValueKind == JsonValueKind.Array)
                {
                    Interpolation f = factory.Interpolate(x, y);
                    check.Output(c, "values", InterpolationInputs.Deferred(points, p => f.Evaluate(p, true)));
                }
                else
                {
                    check.Output(c, "values", () => factory.Interpolate(x, y).Evaluate(points[0], true));
                }
            }
            else
            {
                check.Output(c, "global", MixedLinearCubic.Global);
                check.Output(c, "requiredPoints", MixedLinearCubic.RequiredPoints);
            }
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"cxx/math/interpolations/mixedinterpolation: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }

    private static MixedLinearCubic FactoryOf(JsonElement spec) =>
        new(
            spec.GetProperty("n").GetInt32(),
            InterpolationInputs.Behavior(spec),
            InterpolationInputs.DerivativeApprox(spec),
            spec.GetProperty("monotonic").GetBoolean(),
            InterpolationInputs.Condition(spec, "leftCondition"),
            InterpolationInputs.NullableNumber(spec.GetProperty("leftConditionValue")),
            InterpolationInputs.Condition(spec, "rightCondition"),
            InterpolationInputs.Number(spec.GetProperty("rightConditionValue")));
}
