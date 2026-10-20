// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The piecewise year-on-year inflation curve vectors,
/// <c>tests/vectors/termstructures/inflation/piecewiseyoyinflationcurve.json</c>:
/// <see cref="PiecewiseYoYInflation"/> bootstrapped on <see cref="YearOnYearInflationSwapHelper"/>s, its nodes and
/// rates, the helpers' dates and implied quotes, and the curve after
/// <see cref="InflationTermStructureExtensions.SetSeasonality"/>; the outputs the stability filter dropped are null.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PiecewiseYoYInflationCurveVectorTests
{
    [Fact]
    public void PiecewiseYoYInflationCurveVector()
    {
        var file = VectorFile.Load("termstructures/inflation/piecewiseyoyinflationcurve");
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
            YieldTermStructure nominal = CashFlowInputs.CurveOf(c.Input("nominalCurve"));
            YearOnYearInflationSwapHelper[] helpers =
            [
                .. c.Input("helpers").EnumerateArray().Select(h => new YearOnYearInflationSwapHelper(
                    h.GetProperty("quote").GetDouble(),
                    lag,
                    new Date(h.GetProperty("maturity").GetInt32()),
                    calendar,
                    convention,
                    dc,
                    (YoYInflationIndex)index,
                    interpolation,
                    nominal,
                    context,
                    pillar)),
            ];
            var curve = new PiecewiseYoYInflation(
                c.DateInput("referenceDate"),
                c.DateInput("baseDate"),
                c.Double("baseYoYRate"),
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
            check.Output(c, "data", PiecewiseZeroInflationCurveVectorTests.Kept(dropped, "data", curve.Data().Select(x => (Func<object?>)(() => x))));
            check.Output(c, "rates", PiecewiseZeroInflationCurveVectorTests.Kept(dropped, "rates", dates.Select(d => (Func<object?>)(() => curve.YoyRate(d)))));
            check.Output(
                c,
                "impliedQuotes",
                PiecewiseZeroInflationCurveVectorTests.Kept(dropped, "impliedQuotes", helpers.Select(h => (Func<object?>)(() => h.SetTermStructure(curve).ImpliedQuote(context)))));
            if (c.Input("seasonality").ValueKind != JsonValueKind.Null)
            {
                PiecewiseYoYInflation seasonal =
                    curve.SetSeasonality(InflationInputs.SeasonalityOf(c.Input("seasonality")), context);
                check.Output(
                    c, "seasonalData", PiecewiseZeroInflationCurveVectorTests.Kept(dropped, "seasonalData", seasonal.Data().Select(x => (Func<object?>)(() => x))));
                check.Output(
                    c,
                    "seasonalRates",
                    PiecewiseZeroInflationCurveVectorTests.Kept(dropped, "seasonalRates", dates.Select(d => (Func<object?>)(() => seasonal.YoyRate(d)))));
            }
        }

        check.Complete();
    }
}
