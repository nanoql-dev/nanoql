// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The inflation vectors' input specs, as <c>tools/vectors/gen/termstructures/inflationtermstructure.py</c>,
/// <c>tools/vectors/gen/termstructures/inflation/seasonality.py</c> and <c>tools/vectors/gen/indexes/inflationindex.py</c>
/// define them.
/// </summary>
/// <remarks>
/// <para>
/// A seasonality is <c>{"type": "MultiplicativePriceSeasonality", "baseDate", "frequency", "factors"}</c> or
/// <c>{"type": "KerkhofSeasonality", "baseDate", "factors"}</c>, or null for none.
/// </para>
/// <para>
/// A curve is <c>{"type": "ZeroInflationCurve" or "YoYInflationCurve", "referenceDate", "dates", "rates",
/// "frequency", "dayCounter", "seasonality", "enableExtrapolation"}</c>, a linear interpolated curve, or null for none.
/// </para>
/// <para>
/// A region is <c>{"type": "CustomRegion", "name", "code"}</c> or <c>{"type"}</c> naming a preset region. An index is
/// <c>{"type"}</c> naming a preset with its <c>"curve"</c>; <c>{"type": "ZeroInflationIndex" or "YoYInflationIndex",
/// "familyName", "region", "revised", "frequency", "availabilityLag", "currency", "curve"}</c>, the availability lag
/// <c>[length, units]</c> and the currency its code (GBP, EUR or USD); or <c>{"type": "YoYRatio", "underlying",
/// "curve"}</c>, a year-on-year index defined as the ratio of its underlying's fixings.
/// </para>
/// </remarks>
internal static class InflationInputs
{
    public static Seasonality? SeasonalityOf(JsonElement spec)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var baseDate = new Date(spec.GetProperty("baseDate").GetInt32());
        double[] factors = [.. spec.GetProperty("factors").EnumerateArray().Select(e => e.GetDouble())];
        return spec.GetProperty("type").GetString()! switch
        {
            "MultiplicativePriceSeasonality" =>
                new MultiplicativePriceSeasonality(baseDate, (Frequency)spec.GetProperty("frequency").GetInt32(), factors),
            "KerkhofSeasonality" => new KerkhofSeasonality(baseDate, factors),
            string type => throw new InvalidOperationException($"unknown seasonality type '{type}'"),
        };
    }

    public static InflationTermStructure CurveOf(JsonElement spec)
    {
        var referenceDate = new Date(spec.GetProperty("referenceDate").GetInt32());
        Date[] dates = [.. spec.GetProperty("dates").EnumerateArray().Select(e => new Date(e.GetInt32()))];
        double[] rates = [.. spec.GetProperty("rates").EnumerateArray().Select(e => e.GetDouble())];
        var frequency = (Frequency)spec.GetProperty("frequency").GetInt32();
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        Seasonality? seasonality = SeasonalityOf(spec.GetProperty("seasonality"));
        InflationTermStructure curve = spec.GetProperty("type").GetString()! switch
        {
            "ZeroInflationCurve" => new ZeroInflationCurve(referenceDate, dates, rates, frequency, dc, seasonality),
            "YoYInflationCurve" => new YoYInflationCurve(referenceDate, dates, rates, frequency, dc, seasonality),
            string type => throw new InvalidOperationException($"unknown curve type '{type}'"),
        };
        return spec.GetProperty("enableExtrapolation").GetBoolean() ? curve.EnableExtrapolation() : curve;
    }

    public static ZeroInflationTermStructure? ZeroCurveOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : (ZeroInflationTermStructure)CurveOf(spec);

    public static YoYInflationTermStructure? YoYCurveOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : (YoYInflationTermStructure)CurveOf(spec);

    public static Region RegionOf(JsonElement spec) =>
        spec.GetProperty("type").GetString()! switch
        {
            "CustomRegion" => new CustomRegion(spec.GetProperty("name").GetString()!, spec.GetProperty("code").GetString()!),
            "AustraliaRegion" => new AustraliaRegion(),
            "EURegion" => new EURegion(),
            "FranceRegion" => new FranceRegion(),
            "UKRegion" => new UKRegion(),
            "USRegion" => new USRegion(),
            "ZARegion" => new ZARegion(),
            string type => throw new InvalidOperationException($"unknown region type '{type}'"),
        };

    public static InflationIndex IndexOf(JsonElement spec)
    {
        JsonElement curve = spec.GetProperty("curve");
        return spec.GetProperty("type").GetString()! switch
        {
            "UKRPI" => new UKRPI(ZeroCurveOf(curve)),
            "USCPI" => new USCPI(ZeroCurveOf(curve)),
            "EUHICP" => new EUHICP(ZeroCurveOf(curve)),
            "EUHICPXT" => new EUHICPXT(ZeroCurveOf(curve)),
            "YYUKRPI" => new YYUKRPI(YoYCurveOf(curve)),
            "YYUSCPI" => new YYUSCPI(YoYCurveOf(curve)),
            "YYEUHICP" => new YYEUHICP(YoYCurveOf(curve)),
            "YYEUHICPXT" => new YYEUHICPXT(YoYCurveOf(curve)),
            "AUCPI" => new AUCPI(
                (Frequency)spec.GetProperty("frequency").GetInt32(),
                spec.GetProperty("revised").GetBoolean(),
                ZeroCurveOf(curve)),
            "YYAUCPI" => new YYAUCPI(
                (Frequency)spec.GetProperty("frequency").GetInt32(),
                spec.GetProperty("revised").GetBoolean(),
                YoYCurveOf(curve)),
            "FRHICP" => new FRHICP(ZeroCurveOf(curve)),
            "YYFRHICP" => new YYFRHICP(YoYCurveOf(curve)),
            "UKHICP" => new UKHICP(ZeroCurveOf(curve)),
            "ZACPI" => new ZACPI(ZeroCurveOf(curve)),
            "YYZACPI" => new YYZACPI(YoYCurveOf(curve)),
            "ZeroInflationIndex" => new ZeroInflationIndex(
                spec.GetProperty("familyName").GetString()!,
                RegionOf(spec.GetProperty("region")),
                spec.GetProperty("revised").GetBoolean(),
                (Frequency)spec.GetProperty("frequency").GetInt32(),
                LagOf(spec.GetProperty("availabilityLag")),
                CurrencyOf(spec.GetProperty("currency").GetString()!),
                ZeroCurveOf(curve)),
            "YoYInflationIndex" => new YoYInflationIndex(
                spec.GetProperty("familyName").GetString()!,
                RegionOf(spec.GetProperty("region")),
                spec.GetProperty("revised").GetBoolean(),
                (Frequency)spec.GetProperty("frequency").GetInt32(),
                LagOf(spec.GetProperty("availabilityLag")),
                CurrencyOf(spec.GetProperty("currency").GetString()!),
                YoYCurveOf(curve)),
            "YoYRatio" => new YoYInflationIndex(
                (ZeroInflationIndex)IndexOf(spec.GetProperty("underlying")), YoYCurveOf(curve)),
            string type => throw new InvalidOperationException($"unknown index type '{type}'"),
        };
    }

    public static Period LagOf(JsonElement spec) => new(spec[0].GetInt32(), (TimeUnit)spec[1].GetInt32());

    public static Period? NullableLagOf(JsonElement spec) => spec.ValueKind == JsonValueKind.Null ? null : LagOf(spec);

    public static CPIVolatilitySurface CPIVolatilityOf(JsonElement spec, EvaluationContext context)
    {
        ConstantCPIVolatility surface = ConstantCPIVolatility.Create(
            spec.GetProperty("volatility").GetDouble(),
            spec.GetProperty("settlementDays").GetInt32(),
            CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
            (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            LagOf(spec.GetProperty("observationLag")),
            (Frequency)spec.GetProperty("frequency").GetInt32(),
            spec.GetProperty("indexIsInterpolated").GetBoolean(),
            context);
        return spec.GetProperty("enableExtrapolation").GetBoolean() ? surface.EnableExtrapolation() : surface;
    }

    public static YoYOptionletVolatilitySurface YoYVolatilityOf(JsonElement spec, EvaluationContext context)
    {
        ConstantYoYOptionletVolatility surface = ConstantYoYOptionletVolatility.Create(
            spec.GetProperty("volatility").GetDouble(),
            spec.GetProperty("settlementDays").GetInt32(),
            CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
            (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            LagOf(spec.GetProperty("observationLag")),
            (Frequency)spec.GetProperty("frequency").GetInt32(),
            spec.GetProperty("indexIsInterpolated").GetBoolean(),
            context,
            spec.GetProperty("minStrike").GetDouble(),
            spec.GetProperty("maxStrike").GetDouble(),
            (VolatilityType)spec.GetProperty("volatilityType").GetInt32(),
            spec.GetProperty("displacement").GetDouble());
        return spec.GetProperty("enableExtrapolation").GetBoolean() ? surface.EnableExtrapolation() : surface;
    }

    private static Currency CurrencyOf(string code) =>
        code switch
        {
            "GBP" => new GBPCurrency(),
            "EUR" => new EURCurrency(),
            "USD" => new USDCurrency(),
            _ => throw new InvalidOperationException($"unknown currency '{code}'"),
        };
}
