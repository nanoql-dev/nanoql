// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's basket vectors, <c>tests/vectors/cxx/experimental/credit/basket.json</c>: baskets bound to
/// constant-loss models, their inspectors, their reads by date, and the statistics the model gives and refuses; and a
/// basket on the base loss model, each statistic its default's refusal.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BasketCxxVectorTests
{
    [Fact]
    public void BasketCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/basket");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Name == "refused baskets")
            {
                JsonElement model = c.Input("model");
                check.Output(c, "inspect", Each(
                    c.Input("baskets").EnumerateArray(), b => Inspect(PortfolioInputs.BoundBasketOf(b, model, context))));
                continue;
            }

            Basket basket = PortfolioInputs.BoundBasketOf(c.Input("basket"), c.Input("model"), context);
            if (c.Name == "the base model's defaults")
            {
                Date d5 = context.EvaluationDate + 1826;
                check.Output(c, "inspect", Inspect(basket));
                check.Output(c, "defaults", new Func<object?>[]
                {
                    () => basket.ExpectedTrancheLoss(d5),
                    () => basket.ProbOverLoss(d5, 0.5),
                    () => basket.Percentile(d5, 0.9),
                    () => basket.ExpectedShortfall(d5, 0.9),
                    () => basket.SplitVaRLevel(d5, 10.0),
                    () => basket.LossDistribution(d5),
                    () => basket.ProbsBeingNthEvent(1, d5),
                    () => basket.DefaultCorrelation(d5, 0, 1),
                    () => basket.ProbAtLeastNEvents(1, d5),
                    () => basket.RecoveryRate(d5, 0),
                });
                continue;
            }

            Date[] dates = [.. c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
            bool[] restated = [.. c.Input("restated").EnumerateArray().Select(x => x.GetBoolean())];
            int n = basket.Size;
            check.Output(c, "inspect", Inspect(basket));
            check.Output(c, "settledLoss", Each(dates, d => basket.SettledLoss(d)));
            check.Output(c, "cumulatedLoss", Each(dates, d => basket.CumulatedLoss(d)));
            check.Output(c, "remainingNotional", Each(dates, d => basket.RemainingNotional(d)));
            check.Output(c, "remainingNotionals", Each(dates, d => basket.RemainingNotionals(d).ToArray()));
            check.Output(c, "remainingNames", Each(dates, d => basket.RemainingNames(d).ToArray()));
            check.Output(c, "remainingSize", Each(dates, d => basket.RemainingSize(d)));
            check.Output(c, "liveList", Each(dates, d => basket.LiveList(d).ToArray()));
            Date[] kept = [.. dates.Where((_, i) => restated[i])];
            check.Output(c, "remainingAttachmentAmount", Each(kept, d => basket.RemainingAttachmentAmount(d)));
            check.Output(c, "remainingDetachmentAmount", Each(kept, d => basket.RemainingDetachmentAmount(d)));
            check.Output(c, "remainingTrancheNotional", Each(kept, d => basket.RemainingTrancheNotional(d)));
            check.Output(c, "probabilities", Each(dates, d => basket.Probabilities(d).ToArray()));
            check.Output(c, "remainingProbabilities", Each(dates, d => basket.RemainingProbabilities(d).ToArray()));
            check.Output(c, "exposure", Each(
                Enumerable.Range(0, n).SelectMany(i => dates.Take(2).Select(d => (i, d))),
                p => basket.Exposure($"Name{p.i}", p.d)));
            check.Output(c, "probAtLeastNEvents", Each(
                Enumerable.Range(0, n + 2).SelectMany(k => dates.Select(d => (k, d))),
                p => basket.ProbAtLeastNEvents(p.k, p.d)));
            check.Output(c, "defaultCorrelation", Each(
                dates[1..4].SelectMany(d => Enumerable.Range(0, n).SelectMany(i => Enumerable.Range(0, n).Select(j => (d, i, j)))),
                p => basket.DefaultCorrelation(p.d, p.i, p.j)));
            check.Output(c, "recoveryRate", Each(
                dates[..3].SelectMany(d => Enumerable.Range(0, n).Select(i => (d, i))),
                p => basket.RecoveryRate(p.d, p.i)));
            Date d1 = context.EvaluationDate + 1826;
            check.Output(c, "refused", new Func<object?>[]
            {
                () => basket.ProbsBeingNthEvent(1, d1),
                () => basket.ExpectedTrancheLoss(d1),
                () => basket.ProbOverLoss(d1, 0.5),
                () => basket.Percentile(d1, 0.9),
                () => basket.ExpectedShortfall(d1, 0.9),
                () => basket.SplitVaRLevel(d1, 10.0),
                () => basket.LossDistribution(d1),
                () => basket.SettledLoss(context.EvaluationDate - 1000),
            });
        }

        check.Complete();
    }

    private static object?[] Inspect(Basket x) =>
    [
        x.Size, x.Notional, x.BasketNotional, x.TrancheNotional, x.AttachmentAmount, x.DetachmentAmount,
        x.AttachmentRatio, x.DetachmentRatio, x.RefDate, x.RemainingNotional(), x.RemainingSize(),
        x.RemainingNames().ToArray(), x.RemainingNotionals().ToArray(), x.LiveList().ToArray(), x.SettledLoss(),
        x.CumulatedLoss(), x.RemainingAttachmentAmount(), x.RemainingDetachmentAmount(), x.RemainingTrancheNotional(),
        x.DefaultKeys.Select(PortfolioInputs.KeySummary).ToArray(),
    ];

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
