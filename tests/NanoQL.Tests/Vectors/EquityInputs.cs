// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The equity vector files' specs, as tools/vectors/gen/termstructures/volatility/equityfx/blackvoltermstructure.py,
/// tools/vectors/gen/processes/blackscholesprocess.py, tools/vectors/gen/pricingengines/blackcalculator.py and
/// tools/vectors/gen/pricingengines/vanilla/analyticdividendeuropeanengine.py write them: Black volatility structures,
/// yield curves, Black-Scholes processes, payoffs, exercises and dividends.
/// </summary>
internal static class EquityInputs
{
    /// <summary>The Black volatility structure a spec describes, a settlement-days one dated by the context.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The structure.</returns>
    public static BlackVolTermStructure BlackVolOf(JsonElement spec, EvaluationContext context)
    {
        string type = spec.GetProperty("type").GetString()!;
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        switch (type)
        {
            case "BlackConstantVol":
                double vol = spec.GetProperty("volatility").GetDouble();
                Calendar calendar = CashFlowInputs.CalendarOf(spec.GetProperty("calendar"));
                return spec.TryGetProperty("settlementDays", out JsonElement days)
                    ? BlackConstantVol.Create(days.GetInt32(), calendar, vol, dc, context)
                    : new BlackConstantVol(DateOf(spec.GetProperty("referenceDate")), calendar, vol, dc);
            case "BlackVarianceCurve":
                var curve = new BlackVarianceCurve(
                    DateOf(spec.GetProperty("referenceDate")),
                    DatesOf(spec.GetProperty("dates")),
                    Doubles(spec.GetProperty("volatilities")),
                    dc,
                    spec.GetProperty("forceMonotoneVariance").GetBoolean(),
                    (BlackVolTimeExtrapolationType)spec.GetProperty("timeExtrapolation").GetInt32());
                return spec.GetProperty("interpolation").ValueKind == JsonValueKind.Null
                    ? curve
                    : spec.GetProperty("interpolation").GetString() switch
                    {
                        "cubic" => curve.SetInterpolation<Cubic>(),
                        string s => throw new InvalidOperationException($"unknown interpolation '{s}'"),
                        null => curve,
                    };
            case "BlackVarianceSurface":
                double[][] rows = [.. spec.GetProperty("volatilities").EnumerateArray().Select(Doubles)];
                var matrix = new Matrix(rows.Length, rows.Length == 0 ? 0 : rows[0].Length);
                for (int i = 0; i < rows.Length; i++)
                {
                    for (int j = 0; j < rows[i].Length; j++)
                    {
                        matrix[i, j] = rows[i][j];
                    }
                }

                var surface = new BlackVarianceSurface(
                    DateOf(spec.GetProperty("referenceDate")),
                    CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                    DatesOf(spec.GetProperty("dates")),
                    Doubles(spec.GetProperty("strikes")),
                    matrix,
                    dc,
                    (BlackVarianceSurfaceExtrapolation)spec.GetProperty("lower").GetInt32(),
                    (BlackVarianceSurfaceExtrapolation)spec.GetProperty("upper").GetInt32());
                return spec.GetProperty("interpolation").ValueKind == JsonValueKind.Null
                    ? surface
                    : spec.GetProperty("interpolation").GetString() switch
                    {
                        "bicubic" => surface.SetInterpolation<Bicubic>(),
                        string s => throw new InvalidOperationException($"unknown interpolation '{s}'"),
                        null => surface,
                    };
            default:
                throw new InvalidOperationException($"unknown structure '{type}'");
        }
    }

    /// <summary>The yield curve a spec describes: a flat forward or a linear zero curve.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The curve.</returns>
    public static YieldTermStructure CurveOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        return type switch
        {
            "FlatForward" => new FlatForward(DateOf(spec.GetProperty("referenceDate")), spec.GetProperty("forward").GetDouble(), dc),
            "ZeroCurve" => new ZeroCurve(DatesOf(spec.GetProperty("dates")), Doubles(spec.GetProperty("rates")), dc),
            _ => throw new InvalidOperationException($"unknown curve '{type}'"),
        };
    }

    /// <summary>The Black-Scholes process a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which dates BlackScholesProcess's dividend curve.</param>
    /// <returns>The process.</returns>
    public static GeneralizedBlackScholesProcess ProcessOf(JsonElement spec, EvaluationContext context)
    {
        string type = spec.GetProperty("type").GetString()!;
        double x0 = spec.GetProperty("x0").GetDouble();
        YieldTermStructure riskFree = CurveOf(spec.GetProperty("riskFree"));
        BlackVolTermStructure vol = BlackVolOf(spec.GetProperty("volatility"), context);
        return type switch
        {
            "BlackScholesProcess" => BlackScholesProcess.Create(x0, riskFree, vol, context),
            "BlackProcess" => new BlackProcess(x0, riskFree, vol),
            "BlackScholesMertonProcess" => new BlackScholesMertonProcess(x0, CurveOf(spec.GetProperty("dividend")), riskFree, vol),
            "GarmanKohlagenProcess" => new GarmanKohlagenProcess(x0, CurveOf(spec.GetProperty("dividend")), riskFree, vol),
            "GeneralizedBlackScholesProcess" => new GeneralizedBlackScholesProcess(
                x0, CurveOf(spec.GetProperty("dividend")), riskFree, vol),
            _ => throw new InvalidOperationException($"unknown process '{type}'"),
        };
    }

    /// <summary>The generalized process a spec describes, given the local volatility a second spec describes.</summary>
    /// <param name="spec">The process spec.</param>
    /// <param name="local">The local volatility spec: a constant local volatility's reference date, volatility and day
    /// counter.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The process.</returns>
    public static GeneralizedBlackScholesProcess ProcessWithLocalVolOf(
        JsonElement spec, JsonElement local, EvaluationContext context) =>
        new(
            spec.GetProperty("x0").GetDouble(),
            CurveOf(spec.GetProperty("dividend")),
            CurveOf(spec.GetProperty("riskFree")),
            BlackVolOf(spec.GetProperty("volatility"), context),
            new LocalConstantVol(
                DateOf(local.GetProperty("referenceDate")),
                local.GetProperty("volatility").GetDouble(),
                CashFlowInputs.DayCounterOf(local.GetProperty("dayCounter"))));

    /// <summary>The striked payoff a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The payoff.</returns>
    public static StrikedTypePayoff PayoffOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        var optionType = (OptionType)spec.GetProperty("optionType").GetInt32();
        double strike = spec.GetProperty("strike").GetDouble();
        return type switch
        {
            "PlainVanilla" => new PlainVanillaPayoff(optionType, strike),
            "CashOrNothing" => new CashOrNothingPayoff(optionType, strike, spec.GetProperty("cashPayoff").GetDouble()),
            "AssetOrNothing" => new AssetOrNothingPayoff(optionType, strike),
            "Gap" => new GapPayoff(optionType, strike, spec.GetProperty("secondStrike").GetDouble()),
            "PercentageStrike" => new PercentageStrikePayoff(optionType, spec.GetProperty("moneyness").GetDouble()),
            _ => throw new InvalidOperationException($"unknown payoff '{type}'"),
        };
    }

    /// <summary>The exercise a spec describes: European at its date, or American from the evaluation date to
    /// it.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The exercise.</returns>
    public static Exercise ExerciseOf(JsonElement spec, EvaluationContext context)
    {
        Date date = DateOf(spec.GetProperty("date"));
        return spec.GetProperty("type").GetString() switch
        {
            "European" => new EuropeanExercise(date),
            "American" => new AmericanExercise(context.EvaluationDate, date),
            string s => throw new InvalidOperationException($"unknown exercise '{s}'"),
            null => throw new InvalidOperationException("no exercise type"),
        };
    }

    /// <summary>The dividends a spec list describes.</summary>
    /// <param name="spec">The list: fixed amounts and fractional rates, each with its date.</param>
    /// <returns>The dividends.</returns>
    public static ImmutableArray<Dividend> DividendsOf(JsonElement spec) =>
    [
        .. spec.EnumerateArray().Select(d => d.GetProperty("type").GetString() switch
        {
            "Fixed" => (Dividend)new FixedDividend(d.GetProperty("amount").GetDouble(), DateOf(d.GetProperty("date"))),
            "Fractional" => new FractionalDividend(d.GetProperty("rate").GetDouble(), DateOf(d.GetProperty("date"))),
            string s => throw new InvalidOperationException($"unknown dividend '{s}'"),
            null => throw new InvalidOperationException("no dividend type"),
        }),
    ];

    /// <summary>A value computed when compared, so a refusal compares as QuantLib's throw.</summary>
    /// <param name="f">The computation.</param>
    /// <returns>The computation, as the vector assertion takes it.</returns>
    public static Func<object?> Lazy(Func<object?> f) => f;

    /// <summary>The doubles a JSON array holds.</summary>
    /// <param name="spec">The array.</param>
    /// <returns>The doubles.</returns>
    public static double[] Doubles(JsonElement spec) => [.. spec.EnumerateArray().Select(e => e.GetDouble())];

    private static Date DateOf(JsonElement spec) => new(spec.GetInt32());

    private static Date[] DatesOf(JsonElement spec) => [.. spec.EnumerateArray().Select(DateOf)];
}
