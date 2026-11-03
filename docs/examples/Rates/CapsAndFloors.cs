// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Rates;

/// <summary>Caps and floors: a 5-year Euribor cap and floor priced on lognormal and normal volatilities.</summary>
public static class CapsAndFloors
{
    /// <summary>The example's outputs.</summary>
    /// <returns>Each instrument's value on each volatility.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        YieldTermStructure curve = Curves.Build();
        EvaluationContext context = Curves.Context;
        var table = new Table("Instrument", ">Strike", ">Black, 20%", ">Bachelier, 0.60%");

        foreach (CapFloorType type in new[] { CapFloorType.Cap, CapFloorType.Floor })
        {
            foreach (double strike in new[] { 0.020, 0.025, 0.030 })
            {
                // <price>
                CapFloor capFloor = new MakeCapFloor(type, new Period(5, TimeUnit.Years), new Euribor6M(curve))
                    .WithStrike(strike)
                    .WithNominal(10e6)
                    .ToCapFloor(context);

                double black = BlackCapFloorEngine.Calculate(capFloor, context, curve, 0.20).NPV!.Value;
                double bachelier = BachelierCapFloorEngine.Calculate(capFloor, context, curve, 0.0060).NPV!.Value;
                // </price>
                table.Add($"5Y {type.ToString().ToLowerInvariant()}", Table.Percent(strike, 2), Table.N(black),
                    Table.N(bachelier));
            }
        }

        yield return ("caps-floors", table);
    }
}
