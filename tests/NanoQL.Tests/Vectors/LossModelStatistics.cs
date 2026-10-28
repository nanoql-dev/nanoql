// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The loss models' statistics through their basket, as <c>tools/vectors/gen/cxx/experimental/credit/defaultevent.py</c>'s
/// <c>model_statistics</c> records them: each output the generator wrote, read on the bound basket.
/// </summary>
internal static class LossModelStatistics
{
    /// <summary>Checks a vector file of loss-model statistics.</summary>
    /// <param name="name">The vector file's name.</param>
    public static void Check(string name)
    {
        var file = VectorFile.Load(name);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Basket basket = PortfolioInputs.BoundBasketOf(c.Input("basket"), c.Input("model"), context);
            Date[] dates = [.. c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
            double[] levels = Reals(c.Input("levels"));
            double[] shortfalls = Reals(c.Input("shortfalls"));
            double[] fractions = Reals(c.Input("fractions"));
            double[] splits = Reals(c.Input("splits"));
            int n = basket.Size;
            check.Output(c, "expectedTrancheLoss", Each(dates, d => basket.ExpectedTrancheLoss(d)));
            Optional(c, check, "percentile", () => Each(dates.SelectMany(d => levels.Select(p => (d, p))), x => basket.Percentile(x.d, x.p)));
            Optional(c, check, "expectedShortfall", () => Each(dates.SelectMany(d => shortfalls.Select(p => (d, p))), x => basket.ExpectedShortfall(x.d, x.p)));
            Optional(c, check, "probOverLoss", () => Each(dates.SelectMany(d => fractions.Select(f => (d, f))), x => basket.ProbOverLoss(x.d, x.f)));
            Optional(c, check, "probAtLeastNEvents", () => Each(dates.SelectMany(d => Enumerable.Range(0, n + 2).Select(k => (d, k))), x => basket.ProbAtLeastNEvents(x.k, x.d)));
            Optional(c, check, "probsBeingNthEvent", () => Each(dates.SelectMany(d => Enumerable.Range(1, n).Select(k => (d, k))), x => basket.ProbsBeingNthEvent(x.k, x.d).ToArray()));
            Optional(c, check, "splitVaRLevel", () => Each(dates.SelectMany(d => splits.Select(s => (d, s))), x => basket.SplitVaRLevel(x.d, x.s).ToArray()));
            Optional(c, check, "lossDistribution", () => Each(dates, d => basket.LossDistribution(d).Select(kv => new object[] { kv.Key, kv.Value }).ToArray()));
            Optional(c, check, "recoveryRate", () => Each(Enumerable.Range(0, n), i => basket.RecoveryRate(dates[0], i)));
            Date last = dates[^1];
            check.Output(c, "refused", Each(c.Input("refused").EnumerateArray().Select(r => r.GetString()!), r => r switch
            {
                "probsBeingNthEvent" => basket.ProbsBeingNthEvent(1, last).ToArray(),
                "expectedTrancheLoss" => basket.ExpectedTrancheLoss(last),
                "probOverLoss" => basket.ProbOverLoss(last, 0.5),
                "percentile" => basket.Percentile(last, 0.9),
                "expectedShortfall" => basket.ExpectedShortfall(last, 0.9),
                "splitVaRLevel" => basket.SplitVaRLevel(last, 1.0).ToArray(),
                "lossDistribution" => basket.LossDistribution(last),
                "recoveryRate" => basket.RecoveryRate(last, 0),
                "probAtLeastNEvents" => basket.ProbAtLeastNEvents(1, last),
                "defaultCorrelation" => (object)basket.DefaultCorrelation(last, 0, 1),
                _ => throw new ArgumentException($"unknown statistic {r}", nameof(name)),
            }));
        }

        check.Complete();
    }

    private static void Optional(VectorCase c, VectorAssert check, string output, Func<object?> actual)
    {
        if (c.Expected.TryGetProperty(output, out _))
        {
            check.Output(c, output, actual());
        }
    }

    private static double[] Reals(JsonElement e) => [.. e.EnumerateArray().Select(VectorCase.Real)];

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
