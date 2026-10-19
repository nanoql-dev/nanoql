// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the smile sections that the smile section vector files describe in their inputs, as their generators build
/// them (<c>tools/vectors/gen/termstructures/volatility/flatsmilesection.py</c> and <c>sabrsmilesection.py</c>), and
/// the summary the C++ harness gives of a section a method returns
/// (<c>tools/cxx/src/termstructures/volatility/smilesectionspec.hpp</c>).
/// </summary>
/// <remarks>
/// A section is <c>{"type": "FlatSmileSection", "form", ...}</c>, by date with a reference date (<c>"date"</c>), by
/// date with the evaluation date as its reference date (<c>"floating"</c>, nanoql's
/// <see cref="FlatSmileSection.Create"/>) or by time (<c>"time"</c>), its day counter null for QuantLib's empty one and
/// its ATM level null for none; <c>{"type": "SabrSmileSection", "form", "forward", "sabrParameters", ...}</c> in the
/// same three forms (<see cref="SabrSmileSection.Create"/> the floating one), its day counter null in the time form;
/// or the C++ harness's own section, <c>{"type": "HarnessSmileSection", ...}</c>.
/// </remarks>
internal static class SmileSectionInputs
{
    /// <summary>The smile section a spec describes, of either type.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, from which a floating section takes its reference date.</param>
    /// <returns>The section.</returns>
    public static SmileSection SectionOf(JsonElement spec, EvaluationContext context)
    {
        string type = spec.GetProperty("type").GetString()!;
        string form = spec.GetProperty("form").GetString()!;
        if (type == "SabrSmileSection")
        {
            return SabrSectionOf(spec, form, context);
        }

        JsonElement dcSpec = spec.GetProperty("dayCounter");
        DayCounter? dc = dcSpec.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.DayCounterOf(dcSpec);
        double? atmLevel = NullableReal(spec.GetProperty("atmLevel"));
        var volatilityType = (VolatilityType)spec.GetProperty("volatilityType").GetInt32();
        double shift = IndexInputs.Real(spec.GetProperty("shift"));
        if (type == "HarnessSmileSection")
        {
            return form == "time"
                ? new HarnessSmileSection(IndexInputs.Real(spec.GetProperty("exerciseTime")), dc, volatilityType, shift, spec)
                : new HarnessSmileSection(
                    new Date(spec.GetProperty("exerciseDate").GetInt32()),
                    dc!,
                    new Date(spec.GetProperty("referenceDate").GetInt32()),
                    volatilityType,
                    shift,
                    spec);
        }

        if (type != "FlatSmileSection")
        {
            throw new InvalidOperationException($"unknown smile section type '{type}'");
        }

        double vol = IndexInputs.Real(spec.GetProperty("volatility"));
        return form switch
        {
            "time" => new FlatSmileSection(
                IndexInputs.Real(spec.GetProperty("exerciseTime")), vol, dc, atmLevel, volatilityType, shift),
            "date" => new FlatSmileSection(
                new Date(spec.GetProperty("exerciseDate").GetInt32()),
                vol,
                dc!,
                new Date(spec.GetProperty("referenceDate").GetInt32()),
                atmLevel,
                volatilityType,
                shift),
            "floating" => FlatSmileSection.Create(
                new Date(spec.GetProperty("exerciseDate").GetInt32()), vol, dc!, context, atmLevel, volatilityType, shift),
            _ => throw new InvalidOperationException($"unknown smile section form '{form}'"),
        };
    }

    /// <summary>The SABR smile section a spec of type <c>SabrSmileSection</c> describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="form">Its form: <c>"date"</c>, <c>"floating"</c> or <c>"time"</c>.</param>
    /// <param name="context">The case's evaluation context, from which a floating section takes its reference date.</param>
    /// <returns>The section.</returns>
    public static SabrSmileSection SabrSectionOf(JsonElement spec, string form, EvaluationContext context)
    {
        double forward = IndexInputs.Real(spec.GetProperty("forward"));
        double[] parameters = VolatilityInputs.Reals(spec.GetProperty("sabrParameters"));
        var volatilityType = (VolatilityType)spec.GetProperty("volatilityType").GetInt32();
        double shift = IndexInputs.Real(spec.GetProperty("shift"));
        if (form == "time")
        {
            return new SabrSmileSection(
                IndexInputs.Real(spec.GetProperty("exerciseTime")), forward, parameters, shift, volatilityType);
        }

        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        var d = new Date(spec.GetProperty("exerciseDate").GetInt32());
        return form switch
        {
            "date" => new SabrSmileSection(
                d, forward, parameters, new Date(spec.GetProperty("referenceDate").GetInt32()), dc, shift, volatilityType),
            "floating" => SabrSmileSection.Create(d, forward, parameters, context, dc, shift, volatilityType),
            _ => throw new InvalidOperationException($"unknown smile section form '{form}'"),
        };
    }

    /// <summary>
    /// A section's summary, as the C++ harness gives it: its exercise date, exercise time, volatility type, shift, ATM
    /// level and volatility at a strike.
    /// </summary>
    /// <param name="section">The section.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The summary.</returns>
    public static object?[] Summary(SmileSection section, double strike) =>
        [section.ExerciseDate, section.ExerciseTime, section.VolatilityType, section.Shift, section.AtmLevel(), section.Volatility(strike)];

    /// <summary>A real that may be null, QuantLib's <c>Null&lt;Real&gt;()</c>.</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The real, or <see langword="null"/>.</returns>
    public static double? NullableReal(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : IndexInputs.Real(e);

    /// <summary>
    /// The C++ harness's own section: it overrides only what <see cref="SmileSection"/> leaves abstract, with the
    /// volatility a + b k + c k² at a strike k, and the volatility type and shift where the spec overrides them, so
    /// that the base's defaults are what it prices with.
    /// </summary>
    private sealed class HarnessSmileSection : SmileSection
    {
        private readonly double a;
        private readonly double b;
        private readonly double c;
        private readonly double? atmLevel;
        private readonly double minStrike;
        private readonly double maxStrike;
        private readonly VolatilityType? overriddenType;
        private readonly double? overriddenShift;

        public HarnessSmileSection(Date d, DayCounter dc, Date referenceDate, VolatilityType type, double shift, JsonElement spec)
            : base(d, dc, referenceDate, type, shift)
        {
            (a, b, c, atmLevel, minStrike, maxStrike, overriddenType, overriddenShift) = Read(spec);
        }

        public HarnessSmileSection(double exerciseTime, DayCounter? dc, VolatilityType type, double shift, JsonElement spec)
            : base(exerciseTime, dc, type, shift)
        {
            (a, b, c, atmLevel, minStrike, maxStrike, overriddenType, overriddenShift) = Read(spec);
        }

        public override VolatilityType VolatilityType => overriddenType ?? base.VolatilityType;

        public override double Shift => overriddenShift ?? base.Shift;

        public override double MinStrike() => minStrike;

        public override double MaxStrike() => maxStrike;

        public override double? AtmLevel() => atmLevel;

        protected override double VolatilityImpl(double strike) => a + (b * strike) + (c * strike * strike);

        private static (double, double, double, double?, double, double, VolatilityType?, double?) Read(JsonElement spec)
        {
            double[] smile = VolatilityInputs.Reals(spec.GetProperty("smile"));
            JsonElement type = spec.GetProperty("overriddenType");
            return (
                smile[0],
                smile[1],
                smile[2],
                NullableReal(spec.GetProperty("atmLevel")),
                IndexInputs.Real(spec.GetProperty("minStrike")),
                IndexInputs.Real(spec.GetProperty("maxStrike")),
                type.ValueKind == JsonValueKind.Null ? null : (VolatilityType)type.GetInt32(),
                NullableReal(spec.GetProperty("overriddenShift")));
        }
    }
}
