// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's index vectors, <c>tests/vectors/cxx/index.json</c>:
/// <see cref="FinancialIndex.AllowsNativeFixings"/>, the store's refusal of every writer for an index without native
/// fixings, and <see cref="IndexFixings.AddFixings(FinancialIndex, TimeSeries{double}, bool)"/>, which the wheel
/// doesn't reach, replayed on <see cref="IndexFixings"/> and read back through <see cref="IndexFixings.Histories"/>
/// and <see cref="FinancialIndex.TimeSeries"/>. A scenario whose last step QuantLib refuses has that refusal as its
/// one output.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IndexCxxVectorTests
{
    [Fact]
    public void IndexCxxVector()
    {
        var file = VectorFile.Load("cxx/index");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            FinancialIndex[] indexes = c.Input("indexes").EnumerateArray().Select(IndexInputs.IndexOf).ToArray();
            if (c.Expected.TryGetProperty("allowsNativeFixings", out _))
            {
                check.Output(c, "allowsNativeFixings", indexes.Select(i => i.AllowsNativeFixings).ToArray());
                continue;
            }

            if (c.Inputs.TryGetProperty("refusals", out JsonElement refusals))
            {
                check.Output(
                    c,
                    "refused",
                    refusals.EnumerateArray()
                        .Select(s => (Func<object?>)(() => IndexInputs.Apply(context.Fixings, s, indexes)))
                        .ToArray());
                check.Output(c, "histories", context.Fixings.Histories());
                continue;
            }

            // The steps before the last must succeed; a failure among them fails each output.
            JsonElement[] steps = [.. c.Input("steps").EnumerateArray()];
            IndexFixings store = context.Fixings;
            Exception? failure = null;
            try
            {
                foreach (JsonElement step in steps[..^1])
                {
                    store = IndexInputs.Apply(store, step, indexes);
                }
            }
            catch (Exception e)
            {
                failure = e;
            }

            IndexFixings Last() => failure is null
                ? IndexInputs.Apply(store, steps[^1], indexes)
                : throw new InvalidOperationException($"a step before the last threw: {failure.Message}", failure);

            if (c.Expected.TryGetProperty("refused", out _))
            {
                check.Output(c, "refused", Last);
                continue;
            }

            check.Output(c, "histories", () => Last().Histories());
            check.Output(
                c,
                "timeSeries",
                () =>
                {
                    EvaluationContext after = context with { Fixings = Last() };
                    return indexes.Select(i => IndexVectorTests.Pairs(i.TimeSeries(after))).ToArray();
                });
        }

        check.Complete();
    }
}
