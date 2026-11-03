// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's risky asset swap vectors, <c>tests/vectors/cxx/experimental/credit/riskyassetswap.json</c>:
/// <see cref="RiskyAssetSwap"/>'s NPV and fair spread where its schedules coincide at recovery 0, its float annuity on
/// every case, and a hazard-rate curve bootstrapped on <see cref="AssetSwapHelper"/>s.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RiskyAssetSwapCxxVectorTests
{
    [Fact]
    public void RiskyAssetSwapCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/riskyassetswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            EvaluationContext context = c.Context();
            if (c.Input("kind").GetString() == "swap")
            {
                RiskyAssetSwap swap = SwapOf(a, context);
                check.Output(c, "floatAnnuity", () => swap.FloatAnnuity(context));
                if (!c.Bool("annuityOnly"))
                {
                    check.Output(c, "NPV", () => swap.Calculate(context).NPV);
                    check.Output(c, "fairSpread", () => swap.Calculate(context).FairSpread);
                }

                continue;
            }

            AssetSwapHelper[] helpers = [.. a[0].EnumerateArray().Select(h => HelperOf(h, context))];
            var curve = new PiecewiseDefaultCurve<HazardRate, BackwardFlat, IterativeBootstrap>(
                new Date(a[1].GetInt32()), helpers, new Actual365Fixed(), context);
            double[] times = [.. a[2].EnumerateArray().Select(VectorCase.Real)];
            check.Output(c, "dates", curve.Dates());
            check.Output(c, "data", curve.Data());
            check.Output(c, "survivalProbability", times.Select(t => curve.SurvivalProbability(t, true)).ToArray());
        }

        check.Complete();
    }

    // The swap the harness's first eleven arguments describe.
    internal static RiskyAssetSwap SwapOf(JsonElement[] a, EvaluationContext context) =>
        new(
            a[0].GetBoolean(),
            VectorCase.Real(a[1]),
            CreditInputs.ScheduleOf(a[2], context),
            CreditInputs.ScheduleOf(a[3], context),
            CreditInputs.DayCounterOf(a[4]),
            CreditInputs.DayCounterOf(a[5]),
            VectorCase.Real(a[6]),
            VectorCase.Real(a[7]),
            CreditInputs.DiscountOf(a[8])!,
            CreditInputs.CurveOf(a[9], context),
            a[10].ValueKind == JsonValueKind.Null ? null : VectorCase.Real(a[10]));

    private static AssetSwapHelper HelperOf(JsonElement h, EvaluationContext context)
    {
        JsonElement[] x = [.. h.EnumerateArray()];
        return new AssetSwapHelper(
            VectorCase.Real(x[0]),
            new Period(x[1].GetInt32(), (TimeUnit)x[2].GetInt32()),
            x[3].GetInt32(),
            CreditInputs.CalendarOf(x[4]),
            new Period(x[5].GetInt32(), (TimeUnit)x[6].GetInt32()),
            (BusinessDayConvention)x[7].GetInt32(),
            CreditInputs.DayCounterOf(x[8]),
            new Period(x[9].GetInt32(), (TimeUnit)x[10].GetInt32()),
            (BusinessDayConvention)x[11].GetInt32(),
            CreditInputs.DayCounterOf(x[12]),
            VectorCase.Real(x[13]),
            CreditInputs.DiscountOf(x[14])!,
            context);
    }
}
