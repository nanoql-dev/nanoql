// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The inflation index vectors, <c>tests/vectors/indexes/inflationindex.json</c>: the zero-inflation and year-on-year
/// indexes' inspectors; fixings written through <see cref="IndexFixings.AddFixing"/>, on every day of their inflation
/// period, and the store read back whole; reads, forecasts and clones; and <see cref="CPI"/>'s lagged observations.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InflationIndexVectorTests
{
    [Fact]
    public void InflationIndexVector()
    {
        var file = VectorFile.Load("indexes/inflationindex");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("index");
            InflationIndex index = InflationInputs.IndexOf(spec);
            switch (c.String("kind"))
            {
                case "inspectors":
                    Inspectors(c, check, index);
                    break;
                case "store":
                    Store(c, check, index);
                    break;
                case "reads":
                {
                    EvaluationContext context = WithFixings(c, index);
                    Date[] dates = c.DateInputs("dates");
                    check.Output(c, "lastFixingDate", () => LastFixingDate(index, context));
                    check.Output(c, "needsForecast", Each(dates, d => NeedsForecast(index, d, context)));
                    check.Output(c, "fixing", Each(dates, d => index.Fixing(d, context)));
                    check.Output(c, "pastFixing", Each(dates, d => index.PastFixing(d, context)));
                    break;
                }

                case "forecasts":
                {
                    EvaluationContext context = WithFixings(c, index);
                    Date[] dates = c.DateInputs("dates");
                    InflationIndex clone = index switch
                    {
                        ZeroInflationIndex zero => zero.Clone(InflationInputs.ZeroCurveOf(c.Input("cloneCurve"))),
                        YoYInflationIndex yoy => yoy.Clone(InflationInputs.YoYCurveOf(c.Input("cloneCurve"))),
                        _ => throw new InvalidOperationException("an inflation index of another kind"),
                    };
                    check.Output(c, "needsForecast", Each(dates, d => NeedsForecast(index, d, context)));
                    check.Output(c, "fixing", Each(dates, d => index.Fixing(d, context)));
                    check.Output(c, "cloneName", clone.Name);
                    check.Output(c, "cloneFixing", Each(dates, d => clone.Fixing(d, context)));
                    break;
                }

                case "lagged":
                {
                    EvaluationContext context = WithFixings(c, index);
                    Period[] lags = [.. c.Input("lags").EnumerateArray().Select(InflationInputs.LagOf)];
                    CPIInterpolationType[] types = [.. c.Ints("interpolations").Select(i => (CPIInterpolationType)i)];
                    check.Output(
                        c,
                        "lagged",
                        c.DateInputs("dates").Select(d => lags.Select(lag => types
                            .Select(t => (Func<object?>)(() => Lagged(index, d, lag, t, context))).ToArray())
                            .ToArray()).ToArray());
                    break;
                }

                default:
                    throw new InvalidOperationException($"unknown case kind in {c.Name}");
            }
        }

        check.Complete();
    }

    private static void Inspectors(VectorCase c, VectorAssert check, InflationIndex index)
    {
        check.Output(c, "name", index.Name);
        check.Output(c, "familyName", index.FamilyName);
        check.Output(c, "regionName", index.Region.Name);
        check.Output(c, "regionCode", index.Region.Code);
        check.Output(c, "revised", index.Revised);
        check.Output(c, "frequency", index.Frequency);
        check.Output(c, "availabilityLagLength", index.AvailabilityLag.Length);
        check.Output(c, "availabilityLagUnits", index.AvailabilityLag.Units);
        check.Output(c, "currency", index.Currency.Code);
        check.Output(c, "fixingCalendar", index.FixingCalendar.Name);
        check.Output(c, "isValidFixingDate", index.IsValidFixingDate(c.DateInput("weekend")));
        var yoy = index as YoYInflationIndex;
        check.Output(c, "ratio", yoy?.Ratio);
        check.Output(c, "underlying", yoy?.UnderlyingIndex?.Name);
    }

    private static void Store(VectorCase c, VectorAssert check, InflationIndex index)
    {
        EvaluationContext context = WithFixings(c, index);
        FinancialIndex owner = Owner(index);
        JsonElement[] adds = [.. c.Input("adds").EnumerateArray()];
        bool refused = c.Expected.TryGetProperty("refusedAdd", out _);
        foreach (JsonElement add in refused ? adds[..^1] : adds)
        {
            context = context with
            {
                Fixings = context.Fixings.AddFixing(owner, new Date(add[0].GetInt32()), add[1].GetDouble(), add[2].GetBoolean()),
            };
        }

        if (refused)
        {
            JsonElement last = adds[^1];
            check.Output(
                c,
                "refusedAdd",
                () => context.Fixings.AddFixing(owner, new Date(last[0].GetInt32()), last[1].GetDouble(), last[2].GetBoolean())
                    is null ? 1 : 0);
            return;
        }

        TimeSeries<double> stored = owner.TimeSeries(context);
        check.Output(c, "storedDates", stored.Dates());
        check.Output(c, "storedValues", stored.Values());
        check.Output(c, "lastFixingDate", LastFixingDate(index, context));
    }

    // The case's fixings, added one at a time through the store's AddFixing, on the index or a ratio's underlying.
    private static EvaluationContext WithFixings(VectorCase c, InflationIndex index)
    {
        EvaluationContext context = c.Context();
        FinancialIndex owner = Owner(index);
        foreach (JsonElement f in c.Input("fixings").EnumerateArray())
        {
            context = context with
            {
                Fixings = context.Fixings.AddFixing(owner, new Date(f[0].GetInt32()), f[1].GetDouble()),
            };
        }

        return context;
    }

    private static FinancialIndex Owner(InflationIndex index) =>
        index is YoYInflationIndex { UnderlyingIndex: { } underlying } ? underlying : index;

    private static Func<object?>[] Each<T>(Date[] dates, Func<Date, T> f) =>
        [.. dates.Select(d => (Func<object?>)(() => f(d)))];

    private static bool NeedsForecast(InflationIndex index, Date d, EvaluationContext context) =>
        index switch
        {
            ZeroInflationIndex zero => zero.NeedsForecast(d, context),
            YoYInflationIndex yoy => yoy.NeedsForecast(d, context),
            _ => throw new InvalidOperationException("an inflation index of another kind"),
        };

    private static Date LastFixingDate(InflationIndex index, EvaluationContext context) =>
        index switch
        {
            ZeroInflationIndex zero => zero.LastFixingDate(context),
            YoYInflationIndex yoy => yoy.LastFixingDate(context),
            _ => throw new InvalidOperationException("an inflation index of another kind"),
        };

    private static double Lagged(InflationIndex index, Date d, Period lag, CPIInterpolationType type, EvaluationContext context) =>
        index switch
        {
            ZeroInflationIndex zero => CPI.LaggedFixing(zero, d, lag, type, context),
            YoYInflationIndex yoy => CPI.LaggedYoYRate(yoy, d, lag, type, context),
            _ => throw new InvalidOperationException("an inflation index of another kind"),
        };
}
