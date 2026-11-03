// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Conventions;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.SofrMarket;

/// <summary>Pricing a USD SOFR swap to match the market: QuantLib's defaults beside the market's conventions.</summary>
public static class SofrExample
{
    /// <summary>Gets the conventions file the example reads, beside the running assembly.</summary>
    public static string ConventionsFile => Path.Combine(AppContext.BaseDirectory, "SofrMarket", "market.xml");

    /// <summary>Prices each trade at each step, on $10 million.</summary>
    /// <param name="conventionsFile">The conventions file: the market's convention and the steps toward it.</param>
    /// <returns>Each trade's value and fair rate at each step.</returns>
    public static IReadOnlyList<(string Trade, string Step, double Npv, double FairRate)> Run(string conventionsFile)
    {
        // <curve>
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        (Period Tenor, double Rate)[] quotes =
        [
            (new(1, TimeUnit.Months), 0.0430), (new(3, TimeUnit.Months), 0.0428), (new(6, TimeUnit.Months), 0.0421),
            (new(1, TimeUnit.Years), 0.0405), (new(2, TimeUnit.Years), 0.0380), (new(3, TimeUnit.Years), 0.0368),
            (new(5, TimeUnit.Years), 0.0360), (new(7, TimeUnit.Years), 0.0362), (new(10, TimeUnit.Years), 0.0370),
        ];
        ConventionSet set = ConventionSet.QuantLib.With(ConventionsXml.Read(conventionsFile));
        OisConvention market = set.Ois("USD-SOFR");
        OISRateHelper[] helpers = [.. quotes.Select(q => market.RateHelper(q.Rate, q.Tenor, context))];
        var curve = new PiecewiseLogLinearDiscount(today, helpers, new Actual365Fixed(), context);
        // </curve>

        var tenor = new Period(5, TimeUnit.Years);
        var rows = new List<(string, string, double, double)>();
        foreach (var (trade, rate, start) in new (string, double, Date?)[]
                 {
                     ("par, spot", 0.036, null), ("off-market 4.25%, spot", 0.0425, null),
                     ("par, from Good Friday 2027", 0.036, new Date(26, Month.March, 2027)),
                 })
        {
            // <steps>
            (string Step, MakeOIS Builder)[] steps =
            [
                ("QuantLib's MakeOIS defaults", new MakeOIS(tenor, set.OvernightIndex("Sofr", curve))),
                ("+ the 2-day payment lag", set.Ois("Lag").Builder(tenor, curve)),
                ("+ New York business days", set.Ois("LagAndCalendars").Builder(tenor, curve)),
                ("the market's convention", (start is null ? market : set.Ois("USD-SOFR-Dated")).Builder(tenor, curve)),
            ];
            foreach (var (step, builder) in steps)
            {
                OvernightIndexedSwap swap = builder.WithEffectiveDate(start).WithFixedRate(rate).WithNominal(10e6)
                    .ToOvernightIndexedSwap(context);
                OvernightIndexedSwapResults result = DiscountingSwapEngine.Calculate(swap, context, curve);
                rows.Add((trade, step, result.NPV!.Value, result.FairRate!.Value));
            }

            // </steps>
        }

        return rows;
    }

    /// <summary>The example's outputs.</summary>
    /// <returns>Each trade's value, fair rate and each step's difference.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        var table = new Table("Trade", "Step", ">NPV", ">Fair rate", ">Step's difference");
        string? trade = null;
        double previous = 0;
        foreach (var (t, step, npv, fair) in Run(ConventionsFile))
        {
            table.Add(t, step, Table.N(npv), Table.Percent(fair), t == trade ? Table.N(npv - previous) : string.Empty);
            (trade, previous) = (t, npv);
        }

        yield return ("sofr", table);
    }
}
