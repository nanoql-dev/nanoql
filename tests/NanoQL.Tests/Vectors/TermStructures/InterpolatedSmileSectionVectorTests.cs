// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interpolated smile section vectors, <c>tests/vectors/termstructures/volatility/interpolatedsmilesection.json</c>:
/// on <see cref="LinearInterpolatedSmileSection"/>, <see cref="CubicInterpolatedSmileSection"/>,
/// <see cref="MonotonicCubicInterpolatedSmileSection"/> and <see cref="SplineCubicInterpolatedSmileSection"/>, by time,
/// by date with a reference date and by date from the evaluation context (their <c>Create</c>, and the generic class's
/// <see cref="InterpolatedSmileSection{TInterpolator}.Create"/>), with and without flat strike extrapolation, the
/// inspectors, and at 10 strikes inside and outside the section's the variance, volatility and option prices; and the
/// sections QuantLib refuses to build.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolatedSmileSectionVectorTests
{
    [Fact]
    public void InterpolatedSmileSectionVector()
    {
        var file = VectorFile.Load("termstructures/volatility/interpolatedsmilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "section":
                    CheckSection(check, c, SectionOf(c.Input("section"), c.Context()));
                    break;
                case "refusal":
                    check.Output(c, "section", () => SectionOf(c.Input("section"), c.Context()).Volatility(0.03));
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckSection(VectorAssert check, VectorCase c, SmileSection s)
    {
        double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
        double discount = c.Double("discount");

        check.Output(c, "exerciseDate", s.ExerciseDate);
        check.Output(c, "referenceDate", () => s.ReferenceDate());
        check.Output(c, "exerciseTime", s.ExerciseTime);
        check.Output(c, "dayCounter", s.DayCounter?.Name);
        check.Output(c, "volatilityType", s.VolatilityType);
        check.Output(c, "shift", s.Shift);
        check.Output(c, "minStrike", s.MinStrike());
        check.Output(c, "maxStrike", s.MaxStrike());
        check.Output(c, "atmLevel", s.AtmLevel());
        check.Output(c, "variance", AtStrikes(strikes, s.Variance));
        check.Output(c, "volatility", AtStrikes(strikes, k => s.Volatility(k)));
        check.Output(c, "callPrice", AtStrikes(strikes, k => s.OptionPrice(k, OptionType.Call, discount)));
        check.Output(c, "putPrice", AtStrikes(strikes, k => s.OptionPrice(k, OptionType.Put, discount)));
    }

    // The section a spec describes, as the generator builds it.
    private static SmileSection SectionOf(JsonElement spec, EvaluationContext context)
    {
        string type = spec.GetProperty("type").GetString()!;
        string form = spec.GetProperty("form").GetString()!;
        double[] strikes = VolatilityInputs.Reals(spec.GetProperty("strikes"));
        double[] stdDevs = VolatilityInputs.Reals(spec.GetProperty("stdDevs"));
        double atm = IndexInputs.Real(spec.GetProperty("atmLevel"));
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        var vt = (VolatilityType)spec.GetProperty("volatilityType").GetInt32();
        double shift = IndexInputs.Real(spec.GetProperty("shift"));
        bool flat = spec.GetProperty("flatStrikeExtrapolation").GetBoolean();
        double t = form == "time" ? IndexInputs.Real(spec.GetProperty("exerciseTime")) : 0.0;
        Date d = form == "time" ? default : new Date(spec.GetProperty("exerciseDate").GetInt32());
        Date reference = form == "date" ? new Date(spec.GetProperty("referenceDate").GetInt32()) : default;

        // The floating sections with flat strike extrapolation go through the generic class's Create, the others
        // through each named class's: QuantLib-Python's classes are the template's instantiations.
        if (form == "floating" && flat)
        {
            return type switch
            {
                "LinearInterpolatedSmileSection" =>
                    InterpolatedSmileSection<Linear>.Create(d, strikes, stdDevs, atm, context, dc, default, vt, shift, flat),
                "CubicInterpolatedSmileSection" =>
                    InterpolatedSmileSection<Cubic>.Create(d, strikes, stdDevs, atm, context, dc, CubicOf(spec), vt, shift, flat),
                "MonotonicCubicInterpolatedSmileSection" =>
                    InterpolatedSmileSection<Cubic>.Create(d, strikes, stdDevs, atm, context, dc, MonotonicCubic, vt, shift, flat),
                "SplineCubicInterpolatedSmileSection" =>
                    InterpolatedSmileSection<Cubic>.Create(d, strikes, stdDevs, atm, context, dc, SplineCubic, vt, shift, flat),
                _ => throw new InvalidOperationException($"unknown section {type} ({form})"),
            };
        }

        return (type, form) switch
        {
            ("LinearInterpolatedSmileSection", "time") =>
                new LinearInterpolatedSmileSection(t, strikes, stdDevs, atm, dc, vt, shift, flat),
            ("LinearInterpolatedSmileSection", "date") =>
                new LinearInterpolatedSmileSection(d, strikes, stdDevs, atm, dc, reference, vt, shift, flat),
            ("LinearInterpolatedSmileSection", "floating") =>
                LinearInterpolatedSmileSection.Create(d, strikes, stdDevs, atm, context, dc, vt, shift, flat),
            ("CubicInterpolatedSmileSection", "time") =>
                new CubicInterpolatedSmileSection(t, strikes, stdDevs, atm, CubicOf(spec), dc, vt, shift, flat),
            ("CubicInterpolatedSmileSection", "date") =>
                new CubicInterpolatedSmileSection(d, strikes, stdDevs, atm, dc, CubicOf(spec), reference, vt, shift, flat),
            ("CubicInterpolatedSmileSection", "floating") =>
                CubicInterpolatedSmileSection.Create(d, strikes, stdDevs, atm, context, dc, CubicOf(spec), vt, shift, flat),
            ("MonotonicCubicInterpolatedSmileSection", "time") =>
                new MonotonicCubicInterpolatedSmileSection(t, strikes, stdDevs, atm, dc, vt, shift, flat),
            ("MonotonicCubicInterpolatedSmileSection", "date") =>
                new MonotonicCubicInterpolatedSmileSection(d, strikes, stdDevs, atm, dc, reference, vt, shift, flat),
            ("MonotonicCubicInterpolatedSmileSection", "floating") =>
                MonotonicCubicInterpolatedSmileSection.Create(d, strikes, stdDevs, atm, context, dc, vt, shift, flat),
            ("SplineCubicInterpolatedSmileSection", "time") =>
                new SplineCubicInterpolatedSmileSection(t, strikes, stdDevs, atm, dc, vt, shift, flat),
            ("SplineCubicInterpolatedSmileSection", "date") =>
                new SplineCubicInterpolatedSmileSection(d, strikes, stdDevs, atm, dc, reference, vt, shift, flat),
            ("SplineCubicInterpolatedSmileSection", "floating") =>
                SplineCubicInterpolatedSmileSection.Create(d, strikes, stdDevs, atm, context, dc, vt, shift, flat),
            _ => throw new InvalidOperationException($"unknown section {type} ({form})"),
        };
    }

    // QuantLib-Python's MonotonicCubic and SplineCubic (interpolation.i:144-158): Cubic(Spline, true or false,
    // SecondDerivative, 0, SecondDerivative, 0).
    private static Cubic MonotonicCubic => new(CubicInterpolationDerivativeApprox.Spline, true);

    private static Cubic SplineCubic => new(CubicInterpolationDerivativeApprox.Spline, false);

    // CubicInterpolatedSmileSection's interpolator: QuantLib's Cubic() where the spec gives none.
    private static Cubic CubicOf(JsonElement spec)
    {
        JsonElement c = spec.GetProperty("cubic");
        return c.ValueKind == JsonValueKind.Null
            ? default
            : new Cubic(
                InterpolationInputs.DerivativeApprox(c),
                c.GetProperty("monotonic").GetBoolean(),
                InterpolationInputs.Condition(c, "leftCondition"),
                IndexInputs.Real(c.GetProperty("leftConditionValue")),
                InterpolationInputs.Condition(c, "rightCondition"),
                IndexInputs.Real(c.GetProperty("rightConditionValue")));
    }

    // Each value computed when compared, so that a throw is checked.
    private static Func<object?>[] AtStrikes(double[] strikes, Func<double, double> f) =>
        [.. strikes.Select(k => (Func<object?>)(() => f(k)))];
}
