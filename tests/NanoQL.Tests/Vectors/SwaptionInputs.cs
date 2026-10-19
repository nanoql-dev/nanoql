// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the swaptions, engines and volatility matrices that the swaption vector files describe in their inputs, as
/// their generators build them (<c>tools/vectors/gen/instruments/swaption.py</c>'s and
/// <c>tools/vectors/gen/termstructures/volatility/swaption/swaptionvolmatrix.py</c>'s functions).
/// </summary>
/// <remarks>
/// <para>
/// A swaption is <c>{"swap", "exercise", "settlementType", "settlementMethod"}</c>, its swap a spec of
/// <see cref="SwapInputs"/> and its exercise one of <see cref="OptionInputs"/>. A Black engine on a volatility is
/// <c>{"vol", "dayCounter", "displacement", "model"}</c>, a Bachelier one the same without the displacement.
/// </para>
/// <para>
/// A volatility matrix is <c>{"form", "referenceDate", "calendar", "convention", "optionTenors" or "optionDates",
/// "swapTenors", "volatilities", "dayCounter", "flatExtrapolation", "type", "shifts", "enableExtrapolation"}</c>, its form
/// <c>"tenors"</c> (a fixed reference date, on option tenors), <c>"dates"</c> (on option dates) or <c>"floating"</c>
/// (the reference date the evaluation date adjusted on the calendar, through
/// <see cref="SwaptionVolatilityMatrix.Create"/>, without <c>"referenceDate"</c>); the matrices are arrays of rows, and
/// null shifts are QuantLib's default <c>Matrix()</c>.
/// </para>
/// </remarks>
internal static class SwaptionInputs
{
    /// <summary>The swaption a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which a vanilla swap's floating leg reads.</param>
    /// <returns>The swaption.</returns>
    public static Swaption SwaptionOf(JsonElement spec, EvaluationContext context) => new(
        SwapInputs.SwapOf(spec.GetProperty("swap"), context),
        OptionInputs.ExerciseOf(spec.GetProperty("exercise")),
        (SettlementType)spec.GetProperty("settlementType").GetInt32(),
        (SettlementMethod)spec.GetProperty("settlementMethod").GetInt32());

    /// <summary>The Black engine on a constant volatility a spec describes, priced at once.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="spec">The engine's spec.</param>
    /// <returns>The results.</returns>
    public static InstrumentResults Black(
        Swaption swaption, EvaluationContext context, YieldTermStructure discountCurve, JsonElement spec) =>
        BlackSwaptionEngine.Calculate(
            swaption,
            context,
            discountCurve,
            IndexInputs.Real(spec.GetProperty("vol")),
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            IndexInputs.Real(spec.GetProperty("displacement")),
            (CashAnnuityModel)spec.GetProperty("model").GetInt32());

    /// <summary>The Bachelier engine on a constant volatility a spec describes, priced at once.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="spec">The engine's spec.</param>
    /// <returns>The results.</returns>
    public static InstrumentResults Bachelier(
        Swaption swaption, EvaluationContext context, YieldTermStructure discountCurve, JsonElement spec) =>
        BachelierSwaptionEngine.Calculate(
            swaption,
            context,
            discountCurve,
            IndexInputs.Real(spec.GetProperty("vol")),
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            (CashAnnuityModel)spec.GetProperty("model").GetInt32());

    /// <summary>
    /// Checks a priced swaption's NPV and the results the wheel reads, each under the prefix: <c>NPV</c>,
    /// <c>vega</c>, <c>delta</c>, <c>annuity</c>, <c>forwardPrice</c>, <c>strike</c>, <c>atmForward</c>,
    /// <c>swapLength</c> and <c>stdDev</c>; a refused pricing is each one's throw.
    /// </summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="prefix">The outputs' prefix.</param>
    /// <param name="price">The pricing.</param>
    public static void CheckPriced(VectorAssert check, VectorCase c, string prefix, Func<InstrumentResults> price)
    {
        InstrumentResults? results = null;
        Exception? refused = null;
        try
        {
            results = price();
        }
        catch (Exception e) when (e is ArgumentException or NanoQLException)
        {
            refused = e;
        }

        object? Read(Func<InstrumentResults, object?> f) => refused is not null ? throw refused : f(results!);

        check.Output(c, prefix + "NPV", () => Read(r => r.NPV));
        foreach (string name in (string[])["vega", "delta", "annuity", "forwardPrice", "strike", "atmForward", "swapLength", "stdDev"])
        {
            check.Output(c, prefix + char.ToUpperInvariant(name[0]) + name[1..], () => Read(r => r.Result<double>(name)));
        }
    }

    /// <summary>
    /// The swap index a spec describes: a Euribor swap preset, <c>{"type", "tenor", "forwarding"}</c> with
    /// <c>"discounting"</c> for the exogenous constructor, or a spec of <see cref="SwapIndexInputs"/>.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The index.</returns>
    public static SwapIndex SwapIndexOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        if (type is not ("EuriborSwapIsdaFixA" or "EuriborSwapIsdaFixB" or "EuriborSwapIfrFix"))
        {
            return SwapIndexInputs.SwapIndexOf(spec);
        }

        Period tenor = VolatilityInputs.TenorOf(spec.GetProperty("tenor"));
        YieldTermStructure? forwarding = IndexInputs.CurveOf(spec.GetProperty("forwarding"));
        if (spec.TryGetProperty("discounting", out JsonElement d))
        {
            YieldTermStructure? discounting = IndexInputs.CurveOf(d);
            return type switch
            {
                "EuriborSwapIsdaFixA" => new EuriborSwapIsdaFixA(tenor, forwarding, discounting),
                "EuriborSwapIsdaFixB" => new EuriborSwapIsdaFixB(tenor, forwarding, discounting),
                _ => new EuriborSwapIfrFix(tenor, forwarding, discounting),
            };
        }

        return type switch
        {
            "EuriborSwapIsdaFixA" => new EuriborSwapIsdaFixA(tenor, forwarding),
            "EuriborSwapIsdaFixB" => new EuriborSwapIsdaFixB(tenor, forwarding),
            _ => new EuriborSwapIfrFix(tenor, forwarding),
        };
    }

    /// <summary>
    /// The builder a build spec describes: <c>{"index", "form", "optionTenor" or "fixingDate", "strike", "setters"}</c>,
    /// its setters called in order, a pricing engine a Black engine on a volatility on the given discount curve.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="discountCurve">The discount curve a pricing engine is on.</param>
    /// <returns>The builder.</returns>
    public static MakeSwaption MakeSwaptionOf(JsonElement spec, YieldTermStructure discountCurve)
    {
        SwapIndex index = SwapIndexOf(spec.GetProperty("index"));
        double? strike = CouponInputs.NullableReal(spec.GetProperty("strike"));
        MakeSwaption builder = spec.GetProperty("form").GetString() == "tenor"
            ? new MakeSwaption(index, VolatilityInputs.TenorOf(spec.GetProperty("optionTenor")), strike)
            : new MakeSwaption(index, new Date(spec.GetProperty("fixingDate").GetInt32()), strike);
        foreach (JsonElement setter in spec.GetProperty("setters").EnumerateArray())
        {
            JsonElement x = setter[1];
            builder = setter[0].GetString() switch
            {
                "nominal" => builder.WithNominal(IndexInputs.Real(x)),
                "settlementType" => builder.WithSettlementType((SettlementType)x.GetInt32()),
                "settlementMethod" => builder.WithSettlementMethod((SettlementMethod)x.GetInt32()),
                "optionConvention" => builder.WithOptionConvention((BusinessDayConvention)x.GetInt32()),
                "exerciseDate" => builder.WithExerciseDate(new Date(x.GetInt32())),
                "exerciseCalendar" => builder.WithExerciseCalendar(CashFlowInputs.CalendarOf(x)),
                "underlyingType" => builder.WithUnderlyingType((SwapType)x.GetInt32()),
                "indexedCoupons" => builder.WithIndexedCoupons(x.GetBoolean()),
                "atParCoupons" => builder.WithAtParCoupons(x.GetBoolean()),
                "pricingEngine" => builder.WithPricingEngine((s, c) => Black(s, c, discountCurve, x)),
                string name => throw new InvalidOperationException($"unknown setter '{name}'"),
                null => throw new InvalidOperationException("a setter without a name"),
            };
        }

        return builder;
    }

    /// <summary>The volatility matrix a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which a floating matrix takes its reference date from.</param>
    /// <returns>The matrix, with extrapolation enabled when the spec says so.</returns>
    public static SwaptionVolatilityMatrix MatrixOf(JsonElement spec, EvaluationContext context)
    {
        string form = spec.GetProperty("form").GetString()!;
        Calendar calendar = CashFlowInputs.CalendarOf(spec.GetProperty("calendar"));
        var bdc = (BusinessDayConvention)spec.GetProperty("convention").GetInt32();
        Period[] swapTenors = [.. spec.GetProperty("swapTenors").EnumerateArray().Select(VolatilityInputs.TenorOf)];
        Matrix volatilities = MatrixValueOf(spec.GetProperty("volatilities"))!;
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        bool flat = spec.GetProperty("flatExtrapolation").GetBoolean();
        var type = (VolatilityType)spec.GetProperty("type").GetInt32();
        Matrix? shifts = MatrixValueOf(spec.GetProperty("shifts"));
        SwaptionVolatilityMatrix matrix = form switch
        {
            "tenors" => new SwaptionVolatilityMatrix(
                new Date(spec.GetProperty("referenceDate").GetInt32()),
                calendar,
                bdc,
                spec.GetProperty("optionTenors").EnumerateArray().Select(VolatilityInputs.TenorOf),
                swapTenors,
                volatilities,
                dc,
                flat,
                type,
                shifts),
            "dates" => new SwaptionVolatilityMatrix(
                new Date(spec.GetProperty("referenceDate").GetInt32()),
                calendar,
                bdc,
                spec.GetProperty("optionDates").EnumerateArray().Select(d => new Date(d.GetInt32())),
                swapTenors,
                volatilities,
                dc,
                flat,
                type,
                shifts),
            "floating" => SwaptionVolatilityMatrix.Create(
                calendar,
                bdc,
                spec.GetProperty("optionTenors").EnumerateArray().Select(VolatilityInputs.TenorOf),
                swapTenors,
                volatilities,
                dc,
                context,
                flat,
                type,
                shifts),
            _ => throw new InvalidOperationException($"unknown matrix form '{form}'"),
        };
        return spec.GetProperty("enableExtrapolation").GetBoolean() ? matrix.EnableExtrapolation() : matrix;
    }

    // A matrix as an array of rows, or null for QuantLib's default Matrix().
    private static Matrix? MatrixValueOf(JsonElement rows)
    {
        if (rows.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        double[][] r = [.. rows.EnumerateArray().Select(VolatilityInputs.Reals)];
        int columns = r.Length == 0 ? 0 : r[0].Length;
        var m = new Matrix(r.Length, columns);
        for (int i = 0; i < r.Length; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                m[i, j] = r[i][j];
            }
        }

        return m;
    }
}
