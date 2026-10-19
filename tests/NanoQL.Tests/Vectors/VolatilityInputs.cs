// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the volatility structures that the optionlet and swaption volatility vector files describe in their inputs,
/// as their generators build them (<c>tools/vectors/gen/termstructures/volatility/optionlet/constantoptionletvol.py</c>
/// and <c>tools/vectors/gen/termstructures/volatility/swaption/swaptionconstantvol.py</c>).
/// </summary>
/// <remarks>
/// An optionlet surface is <c>{"type": "ConstantOptionletVolatility", "referenceDate" or "settlementDays", "calendar",
/// "convention", "volatility", "dayCounter", "volatilityType", "displacement", "enableExtrapolation"}</c>, with the
/// calendar and day counter as <see cref="CashFlowInputs"/> reads them; or the C++ harness's own structure,
/// <c>{"type": "BaseOptionletVolatility", "referenceDate", "calendar", "convention", "volatility", "dayCounter",
/// "enableExtrapolation"}</c>. A swaption surface is described at <see cref="SwaptionVolatilityOf"/>. A tenor is
/// <c>[length, units]</c>.
/// </remarks>
internal static class VolatilityInputs
{
    /// <summary>The optionlet volatility structure a surface spec describes, of either type.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, from which a moving surface takes its reference date.</param>
    /// <returns>The structure, with extrapolation enabled when the spec says so.</returns>
    public static OptionletVolatilityStructure OptionletVolatilityOf(JsonElement spec, EvaluationContext context)
    {
        if (spec.GetProperty("type").GetString() != "BaseOptionletVolatility")
        {
            return ConstantOptionletVolatilityOf(spec, context);
        }

        var surface = new BaseOptionletVolatility(
            new Date(spec.GetProperty("referenceDate").GetInt32()),
            CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
            (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            spec.GetProperty("volatility").GetDouble());
        return spec.GetProperty("enableExtrapolation").GetBoolean() ? surface.EnableExtrapolation() : surface;
    }

    /// <summary>The constant optionlet volatility a surface spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, from which a moving surface takes its reference date.</param>
    /// <returns>The structure, with extrapolation enabled when the spec says so.</returns>
    public static ConstantOptionletVolatility ConstantOptionletVolatilityOf(JsonElement spec, EvaluationContext context)
    {
        string type = spec.GetProperty("type").GetString()!;
        if (type != "ConstantOptionletVolatility")
        {
            throw new InvalidOperationException($"unknown optionlet volatility type '{type}'");
        }

        Calendar cal = CashFlowInputs.CalendarOf(spec.GetProperty("calendar"));
        var bdc = (BusinessDayConvention)spec.GetProperty("convention").GetInt32();
        double volatility = spec.GetProperty("volatility").GetDouble();
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        var volatilityType = (VolatilityType)spec.GetProperty("volatilityType").GetInt32();
        double displacement = spec.GetProperty("displacement").GetDouble();
        ConstantOptionletVolatility surface = spec.TryGetProperty("referenceDate", out JsonElement referenceDate)
            ? new ConstantOptionletVolatility(
                new Date(referenceDate.GetInt32()), cal, bdc, volatility, dc, volatilityType, displacement)
            : ConstantOptionletVolatility.Create(
                spec.GetProperty("settlementDays").GetInt32(), cal, bdc, volatility, dc, context, volatilityType, displacement);
        return spec.GetProperty("enableExtrapolation").GetBoolean() ? surface.EnableExtrapolation() : surface;
    }

    /// <summary>The swaption volatility structure a surface spec describes, of either type.</summary>
    /// <param name="spec">
    /// The spec: <c>{"type": "ConstantSwaptionVolatility", "referenceDate" or "settlementDays", "calendar", "convention",
    /// "volatility", "dayCounter", "volatilityType", "shift", "enableExtrapolation"}</c>, or the C++ harness's own
    /// structure, <c>{"type": "BaseSwaptionVolatility", "referenceDate", "calendar", "convention", "volatility",
    /// "dayCounter", "maxSwapTenor", "enableExtrapolation"}</c>.
    /// </param>
    /// <param name="context">The case's evaluation context, from which a moving surface takes its reference date.</param>
    /// <returns>The structure, with extrapolation enabled when the spec says so.</returns>
    public static SwaptionVolatilityStructure SwaptionVolatilityOf(JsonElement spec, EvaluationContext context)
    {
        string type = spec.GetProperty("type").GetString()!;
        Calendar cal = CashFlowInputs.CalendarOf(spec.GetProperty("calendar"));
        var bdc = (BusinessDayConvention)spec.GetProperty("convention").GetInt32();
        double volatility = spec.GetProperty("volatility").GetDouble();
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        SwaptionVolatilityStructure surface;
        if (type == "BaseSwaptionVolatility")
        {
            surface = new BaseSwaptionVolatility(
                new Date(spec.GetProperty("referenceDate").GetInt32()), cal, bdc, dc, volatility, TenorOf(spec.GetProperty("maxSwapTenor")));
        }
        else if (type == "ConstantSwaptionVolatility")
        {
            var volatilityType = (VolatilityType)spec.GetProperty("volatilityType").GetInt32();
            double shift = spec.GetProperty("shift").GetDouble();
            surface = spec.TryGetProperty("referenceDate", out JsonElement referenceDate)
                ? new ConstantSwaptionVolatility(new Date(referenceDate.GetInt32()), cal, bdc, volatility, dc, volatilityType, shift)
                : ConstantSwaptionVolatility.Create(
                    spec.GetProperty("settlementDays").GetInt32(), cal, bdc, volatility, dc, context, volatilityType, shift);
        }
        else
        {
            throw new InvalidOperationException($"unknown swaption volatility type '{type}'");
        }

        return spec.GetProperty("enableExtrapolation").GetBoolean() ? surface.EnableExtrapolation() : surface;
    }

    /// <summary>The period a tenor input describes.</summary>
    /// <param name="tenor">The tenor, <c>[length, units]</c>.</param>
    /// <returns>The period.</returns>
    public static Period TenorOf(JsonElement tenor) => new(tenor[0].GetInt32(), (TimeUnit)tenor[1].GetInt32());

    /// <summary>The reals of an array input, each a number or "NaN", "Infinity" or "-Infinity".</summary>
    /// <param name="values">The array.</param>
    /// <returns>The reals.</returns>
    public static double[] Reals(JsonElement values) => values.EnumerateArray().Select(IndexInputs.Real).ToArray();

    /// <summary>
    /// A grid of outputs, one per pair of inputs, each computed when compared so that a throw is checked:
    /// <c>[first][second]</c>, as the generators write them.
    /// </summary>
    /// <typeparam name="TFirst">The type of the outer inputs.</typeparam>
    /// <typeparam name="TSecond">The type of the inner inputs.</typeparam>
    /// <param name="first">The outer inputs.</param>
    /// <param name="second">The inner inputs.</param>
    /// <param name="f">The output.</param>
    /// <returns>The grid.</returns>
    public static Func<object?>[][] Grid<TFirst, TSecond>(TFirst[] first, TSecond[] second, Func<TFirst, TSecond, double> f) =>
        first.Select(a => second.Select(b => (Func<object?>)(() => f(a, b))).ToArray()).ToArray();

    /// <summary>
    /// The C++ harness's own optionlet structure: it overrides only what <see cref="OptionletVolatilityStructure"/>
    /// leaves abstract, with a constant volatility over every date and strike and a flat smile section by time, so its
    /// volatility type, displacement and smile section by date are the base's defaults.
    /// </summary>
    private sealed class BaseOptionletVolatility(
        Date referenceDate, Calendar cal, BusinessDayConvention bdc, DayCounter dc, double volatility)
        : OptionletVolatilityStructure(referenceDate, cal, bdc, dc)
    {
        public override Date MaxDate => Date.MaxDate;

        public override double MinStrike() => -double.MaxValue;

        public override double MaxStrike() => double.MaxValue;

        protected override SmileSection SmileSectionImpl(double optionTime) => new FlatSmileSection(optionTime, volatility, DayCounter);

        protected override double VolatilityImpl(double optionTime, double strike) => volatility;
    }

    /// <summary>
    /// The C++ harness's own swaption structure: it overrides only what <see cref="SwaptionVolatilityStructure"/> leaves
    /// abstract, so its date and tenor queries, shift and volatility type are the base's defaults. Its volatility at an
    /// option time t, a swap length l and a strike k is volatility + 0.01 t + 0.001 l + 0.1 k, and its smile section a
    /// flat section by time at the volatility for strike 0.
    /// </summary>
    private sealed class BaseSwaptionVolatility(
        Date referenceDate, Calendar cal, BusinessDayConvention bdc, DayCounter dc, double volatility, Period maxSwapTenor)
        : SwaptionVolatilityStructure(referenceDate, cal, bdc, dc)
    {
        public override Date MaxDate => Date.MaxDate;

        public override Period MaxSwapTenor => maxSwapTenor;

        public override double MinStrike() => -double.MaxValue;

        public override double MaxStrike() => double.MaxValue;

        protected override SmileSection SmileSectionImpl(double optionTime, double swapLength) =>
            new FlatSmileSection(optionTime, VolatilityImpl(optionTime, swapLength, 0.0), DayCounter);

        protected override double VolatilityImpl(double optionTime, double swapLength, double strike) =>
            volatility + (0.01 * optionTime) + (0.001 * swapLength) + (0.1 * strike);
    }
}
