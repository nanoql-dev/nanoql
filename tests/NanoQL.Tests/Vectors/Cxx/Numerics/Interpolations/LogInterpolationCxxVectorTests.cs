// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The log-cubic harness vectors, <c>tests/vectors/cxx/math/interpolations/loginterpolation.json</c>:
/// <see cref="LogCubicInterpolation"/>'s constructor over the derivative approximations and some boundary conditions,
/// and <see cref="HarmonicLogCubic"/>, with the value, derivatives and primitive (which throws), and their refusal of a
/// y value of 0; then the LogMixedLinearCubic family: <see cref="LogMixedLinearCubicInterpolation"/> and
/// <see cref="LogMixedLinearCubicNaturalSpline"/> read likewise, the <see cref="LogMixedLinearCubic"/> factory and
/// its presets read back through their values, and the three factories' traits.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LogInterpolationCxxVectorTests
{
    [Fact]
    public void LogInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/loginterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("factory", out JsonElement spec))
            {
                // A LogMixedLinearCubic factory or one of its presets, read back through its values.
                double[] x = InterpolationInputs.Numbers(c.Input("x"));
                double[] y = InterpolationInputs.Numbers(c.Input("y"));
                Interpolation f = FactoryOf(spec, x, y);
                check.Output(
                    c, "values", InterpolationInputs.Deferred(InterpolationInputs.Numbers(c.Input("points")), p => f.Evaluate(p, true)));
            }
            else if (c.Inputs.TryGetProperty("factories", out _))
            {
                check.Output(
                    c,
                    "global",
                    new object?[] { LogMixedLinearCubic.Global, MonotonicLogMixedLinearCubic.Global, KrugerLogMixedLinearCubic.Global });
                check.Output(
                    c,
                    "requiredPoints",
                    new object?[]
                    {
                        LogMixedLinearCubic.RequiredPoints, MonotonicLogMixedLinearCubic.RequiredPoints,
                        KrugerLogMixedLinearCubic.RequiredPoints,
                    });
            }
            else
            {
                InterpolationInputs.CheckMembers(check, c, () => InterpolationInputs.Build(c.Input("interpolation")));
            }
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"cxx/math/interpolations/loginterpolation: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }

    // Each factory's own Interpolate: a preset's, not the LogMixedLinearCubic it converts to.
    private static Interpolation FactoryOf(JsonElement spec, double[] x, double[] y)
    {
        int n = spec.GetProperty("n").GetInt32();
        MixedInterpolationBehavior behavior = InterpolationInputs.Behavior(spec);
        return spec.GetProperty("type").GetString() switch
        {
            "LogMixedLinearCubic" => new LogMixedLinearCubic(
                n,
                behavior,
                InterpolationInputs.DerivativeApprox(spec),
                spec.GetProperty("monotonic").GetBoolean(),
                InterpolationInputs.Condition(spec, "leftCondition"),
                InterpolationInputs.NullableNumber(spec.GetProperty("leftConditionValue")),
                InterpolationInputs.Condition(spec, "rightCondition"),
                InterpolationInputs.Number(spec.GetProperty("rightConditionValue"))).Interpolate(x, y),
            "MonotonicLogMixedLinearCubic" => new MonotonicLogMixedLinearCubic(n, behavior).Interpolate(x, y),
            "KrugerLogMixedLinearCubic" => new KrugerLogMixedLinearCubic(n, behavior).Interpolate(x, y),
            var t => throw new InvalidOperationException($"unknown factory {t}"),
        };
    }
}
