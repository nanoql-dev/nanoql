// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Examples;
using NanoQL.Examples.GettingStarted;
using NanoQL.Examples.Rates;
using NanoQL.Examples.SofrMarket;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Docs;

/// <summary>
/// The docs' examples (<c>docs/examples/</c>): each page's example runs and fills its tables, and the identities the
/// pages rely on hold: the bootstrapped curve reprices its quotes, the SOFR market's convention prices its par swap at
/// par, and the stateless page's scenarios, priced in parallel, give the same bits run after run.
/// </summary>
public class ExamplesTests
{
    /// <summary>Gets each docs page with an example.</summary>
    public static TheoryData<string> Pages => [.. Examples.Examples.All.Select(e => e.Page)];

    [Theory]
    [MemberData(nameof(Pages))]
    public void EachExampleRunsAndFillsItsTables(string page)
    {
        var outputs = Examples.Examples.All.Single(e => e.Page == page).Outputs().ToList();
        Assert.NotEmpty(outputs);
        foreach (var (name, table) in outputs)
        {
            Assert.NotEmpty(table.Rows);
            Assert.All(table.Rows, row => Assert.Contains(row, cell => cell.Length > 0));
            Assert.DoesNotContain("NaN", table.ToMarkdown(), StringComparison.Ordinal);
            Assert.Matches("^[a-z-]+$", name);
        }
    }

    [Fact]
    public void TheFirstSwapIsWorthItsFairRatesSpreadOverTheFixedLeg()
    {
        OvernightIndexedSwapResults result = FirstSwap.Run();
        double bps = result.LegBPS[0]!.Value;
        Assert.Equal(result.NPV!.Value, (result.FairRate!.Value - 0.036) * -bps / 1e-4, 1e-6);
    }

    [Fact]
    public void TheBootstrappedCurveRepricesItsSwapQuotes()
    {
        YieldTermStructure curve = Curves.Build();
        foreach (var (years, rate) in new[] { (2, 0.0225), (5, 0.0245), (10, 0.0270), (30, 0.0285) })
        {
            var index = new EuriborSwapIsdaFixA(new Period(years, TimeUnit.Years), curve);
            VanillaSwap swap = new MakeVanillaSwap(index.Tenor, index.IborIndex)
                .WithFixedRate(rate)
                .WithFixedLegTenor(index.FixedLegTenor)
                .WithFixedLegDayCount(index.DayCounter)
                .WithFixedLegConvention(index.FixedLegConvention)
                .WithFixedLegTerminationDateConvention(index.FixedLegConvention)
                .ToVanillaSwap(Curves.Context);
            double fair = DiscountingSwapEngine.Calculate(swap, Curves.Context, curve).FairRate!.Value;
            Assert.True(Math.Abs(fair - rate) < 1e-10, $"{years}Y: {fair} against {rate}");
        }
    }

    [Fact]
    public void TheSofrMarketsConventionPricesItsParSwapAtPar()
    {
        var rows = SofrExample.Run(SofrExample.ConventionsFile);
        Assert.Equal(12, rows.Count);
        var par = rows.Single(r => r.Trade == "par, spot" && r.Step == "the market's convention");
        Assert.True(Math.Abs(par.Npv) < 1e-6, $"NPV {par.Npv}");
    }

    [Fact]
    public void TheStatelessScenariosGiveTheSameBitsRunAfterRun()
    {
        string first = string.Concat(Examples.Examples.All.Single(e => e.Page == "stateless").Outputs()
            .Select(o => o.Table.ToMarkdown()));
        string second = string.Concat(Examples.Examples.All.Single(e => e.Page == "stateless").Outputs()
            .Select(o => o.Table.ToMarkdown()));
        Assert.Equal(first, second);
    }
}
