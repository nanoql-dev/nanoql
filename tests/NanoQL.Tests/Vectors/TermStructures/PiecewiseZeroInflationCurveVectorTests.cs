// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The piecewise zero-inflation curve vectors,
/// <c>tests/vectors/termstructures/inflation/piecewisezeroinflationcurve.json</c>:
/// <see cref="PiecewiseZeroInflation"/> bootstrapped on <see cref="ZeroCouponInflationSwapHelper"/>s, its nodes and
/// rates, the helpers' dates and implied quotes, and the curve after
/// <see cref="InflationTermStructureExtensions.SetSeasonality"/>; the outputs the stability filter dropped are null.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PiecewiseZeroInflationCurveVectorTests
{
    [Fact]
    public void PiecewiseZeroInflationCurveVector()
    {
        var file = VectorFile.Load("termstructures/inflation/piecewisezeroinflationcurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            Period lag = InflationCashFlowInputs.PeriodOf(c.Input("observationLag"));
            Calendar calendar = CashFlowInputs.CalendarOf(c.Input("calendar"));
            var convention = (BusinessDayConvention)c.Int("convention");
            DayCounter dc = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            var interpolation = (CPIInterpolationType)c.Int("interpolation");
            var pillar = (PillarChoice)c.Int("pillar");
            ZeroCouponInflationSwapHelper[] helpers =
            [
                .. c.Input("helpers").EnumerateArray().Select(h => new ZeroCouponInflationSwapHelper(
                    h.GetProperty("quote").GetDouble(),
                    lag,
                    new Date(h.GetProperty("maturity").GetInt32()),
                    calendar,
                    convention,
                    dc,
                    (ZeroInflationIndex)index,
                    interpolation,
                    context,
                    pillar)),
            ];
            var curve = new PiecewiseZeroInflation(
                c.DateInput("referenceDate"),
                c.DateInput("baseDate"),
                (Frequency)c.Int("frequency"),
                CashFlowInputs.DayCounterOf(c.Input("curveDayCounter")),
                helpers,
                context,
                null,
                c.Double("accuracy"));
            Date[] dates = c.DateInputs("dates");
            JsonElement dropped = c.Input("dropped");
            check.Output(c, "dates", curve.Dates());
            check.Output(c, "times", curve.Times());
            check.Output(c, "baseDate", curve.BaseDate);
            check.Output(c, "maxDate", curve.MaxDate);
            check.Output(c, "pillarDates", helpers.Select(h => h.PillarDate));
            check.Output(c, "earliestDates", helpers.Select(h => h.EarliestDate));
            check.Output(c, "latestDates", helpers.Select(h => h.LatestDate));
            check.Output(c, "data", Kept(dropped, "data", curve.Data().Select(x => (Func<object?>)(() => x))));
            check.Output(c, "rates", Kept(dropped, "rates", dates.Select(d => (Func<object?>)(() => curve.ZeroRate(d)))));
            check.Output(
                c,
                "impliedQuotes",
                Kept(dropped, "impliedQuotes", helpers.Select(h => (Func<object?>)(() => h.SetTermStructure(curve).ImpliedQuote(context)))));
            if (c.Input("seasonality").ValueKind != JsonValueKind.Null)
            {
                PiecewiseZeroInflation seasonal =
                    curve.SetSeasonality(InflationInputs.SeasonalityOf(c.Input("seasonality")), context);
                check.Output(
                    c, "seasonalData", Kept(dropped, "seasonalData", seasonal.Data().Select(x => (Func<object?>)(() => x))));
                check.Output(
                    c,
                    "seasonalRates",
                    Kept(dropped, "seasonalRates", dates.Select(d => (Func<object?>)(() => seasonal.ZeroRate(d)))));
            }
        }

        check.Complete();
    }

    /// <summary>The outputs the stability filter kept, a dropped one null.</summary>
    /// <param name="dropped">The case's <c>"dropped"</c> input: each output's dropped indexes.</param>
    /// <param name="name">The output.</param>
    /// <param name="values">Its values.</param>
    /// <returns>The values, the dropped ones null.</returns>
    internal static Func<object?>[] Kept(JsonElement dropped, string name, IEnumerable<Func<object?>> values)
    {
        HashSet<int> skip = [.. dropped.GetProperty(name).EnumerateArray().Select(i => i.GetInt32())];
        return [.. values.Select((f, i) => skip.Contains(i) ? () => null : f)];
    }
}
