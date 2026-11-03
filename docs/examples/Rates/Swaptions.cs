// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Rates;

/// <summary>Swaptions: European swaptions into a 5-year Euribor swap, on lognormal and normal volatilities.</summary>
public static class Swaptions
{
    /// <summary>The example's outputs.</summary>
    /// <returns>Each swaption's value on each volatility.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        YieldTermStructure curve = Curves.Build();
        EvaluationContext context = Curves.Context;
        var table = new Table("Swaption", ">Strike", ">Black, 20%", ">Bachelier, 0.60%");

        foreach (int expiry in new[] { 1, 5 })
        {
            foreach (double strike in new[] { 0.025, 0.030 })
            {
                // <price>
                var index = new EuriborSwapIsdaFixA(new Period(5, TimeUnit.Years), curve);
                Swaption swaption = new MakeSwaption(index, new Period(expiry, TimeUnit.Years), strike)
                    .WithNominal(10e6)
                    .ToSwaption(context);

                double black = BlackSwaptionEngine.Calculate(swaption, context, curve, 0.20).NPV!.Value;
                double bachelier = BachelierSwaptionEngine.Calculate(swaption, context, curve, 0.0060).NPV!.Value;
                // </price>
                table.Add($"{expiry}Y into 5Y payer", Table.Percent(strike, 2), Table.N(black), Table.N(bachelier));
            }
        }

        yield return ("swaptions", table);
    }
}
