// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The inflation term structure vectors, <c>tests/vectors/termstructures/inflationtermstructure.json</c>:
/// <see cref="InflationTermStructureFunctions.InflationPeriod"/> for every frequency and
/// <see cref="InflationTermStructureFunctions.InflationYearFraction"/>; and the base classes' members on zero-inflation
/// and year-on-year curves: the inspectors, the rates' range checks by date and by time, and
/// <see cref="InflationTermStructureExtensions.SetSeasonality"/>, QuantLib's setter, then the seasonality removed.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InflationTermStructureVectorTests
{
    [Fact]
    public void InflationTermStructureVector()
    {
        var file = VectorFile.Load("termstructures/inflationtermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "periods":
                    Date[] dates = c.DateInputs("dates");
                    Frequency[] frequencies = [.. c.Ints("frequencies").Select(f => (Frequency)f)];
                    check.Output(
                        c,
                        "start",
                        dates.Select(d => frequencies
                            .Select(f => (Func<object?>)(() => InflationTermStructureFunctions.InflationPeriod(d, f).Start))
                            .ToArray()).ToArray());
                    check.Output(
                        c,
                        "end",
                        dates.Select(d => frequencies
                            .Select(f => (Func<object?>)(() => InflationTermStructureFunctions.InflationPeriod(d, f).End))
                            .ToArray()).ToArray());
                    break;
                case "yearFractions":
                    DayCounter dc = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
                    Date[] ends = c.DateInputs("dates");
                    Frequency[] handled = [.. c.Ints("frequencies").Select(f => (Frequency)f)];
                    check.Output(
                        c,
                        "inflationYearFraction",
                        c.DateInputs("starts").Select(s => ends.Select(d => handled.Select(f => new[] { false, true }
                            .Select(i => (Func<object?>)(() =>
                                InflationTermStructureFunctions.InflationYearFraction(f, i, dc, s, d)))
                            .ToArray()).ToArray()).ToArray()).ToArray());
                    break;
                case "members":
                    Members(c, check);
                    break;
                default:
                    throw new InvalidOperationException($"unknown case kind in {c.Name}");
            }
        }

        check.Complete();
    }

    private static void Members(VectorCase c, VectorAssert check)
    {
        InflationTermStructure curve = InflationInputs.CurveOf(c.Input("curve"));
        check.Output(c, "referenceDate", curve.ReferenceDate);
        check.Output(c, "baseDate", curve.BaseDate);
        check.Output(c, "frequency", curve.Frequency);
        check.Output(c, "baseRate", () => curve.BaseRate());
        check.Output(c, "hasSeasonality", curve.HasSeasonality);
        check.Output(c, "maxDate", curve.MaxDate);
        bool[] extrapolate = [false, true];
        check.Output(
            c,
            "rateAtDates",
            c.DateInputs("rangeDates").Select(d => extrapolate
                .Select(e => (Func<object?>)(() => RateAt(curve, d, e))).ToArray()).ToArray());
        check.Output(
            c,
            "rateAtTimes",
            c.Doubles("rangeTimes").Select(t => extrapolate
                .Select(e => (Func<object?>)(() => RateAt(curve, t, e))).ToArray()).ToArray());

        EvaluationContext context = c.Context();
        Date[] dates = c.DateInputs("dates");
        InflationTermStructure copy = curve.EnableExtrapolation()
            .SetSeasonality(InflationInputs.SeasonalityOf(c.Input("seasonality")), context);
        check.Output(c, "hasSeasonalityAfterSet", copy.HasSeasonality);
        check.Output(c, "rateAfterSet", dates.Select(d => (Func<object?>)(() => RateAt(copy, d, false))).ToArray());
        InflationTermStructure unset = copy.SetSeasonality(null, context);
        check.Output(c, "hasSeasonalityAfterUnset", unset.HasSeasonality);
        check.Output(c, "rateAfterUnset", dates.Select(d => (Func<object?>)(() => RateAt(unset, d, false))).ToArray());
    }

    private static double RateAt(InflationTermStructure curve, Date d, bool extrapolate) =>
        curve is ZeroInflationTermStructure zero
            ? zero.ZeroRate(d, extrapolate)
            : ((YoYInflationTermStructure)curve).YoyRate(d, extrapolate);

    private static double RateAt(InflationTermStructure curve, double t, bool extrapolate) =>
        curve is ZeroInflationTermStructure zero
            ? zero.ZeroRate(t, extrapolate)
            : ((YoYInflationTermStructure)curve).YoyRate(t, extrapolate);
}
