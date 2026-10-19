// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The cap and floor vectors' inputs, as <c>tools/vectors/gen/instruments/capfloor.py</c> writes them: an instrument is
/// <c>{"type", "leg", "legType", "capRates", "floorRates"}</c>, its leg an Ibor leg spec
/// (<see cref="CouponInputs.LegOf"/>, <c>"legType"</c> <c>"IborLeg"</c>), a fixed-rate leg spec
/// (<see cref="CashFlowInputs.FixedRateLegOf"/>, <c>"FixedRateLeg"</c>) or null for an empty leg (<c>"Empty"</c>),
/// built as <see cref="Cap"/>, <see cref="Floor"/> or <see cref="Collar"/> by its type; a Black engine on a volatility
/// is <c>{"vol", "dayCounter", "displacement"}</c> and a Bachelier one <c>{"vol"}</c>; a discount curve is a flat
/// forward spec (<see cref="CashFlowInputs.CurveOf"/>).
/// </summary>
internal static class CapFloorInputs
{
    /// <summary>The instrument a spec describes, its leg built under the case's context.</summary>
    /// <param name="spec">The instrument spec.</param>
    /// <param name="context">The case's context: the leg's at-par switch.</param>
    /// <returns>The instrument.</returns>
    public static CapFloor CapFloorOf(JsonElement spec, EvaluationContext context)
    {
        ImmutableArray<CashFlow> leg = LegOf(spec, context);
        double[] capRates = CouponInputs.Reals(spec.GetProperty("capRates"));
        double[] floorRates = CouponInputs.Reals(spec.GetProperty("floorRates"));
        return (CapFloorType)spec.GetProperty("type").GetInt32() switch
        {
            CapFloorType.Cap => new Cap(leg, capRates),
            CapFloorType.Floor => new Floor(leg, floorRates),
            _ => new Collar(leg, capRates, floorRates),
        };
    }

    /// <summary>An instrument spec's leg.</summary>
    /// <param name="spec">The instrument spec.</param>
    /// <param name="context">The case's context.</param>
    /// <returns>The leg.</returns>
    public static ImmutableArray<CashFlow> LegOf(JsonElement spec, EvaluationContext context) =>
        spec.GetProperty("legType").GetString() switch
        {
            "Empty" => [],
            "FixedRateLeg" => CashFlowInputs.FixedRateLegOf(spec.GetProperty("leg"), context),
            _ => CouponInputs.LegOf(spec.GetProperty("leg"), context),
        };

    /// <summary>The scale of an instrument's amounts: its leg's largest notional, or 1 for an empty leg.</summary>
    /// <param name="spec">The instrument spec.</param>
    /// <returns>The scale.</returns>
    public static double Scale(JsonElement spec)
    {
        JsonElement leg = spec.GetProperty("leg");
        return leg.ValueKind == JsonValueKind.Null ? 1.0 : CouponInputs.Reals(leg.GetProperty("notionals")).Max();
    }

    /// <summary>Prices an instrument with <see cref="BlackCapFloorEngine"/> on a volatility spec.</summary>
    /// <param name="capFloor">The instrument.</param>
    /// <param name="context">The case's context.</param>
    /// <param name="curve">The discount curve.</param>
    /// <param name="engine">The engine spec.</param>
    /// <returns>The results.</returns>
    public static InstrumentResults Black(CapFloor capFloor, EvaluationContext context, YieldTermStructure curve, JsonElement engine) =>
        BlackCapFloorEngine.Calculate(
            capFloor,
            context,
            curve,
            IndexInputs.Real(engine.GetProperty("vol")),
            CashFlowInputs.DayCounterOf(engine.GetProperty("dayCounter")),
            IndexInputs.Real(engine.GetProperty("displacement")));

    /// <summary>Prices an instrument with <see cref="BachelierCapFloorEngine"/> on a volatility spec.</summary>
    /// <param name="capFloor">The instrument.</param>
    /// <param name="context">The case's context.</param>
    /// <param name="curve">The discount curve.</param>
    /// <param name="engine">The engine spec, its day counter QuantLib's default when it has none.</param>
    /// <returns>The results.</returns>
    public static InstrumentResults Bachelier(CapFloor capFloor, EvaluationContext context, YieldTermStructure curve, JsonElement engine) =>
        BachelierCapFloorEngine.Calculate(
            capFloor,
            context,
            curve,
            IndexInputs.Real(engine.GetProperty("vol")),
            engine.TryGetProperty("dayCounter", out JsonElement dc) ? CashFlowInputs.DayCounterOf(dc) : null);

    /// <summary>
    /// Prices an instrument with <see cref="BlackCapFloorEngine"/> on an engine spec: <c>{"form": "quote", "vol",
    /// "dayCounter", "displacement"}</c>, or <c>{"form": "structure", "volatility", "displacement"}</c> on a constant
    /// optionlet volatility (<see cref="CouponInputs.VolatilityOf"/>), a null displacement the structure's.
    /// </summary>
    /// <param name="capFloor">The instrument.</param>
    /// <param name="context">The case's context.</param>
    /// <param name="curve">The discount curve.</param>
    /// <param name="engine">The engine spec.</param>
    /// <returns>The results.</returns>
    public static InstrumentResults BlackOf(CapFloor capFloor, EvaluationContext context, YieldTermStructure curve, JsonElement engine) =>
        engine.GetProperty("form").GetString() == "quote"
            ? Black(capFloor, context, curve, engine)
            : BlackCapFloorEngine.Calculate(
                capFloor,
                context,
                curve,
                CouponInputs.VolatilityOf(engine.GetProperty("volatility"))!,
                CouponInputs.NullableReal(engine.GetProperty("displacement")));

    /// <summary>
    /// Prices an instrument with <see cref="BachelierCapFloorEngine"/> on an engine spec: <c>{"form": "quote", "vol"}</c>,
    /// with an optional <c>"dayCounter"</c>, or <c>{"form": "structure", "volatility"}</c>.
    /// </summary>
    /// <param name="capFloor">The instrument.</param>
    /// <param name="context">The case's context.</param>
    /// <param name="curve">The discount curve.</param>
    /// <param name="engine">The engine spec.</param>
    /// <returns>The results.</returns>
    public static InstrumentResults BachelierOf(CapFloor capFloor, EvaluationContext context, YieldTermStructure curve, JsonElement engine) =>
        engine.GetProperty("form").GetString() == "quote"
            ? Bachelier(capFloor, context, curve, engine)
            : BachelierCapFloorEngine.Calculate(capFloor, context, curve, CouponInputs.VolatilityOf(engine.GetProperty("volatility"))!);

    /// <summary>
    /// Compares a case's results per engine: the NPV, the vega and the per-optionlet results, each a list over the
    /// case's engines, a refused pricing each output's throw.
    /// </summary>
    /// <param name="check">The vector check.</param>
    /// <param name="c">The case.</param>
    /// <param name="price">The pricing on one engine spec.</param>
    public static void CheckPerEngine(VectorAssert check, VectorCase c, Func<JsonElement, InstrumentResults> price)
    {
        JsonElement[] engines = [.. c.Input("engines").EnumerateArray()];
        var priced = new (InstrumentResults? Results, Exception? Failure)[engines.Length];
        for (int i = 0; i < engines.Length; i++)
        {
            try
            {
                priced[i] = (price(engines[i]), null);
            }
            catch (Exception e) when (e is ArgumentException or NanoQLException)
            {
                priced[i] = (null, e);
            }
        }

        object?[] Of(Func<InstrumentResults, object?> f) =>
            [.. priced.Select(p => (Func<object?>)(() => p.Failure is null ? f(p.Results!) : throw p.Failure))];

        check.Output(c, "NPV", Of(r => r.NPV));
        check.Output(c, "vega", Of(r => r.Result<double>("vega")));
        foreach (string tag in (string[])["optionletsPrice", "optionletsVega", "optionletsDelta", "optionletsDiscountFactor", "optionletsStdDev"])
        {
            check.Output(c, tag, Of(r => r.Result<ImmutableArray<double>>(tag)));
        }

        check.Output(c, "optionletsAtmForward", Of(r => r.Result<ImmutableArray<double?>>("optionletsAtmForward")));
    }
}
