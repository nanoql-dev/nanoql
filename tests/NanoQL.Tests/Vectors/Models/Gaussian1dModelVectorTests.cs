// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Gaussian1d model vectors, <c>tests/vectors/models/shortrate/onefactormodels/gaussian1dmodel.json</c>: the
/// members of <see cref="Gaussian1dModel"/> through three GSR models (<see cref="Gaussian1dInputs"/>): zero bonds and
/// numeraires by time and by date, the forward rate, the swap rates and annuities of four swap indexes, the compounded
/// rates of overnight coupons and zero-bond options, each also on a given curve where the member takes one.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Gaussian1dModelVectorTests
{
    [Fact]
    public void Gaussian1dModelVector()
    {
        var file = VectorFile.Load("models/shortrate/onefactormodels/gaussian1dmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Gsr model = Gaussian1dInputs.GsrOf(c.Input("model"));
            YieldTermStructure yts = CashFlowInputs.CurveOf(c.Input("yts"));
            IborIndex index = SwapInputs.IndexOf(c.Input("iborIndex"));
            SwapIndex[] swapIndexes = [.. c.Input("swapIndexes").EnumerateArray().Select(SwapIndexInputs.SwapIndexOf)];
            OvernightIndexedSwap[] swaps = [.. c.Input("couponSwaps").EnumerateArray().Select(s => SwapInputs.OvernightIndexedSwapOf(s, context))];
            OvernightIndexedCoupon[] coupons = [.. c.Input("coupons").EnumerateArray().Select(p =>
                (OvernightIndexedCoupon)swaps[p[0].GetInt32()].OvernightLeg[p[1].GetInt32()])];

            double[][] zbTimes = ModelInputs.Points(c, "zerobondTimes");
            check.Output(c, "zerobondTime", zbTimes.Select(p => (object?)model.Zerobond(p[0], p[1], p[2])).ToArray());
            check.Output(c, "zerobondTimeOnCurve", zbTimes.Select(p => (object?)model.Zerobond(p[0], p[1], p[2], yts)).ToArray());
            JsonElement[] zbDates = [.. c.Input("zerobondDates").EnumerateArray()];
            check.Output(c, "zerobondDate", zbDates.Select(p => (object?)model.Zerobond(DateOf(p[0]), OptionalDate(p[1]), p[2].GetDouble())).ToArray());
            check.Output(c, "zerobondDateOnCurve", zbDates.Select(p => (object?)model.Zerobond(DateOf(p[0]), OptionalDate(p[1]), p[2].GetDouble(), yts)).ToArray());

            double[][] numTimes = ModelInputs.Points(c, "numeraireTimes");
            check.Output(c, "numeraireTime", numTimes.Select(p => (object?)model.Numeraire(p[0], p[1])).ToArray());
            check.Output(c, "numeraireTimeOnCurve", numTimes.Select(p => (object?)model.Numeraire(p[0], p[1], yts)).ToArray());
            JsonElement[] numDates = [.. c.Input("numeraireDates").EnumerateArray()];
            check.Output(c, "numeraireDate", numDates.Select(p => (object?)model.Numeraire(DateOf(p[0]), p[1].GetDouble())).ToArray());
            check.Output(c, "numeraireDateOnCurve", numDates.Select(p => (object?)model.Numeraire(DateOf(p[0]), p[1].GetDouble(), yts)).ToArray());

            check.Output(c, "forwardRate", c.Input("forwardPoints").EnumerateArray()
                .Select(p => (object?)model.ForwardRate(DateOf(p[0]), context, OptionalDate(p[1]), p[2].GetDouble(), index)).ToArray());

            JsonElement[] swapPoints = [.. c.Input("swapPoints").EnumerateArray()];
            check.Output(c, "swapRate", swapIndexes.Select(s => (object?)swapPoints
                .Select(p => (object?)model.SwapRate(DateOf(p[0]), SwapInputs.PeriodOf(p[1]), context, OptionalDate(p[2]), p[3].GetDouble(), s)).ToArray()).ToArray());
            check.Output(c, "swapAnnuity", swapIndexes.Select(s => (object?)swapPoints
                .Select(p => (object?)model.SwapAnnuity(DateOf(p[0]), SwapInputs.PeriodOf(p[1]), context, OptionalDate(p[2]), p[3].GetDouble(), s)).ToArray()).ToArray());

            JsonElement[] compounded = [.. c.Input("compoundedPoints").EnumerateArray()];
            check.Output(c, "compoundedRate", coupons.Select(k => (object?)compounded
                .Select(p => (object?)model.CompoundedRate(k, OptionalDate(p[0]), p[1].GetDouble())).ToArray()).ToArray());
            check.Output(c, "compoundedRateOnCurve", coupons.Select(k => (object?)compounded
                .Select(p => (object?)model.CompoundedRate(k, OptionalDate(p[0]), p[1].GetDouble(), yts)).ToArray()).ToArray());

            JsonElement[] zbo = [.. c.Input("zerobondOptionPoints").EnumerateArray()];
            check.Output(c, "zerobondOption", zbo.Select(p => (object?)ZerobondOption(model, p, null)).ToArray());
            check.Output(c, "zerobondOptionOnCurve", zbo.Select(p => (object?)ZerobondOption(model, p, yts)).ToArray());
        }

        check.Complete();
    }

    private static double ZerobondOption(Gsr model, JsonElement p, YieldTermStructure? yts) =>
        model.ZerobondOption(
            (OptionType)p[0].GetInt32(),
            DateOf(p[1]),
            DateOf(p[2]),
            DateOf(p[3]),
            p[4].GetDouble(),
            OptionalDate(p[5]),
            p[6].GetDouble(),
            yts,
            p[7].GetDouble(),
            p[8].GetInt32(),
            p[9].GetBoolean(),
            false);

    private static Date DateOf(JsonElement e) => new(e.GetInt32());

    private static Date? OptionalDate(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : new Date(e.GetInt32());
}
