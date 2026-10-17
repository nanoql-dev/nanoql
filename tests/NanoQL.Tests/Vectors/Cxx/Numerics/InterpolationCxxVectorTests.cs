// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="Interpolation"/>'s and the factories' harness vectors, <c>tests/vectors/cxx/math/interpolation.json</c>:
/// <see cref="Interpolation.XMin"/>, <see cref="Interpolation.XMax"/>, <see cref="Interpolation.XValues"/>,
/// <see cref="Interpolation.YValues"/>, <see cref="Interpolation.IsInRange"/> and extrapolation set on the object; the
/// constructors' refusals; each factory's <c>Interpolate</c>, the zero value of <see cref="Cubic"/> for QuantLib's
/// <c>Cubic()</c> and the presets converted to <see cref="LogCubic"/>; and the factories' traits.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolationCxxVectorTests
{
    [Fact]
    public void InterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("outside", out JsonElement outside))
            {
                CheckAccessors(check, c, InterpolationInputs.Build(c.Input("interpolation")), InterpolationInputs.Numbers(outside));
            }
            else if (c.Inputs.TryGetProperty("at", out JsonElement at))
            {
                // The constructor's case, read at its first node and inside each interval, each read building it anew,
                // so that a refusal is checked at every point.
                check.Output(
                    c,
                    "value",
                    InterpolationInputs.Deferred(
                        InterpolationInputs.Numbers(at), x => InterpolationInputs.Build(c.Input("interpolation")).Evaluate(x, true)));
            }
            else if (c.Inputs.TryGetProperty("method", out JsonElement method))
            {
                double[] x = InterpolationInputs.Numbers(c.Input("x")), y = InterpolationInputs.Numbers(c.Input("y"));
                double[] points = InterpolationInputs.Numbers(c.Input("points"));
                JsonElement? interpolator = c.Inputs.TryGetProperty("interpolator", out JsonElement spec) ? spec : null;
                check.Output(c, "values", (Func<object?>)(() =>
                {
                    Interpolation f = Interpolate(method.GetString()!, interpolator, x, y);
                    return points.Select(p => f.Evaluate(p, true)).ToArray();
                }));
            }
            else
            {
                (bool Global, int RequiredPoints)[] traits =
                [
                    Traits<Linear>(), Traits<BackwardFlat>(), Traits<LogLinear>(), Traits<Cubic>(), Traits<LogCubic>(),
                    Traits<MonotonicLogCubic>(), Traits<KrugerLog>(),
                ];
                check.Output(c, "global", traits.Select(t => t.Global).ToArray());
                check.Output(c, "requiredPoints", traits.Select(t => t.RequiredPoints).ToArray());
            }
        }

        check.Complete();
    }

    // The static traits, read as a consumer generic over the factory reads them.
    private static (bool Global, int RequiredPoints) Traits<TInterpolator>()
        where TInterpolator : struct, IInterpolator =>
        (TInterpolator.Global, TInterpolator.RequiredPoints);

    private static void CheckAccessors(VectorAssert check, VectorCase c, Interpolation f, double[] outside)
    {
        double[] points = InterpolationInputs.Numbers(c.Input("points"));
        check.Output(c, "xMin", f.XMin);
        check.Output(c, "xMax", f.XMax);
        check.Output(c, "xValues", f.XValues());
        check.Output(c, "yValues", f.YValues());
        check.Output(c, "isInRange", points.Select(f.IsInRange).ToArray());
        check.Output(c, "enableExtrapolation", InterpolationInputs.Deferred(outside, p => f.EnableExtrapolation().Evaluate(p)));
        check.Output(c, "enableExtrapolationFalse", InterpolationInputs.Deferred(outside, p => f.EnableExtrapolation(false).Evaluate(p)));
        check.Output(
            c,
            "disableExtrapolation",
            InterpolationInputs.Deferred(outside, p => f.EnableExtrapolation().DisableExtrapolation().Evaluate(p)));
        check.Output(
            c,
            "disableExtrapolationFalse",
            InterpolationInputs.Deferred(outside, p => f.EnableExtrapolation().DisableExtrapolation(false).Evaluate(p)));
    }

    // A factory's interpolation, by the harness method's name and the interpolator spec.
    private static Interpolation Interpolate(string method, JsonElement? interpolator, double[] x, double[] y) =>
        method switch
        {
            "Linear::interpolate" => new Linear().Interpolate(x, y),
            "BackwardFlat::interpolate" => new BackwardFlat().Interpolate(x, y),
            "LogLinear::interpolate" => new LogLinear().Interpolate(x, y),
            "Cubic::interpolate" => CubicFactory(interpolator!.Value).Interpolate(x, y),
            "LogCubic::interpolate" => LogCubicFactory(interpolator!.Value).Interpolate(x, y),
            "MonotonicLogCubic::interpolate" => new MonotonicLogCubic().Interpolate(x, y),
            "KrugerLog::interpolate" => new KrugerLog().Interpolate(x, y),
            _ => throw new InvalidOperationException($"no factory method {method}"),
        };

    // {"type": "Cubic"} alone is QuantLib's Cubic(), which the zero value is.
    private static Cubic CubicFactory(JsonElement spec) =>
        spec.TryGetProperty("derivativeApprox", out _)
            ? new Cubic(
                InterpolationInputs.DerivativeApprox(spec),
                spec.GetProperty("monotonic").GetBoolean(),
                InterpolationInputs.Condition(spec, "leftCondition"),
                InterpolationInputs.Number(spec.GetProperty("leftConditionValue")),
                InterpolationInputs.Condition(spec, "rightCondition"),
                InterpolationInputs.Number(spec.GetProperty("rightConditionValue")))
            : default;

    // A LogCubic with its derivative approximation alone (its defaults for the rest) or all six parameters, or a preset
    // converted to the LogCubic it is.
    private static LogCubic LogCubicFactory(JsonElement spec) => spec.GetProperty("type").GetString() switch
    {
        "MonotonicLogCubic" => new MonotonicLogCubic(),
        "KrugerLog" => new KrugerLog(),
        _ when spec.TryGetProperty("monotonic", out _) => new LogCubic(
            InterpolationInputs.DerivativeApprox(spec),
            spec.GetProperty("monotonic").GetBoolean(),
            InterpolationInputs.Condition(spec, "leftCondition"),
            InterpolationInputs.Number(spec.GetProperty("leftConditionValue")),
            InterpolationInputs.Condition(spec, "rightCondition"),
            InterpolationInputs.Number(spec.GetProperty("rightConditionValue"))),
        _ => new LogCubic(InterpolationInputs.DerivativeApprox(spec)),
    };
}
