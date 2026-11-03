// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Rates;

/// <summary>Swaps: a spot and a forward-starting Euribor swap, and an overnight swap, on the bootstrapped curve.</summary>
public static class Swaps
{
    /// <summary>The example's outputs.</summary>
    /// <returns>Each swap's value, fair rate and fixed leg's basis-point value.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        YieldTermStructure curve = Curves.Build();
        EvaluationContext context = Curves.Context;
        var table = new Table("Swap", ">NPV", ">Fair rate", ">Fixed leg BPS");

        // <vanilla>
        var euribor = new Euribor6M(curve);
        VanillaSwap spot = new MakeVanillaSwap(new Period(5, TimeUnit.Years), euribor)
            .WithFixedRate(0.025)
            .WithNominal(10e6)
            .ToVanillaSwap(context);
        VanillaSwap forward = new MakeVanillaSwap(new Period(5, TimeUnit.Years), euribor)
            .WithForwardStart(new Period(5, TimeUnit.Years))
            .WithFixedRate(0.025)
            .WithNominal(10e6)
            .ToVanillaSwap(context);

        FixedVsFloatingSwapResults spotResult = DiscountingSwapEngine.Calculate(spot, context, curve);
        FixedVsFloatingSwapResults forwardResult = DiscountingSwapEngine.Calculate(forward, context, curve);
        // </vanilla>
        table.Add("5Y payer at 2.50%, spot", Table.N(spotResult.NPV!.Value), Table.Percent(spotResult.FairRate!.Value),
            Table.N(spotResult.LegBPS[0]!.Value));
        table.Add("5Y payer at 2.50%, 5Y forward", Table.N(forwardResult.NPV!.Value),
            Table.Percent(forwardResult.FairRate!.Value), Table.N(forwardResult.LegBPS[0]!.Value));

        // <overnight>
        OvernightIndexedSwap ois = new MakeOIS(new Period(5, TimeUnit.Years), new Estr(curve))
            .WithFixedRate(0.025)
            .WithNominal(10e6)
            .ToOvernightIndexedSwap(context);
        OvernightIndexedSwapResults oisResult = DiscountingSwapEngine.Calculate(ois, context, curve);
        // </overnight>
        table.Add("5Y €STR payer at 2.50%", Table.N(oisResult.NPV!.Value), Table.Percent(oisResult.FairRate!.Value),
            Table.N(oisResult.LegBPS[0]!.Value));

        yield return ("swaps", table);
    }
}
