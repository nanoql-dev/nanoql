// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interpolation vectors' inputs: an interpolation from its type and points, with
/// <see cref="CubicInterpolation"/>'s and <see cref="LogCubicInterpolation"/>'s parameters from the spec, and the
/// mixed interpolations' switch point, behavior and parameters; and the four members read at a case's points with its
/// extrapolation flags.
/// </summary>
internal static class InterpolationInputs
{
    /// <summary>A number as the vectors encode it: the non-finite ones as strings.</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The number.</returns>
    public static double Number(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? double.Parse(e.GetString()!, CultureInfo.InvariantCulture) : e.GetDouble();

    /// <summary>An array of numbers as the vectors encode them.</summary>
    /// <param name="e">The JSON array.</param>
    /// <returns>The numbers.</returns>
    public static double[] Numbers(JsonElement e) => [.. e.EnumerateArray().Select(Number)];

    /// <summary>An array of Booleans.</summary>
    /// <param name="e">The JSON array.</param>
    /// <returns>The values.</returns>
    public static bool[] Bools(JsonElement e) => [.. e.EnumerateArray().Select(b => b.GetBoolean())];

    /// <summary>The interpolation an interpolation spec <c>{"type", "x", "y", …}</c> describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The interpolation.</returns>
    public static Interpolation Build(JsonElement spec) =>
        Build(spec.GetProperty("type").GetString()!, Numbers(spec.GetProperty("x")), Numbers(spec.GetProperty("y")), spec);

    /// <summary>
    /// The interpolation of a type over the given points; <paramref name="spec"/> gives the parameters of
    /// <see cref="CubicInterpolation"/> and <see cref="LogCubicInterpolation"/>.
    /// </summary>
    /// <param name="type">The type, by QuantLib's name.</param>
    /// <param name="x">The x values.</param>
    /// <param name="y">The y values.</param>
    /// <param name="spec">The spec holding the parameters, for the two types that take them.</param>
    /// <returns>The interpolation.</returns>
    public static Interpolation Build(string type, double[] x, double[] y, JsonElement spec) => type switch
    {
        "LinearInterpolation" => new LinearInterpolation(x, y),
        "BackwardFlatInterpolation" => new BackwardFlatInterpolation(x, y),
        "LogLinearInterpolation" => new LogLinearInterpolation(x, y),
        "CubicInterpolation" => new CubicInterpolation(
            x,
            y,
            DerivativeApprox(spec),
            spec.GetProperty("monotonic").GetBoolean(),
            Condition(spec, "leftCondition"),
            Number(spec.GetProperty("leftConditionValue")),
            Condition(spec, "rightCondition"),
            Number(spec.GetProperty("rightConditionValue"))),
        "CubicNaturalSpline" => new CubicNaturalSpline(x, y),
        "MonotonicCubicNaturalSpline" => new MonotonicCubicNaturalSpline(x, y),
        "CubicSplineOvershootingMinimization1" => new CubicSplineOvershootingMinimization1(x, y),
        "CubicSplineOvershootingMinimization2" => new CubicSplineOvershootingMinimization2(x, y),
        "AkimaCubicInterpolation" => new AkimaCubicInterpolation(x, y),
        "KrugerCubic" => new KrugerCubic(x, y),
        "HarmonicCubic" => new HarmonicCubic(x, y),
        "FritschButlandCubic" => new FritschButlandCubic(x, y),
        "Parabolic" => new Parabolic(x, y),
        "MonotonicParabolic" => new MonotonicParabolic(x, y),
        "LogCubicInterpolation" => new LogCubicInterpolation(
            x,
            y,
            DerivativeApprox(spec),
            spec.GetProperty("monotonic").GetBoolean(),
            Condition(spec, "leftCondition"),
            Number(spec.GetProperty("leftConditionValue")),
            Condition(spec, "rightCondition"),
            Number(spec.GetProperty("rightConditionValue"))),
        "LogCubicNaturalSpline" => new LogCubicNaturalSpline(x, y),
        "MonotonicLogCubicNaturalSpline" => new MonotonicLogCubicNaturalSpline(x, y),
        "KrugerLogCubic" => new KrugerLogCubic(x, y),
        "HarmonicLogCubic" => new HarmonicLogCubic(x, y),
        "FritschButlandLogCubic" => new FritschButlandLogCubic(x, y),
        "LogParabolic" => new LogParabolic(x, y),
        "MonotonicLogParabolic" => new MonotonicLogParabolic(x, y),
        "MixedLinearCubicInterpolation" => new MixedLinearCubicInterpolation(
            x,
            y,
            spec.GetProperty("n").GetInt32(),
            Behavior(spec),
            DerivativeApprox(spec),
            spec.GetProperty("monotonic").GetBoolean(),
            Condition(spec, "leftCondition"),
            NullableNumber(spec.GetProperty("leftConditionValue")),
            Condition(spec, "rightCondition"),
            Number(spec.GetProperty("rightConditionValue"))),
        "MixedLinearCubicNaturalSpline" => new MixedLinearCubicNaturalSpline(x, y, spec.GetProperty("n").GetInt32(), Behavior(spec)),
        "MixedLinearMonotonicCubicNaturalSpline" =>
            new MixedLinearMonotonicCubicNaturalSpline(x, y, spec.GetProperty("n").GetInt32(), Behavior(spec)),
        "MixedLinearKrugerCubic" => new MixedLinearKrugerCubic(x, y, spec.GetProperty("n").GetInt32(), Behavior(spec)),
        "MixedLinearFritschButlandCubic" =>
            new MixedLinearFritschButlandCubic(x, y, spec.GetProperty("n").GetInt32(), Behavior(spec)),
        "MixedLinearParabolic" => new MixedLinearParabolic(x, y, spec.GetProperty("n").GetInt32(), Behavior(spec)),
        "MixedLinearMonotonicParabolic" =>
            new MixedLinearMonotonicParabolic(x, y, spec.GetProperty("n").GetInt32(), Behavior(spec)),
        "LogMixedLinearCubicInterpolation" => new LogMixedLinearCubicInterpolation(
            x,
            y,
            spec.GetProperty("n").GetInt32(),
            Behavior(spec),
            DerivativeApprox(spec),
            spec.GetProperty("monotonic").GetBoolean(),
            Condition(spec, "leftCondition"),
            NullableNumber(spec.GetProperty("leftConditionValue")),
            Condition(spec, "rightCondition"),
            Number(spec.GetProperty("rightConditionValue"))),
        "LogMixedLinearCubicNaturalSpline" =>
            new LogMixedLinearCubicNaturalSpline(x, y, spec.GetProperty("n").GetInt32(), Behavior(spec)),
        _ => throw new InvalidOperationException($"no interpolation {type}"),
    };

    /// <summary>A number as the vectors encode it, or <see langword="null"/> for QuantLib's <c>Null&lt;Real&gt;()</c>.</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The number, or <see langword="null"/>.</returns>
    public static double? NullableNumber(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : Number(e);

    /// <summary>The mixed interpolations' behavior of a spec.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>Its <c>behavior</c>.</returns>
    public static MixedInterpolationBehavior Behavior(JsonElement spec) =>
        (MixedInterpolationBehavior)spec.GetProperty("behavior").GetInt32();

    /// <summary>The derivative approximation of a spec.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>Its <c>derivativeApprox</c>.</returns>
    public static CubicInterpolationDerivativeApprox DerivativeApprox(JsonElement spec) =>
        (CubicInterpolationDerivativeApprox)spec.GetProperty("derivativeApprox").GetInt32();

    /// <summary>A boundary condition of a spec.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="name">The condition's key.</param>
    /// <returns>The condition.</returns>
    public static CubicInterpolationBoundaryCondition Condition(JsonElement spec, string name) =>
        (CubicInterpolationBoundaryCondition)spec.GetProperty(name).GetInt32();

    /// <summary>
    /// Compares the value, derivative, second derivative and primitive at the case's <c>points</c>, each with its
    /// <c>allowExtrapolation</c> flag. Where QuantLib's constructor throws, each output is one expected throw, and the
    /// interpolation is built again inside the comparison, so that nanoql's refusal is checked.
    /// </summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="build">Builds the case's interpolation.</param>
    /// <param name="memberPoints">
    /// How many of the points the derivative, second derivative and primitive are read at, the first ones; all of
    /// them when <see langword="null"/>.
    /// </param>
    public static void CheckMembers(VectorAssert check, VectorCase c, Func<Interpolation> build, int? memberPoints = null)
    {
        double[] points = Numbers(c.Input("points"));
        bool[] allow = Bools(c.Input("allowExtrapolation"));
        double[] some = points[..(memberPoints ?? points.Length)];
        Interpolation? f = null;
        if (c.Expected.GetProperty("value").ValueKind == JsonValueKind.Array)
        {
            try
            {
                f = build();
            }
            catch (Exception e) when (e is ArgumentException or NanoQLException)
            {
                // A refusal where QuantLib builds: the comparison below rebuilds it and reports the mismatch.
            }
        }

        check.Output(c, "value", Members(f, build, points, allow, (g, p, a) => g.Evaluate(p, a)));
        check.Output(c, "derivative", Members(f, build, some, allow, (g, p, a) => g.Derivative(p, a)));
        check.Output(c, "secondDerivative", Members(f, build, some, allow, (g, p, a) => g.SecondDerivative(p, a)));
        check.Output(c, "primitive", Members(f, build, some, allow, (g, p, a) => g.Primitive(p, a)));
    }

    /// <summary>Each value computed when compared, so that a throw is checked.</summary>
    /// <param name="inputs">The inputs.</param>
    /// <param name="f">The function.</param>
    /// <returns>The deferred values.</returns>
    public static object?[] Deferred<T>(IEnumerable<T> inputs, Func<T, object?> f) =>
        [.. inputs.Select(x => (object?)(Func<object?>)(() => f(x)))];

    private static object? Members(
        Interpolation? f, Func<Interpolation> build, double[] points, bool[] allow, Func<Interpolation, double, bool, double> member)
    {
        if (f is null)
        {
            // QuantLib's constructor throws: build again, so that the comparison sees nanoql's refusal.
            return (Func<object?>)(() => member(build(), points[0], allow[0]));
        }

        return Deferred(Enumerable.Range(0, points.Length), i => member(f, points[i], allow[i]));
    }
}
