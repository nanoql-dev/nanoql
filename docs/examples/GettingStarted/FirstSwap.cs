// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.GettingStarted;

/// <summary>Getting started: a five-year SOFR swap on a flat curve.</summary>
public static class FirstSwap
{
    /// <summary>Prices the swap.</summary>
    /// <returns>The swap's results.</returns>
    public static OvernightIndexedSwapResults Run()
    {
        // <swap>
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.036, new Actual365Fixed());

        OvernightIndexedSwap swap = new MakeOIS(new Period(5, TimeUnit.Years), new Sofr(curve))
            .WithFixedRate(0.036)
            .WithNominal(10e6)
            .ToOvernightIndexedSwap(context);

        OvernightIndexedSwapResults result = DiscountingSwapEngine.Calculate(swap, context, curve);
        // </swap>
        return result;
    }

    /// <summary>The example's output.</summary>
    /// <returns>The swap's NPV and fair rate.</returns>
    public static Table Output()
    {
        OvernightIndexedSwapResults result = Run();
        return new Table(">NPV", ">Fair rate").Add(Table.N(result.NPV!.Value), Table.Percent(result.FairRate!.Value));
    }
}
