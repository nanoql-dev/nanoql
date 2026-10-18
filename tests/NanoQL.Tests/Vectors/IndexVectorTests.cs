// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The fixing-store vectors, <c>tests/vectors/index.json</c>: scenarios of adds, batches, duplicates, invalid dates,
/// names differing in case and clears, each replayed on <see cref="IndexFixings"/> from the case's context, and the
/// store read back through <see cref="IndexFixings.Histories"/> and the index's
/// <see cref="FinancialIndex.TimeSeries"/>, <see cref="FinancialIndex.HasHistoricalFixing"/> and
/// <see cref="FinancialIndex.PastFixing"/>. A scenario whose last step QuantLib refuses has that refusal as its one
/// output.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IndexVectorTests
{
    [Fact]
    public void IndexVector()
    {
        var file = VectorFile.Load("index");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FinancialIndex[] indexes = c.Input("indexes").EnumerateArray().Select(IndexInputs.IndexOf).ToArray();
            JsonElement[] steps = [.. c.Input("steps").EnumerateArray()];
            bool refused = c.Expected.TryGetProperty("refused", out _);

            // The case's context, and every step but a refused last one, must succeed; a failure among them fails
            // each output.
            EvaluationContext? context = null;
            Exception? failure = null;
            try
            {
                context = c.Context();
                IndexFixings store = context.Fixings;
                foreach (JsonElement step in steps.Take(refused ? steps.Length - 1 : steps.Length))
                {
                    store = IndexInputs.Apply(store, step, indexes);
                }

                context = context with { Fixings = store };
            }
            catch (Exception e)
            {
                failure = e;
            }

            EvaluationContext After() => failure is null
                ? context!
                : throw new InvalidOperationException($"the context or a step threw: {failure.Message}", failure);

            if (refused)
            {
                check.Output(c, "refused", () =>
                {
                    IndexInputs.Apply(After().Fixings, steps[^1], indexes);
                    return null;
                });
                continue;
            }

            // QuantLib's stored dates, at which the reads are made.
            Date[][] stored = c.Expected.GetProperty("timeSeries").EnumerateArray()
                .Select(s => s.EnumerateArray().Select(p => new Date(p[0].GetInt32())).ToArray())
                .ToArray();
            Date[] missing = c.DateInputs("missing");
            check.Output(c, "histories", () => After().Fixings.Histories());
            check.Output(c, "timeSeries", () => indexes.Select(i => Pairs(i.TimeSeries(After()))).ToArray());
            check.Output(
                c,
                "hasHistoricalFixing",
                () => At(indexes, stored, (i, d) => i.HasHistoricalFixing(d, After())));
            check.Output(c, "pastFixing", () => At(indexes, stored, (i, d) => i.PastFixing(d, After())));
            check.Output(
                c,
                "missingHasHistoricalFixing",
                () => missing.Select(d => indexes[0].HasHistoricalFixing(d, After())).ToArray());
            check.Output(
                c,
                "missingPastFixing",
                missing.Select(d => (Func<object?>)(() => indexes[0].PastFixing(d, After()))).ToArray());
            if (c.Expected.TryGetProperty("usingAtParCoupons", out _))
            {
                check.Output(c, "usingAtParCoupons", () => After().UseAtParCoupons);
            }
        }

        check.Complete();
    }

    /// <summary>A series as [date, value] pairs in date order.</summary>
    internal static object[][] Pairs(TimeSeries<double> series) =>
        series.Select(p => new object[] { p.Key, p.Value }).ToArray();

    /// <summary>A read of each index at each of its dates.</summary>
    private static object?[][] At<T>(FinancialIndex[] indexes, Date[][] dates, Func<FinancialIndex, Date, T> read) =>
        indexes.Zip(dates, (i, ds) => ds.Select(d => (object?)read(i, d)).ToArray()).ToArray();
}
