// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Examples;
using NanoQL.Examples.GettingStarted;
using NanoQL.Examples.Rates;
using NanoQL.Examples.SofrMarket;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
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

    /// <summary>
    /// The equity options page's table holds QuantLib 1.44's EquityOption example's output, each value at its six
    /// decimals; the Longstaff-Schwartz row, marked, holds QuantLib's example given nanoql's calibration seed, 42 +
    /// 1,768,237,423, the seed QuantLib documents (the divergences page's LongstaffSchwartzCalibrationSeedTests).
    /// </summary>
    [Fact]
    public void TheEquityOptionsHoldQuantLibsExample()
    {
        string[][] expected =
        [
            ["3.844308", "", ""], // Black-Scholes
            ["3.818577", "", ""], // Black Vasicek Model
            ["3.844306", "", ""], // Heston semi-analytic
            ["3.844306", "", ""], // Bates semi-analytic
            ["", "", "4.459628"], // Barone-Adesi/Whaley
            ["", "", "4.453064"], // Bjerksund/Stensland
            ["", "", "4.486617"], // QD+ fixed-point (fast)
            ["", "", "4.486669"], // QD+ fixed-point (accurate)
            ["", "", "4.486674"], // QD+ fixed-point (high precision)
            ["3.844309", "", ""], // Integral
            ["3.844330", "4.360765", "4.486113"], // Finite differences
            ["3.844132", "4.361174", "4.486552"], // Binomial Jarrow-Rudd
            ["3.843504", "4.360861", "4.486415"], // Binomial Cox-Ross-Rubinstein
            ["3.836911", "4.354455", "4.480097"], // Additive equiprobabilities
            ["3.843557", "4.360909", "4.486461"], // Binomial Trigeorgis
            ["3.844171", "4.361176", "4.486413"], // Binomial Tian
            ["3.844308", "4.360713", "4.486076"], // Binomial Leisen-Reimer
            ["3.844308", "4.360713", "4.486076"], // Binomial Joshi
            ["3.834522", "", ""], // MC (crude)
            ["3.844613", "", ""], // QMC (Sobol)
            ["", "", "4.454440"], // MC (Longstaff Schwartz), QuantLib's example with nanoql's calibration seed
        ];
        var rows = Examples.Equity.EquityOptions.Outputs().Single().Table.Rows;
        Assert.Equal(expected.Length, rows.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], rows[i][1..]);
        }

        Assert.Equal(Examples.Equity.EquityOptions.LongstaffSchwartz, rows[^1][0]);
    }

    /// <summary>
    /// The CDS page's tables hold QuantLib 1.44's CDS example's output (its examples 1 and 2), each value at its printed
    /// digits; the repriced quotes' NPVs, round-off in both libraries, are the identity they are, a CDS at the spread
    /// its curve was bootstrapped from worth nothing, within the bootstrap's accuracy.
    /// </summary>
    [Fact]
    public void TheCreditDefaultSwapsHoldQuantLibsExample()
    {
        var tables = Examples.Credit.CreditDefaultSwaps.Outputs().ToDictionary(o => o.Name, o => o.Table.Rows);
        Assert.Equal(
            [
                ["2007-05-15", "0.0299685"], ["2007-09-20", "0.0299685"], ["2007-12-20", "0.0299613"],
                ["2008-06-20", "0.0299619"], ["2009-06-22", "0.0299607"],
            ],
            tables["cds-hazard-rates"]);
        Assert.Equal([["1Y", "97.040077%"], ["2Y", "94.175796%"]], tables["cds-survival"]);
        string[][] legs = [["-5177.05", "5177.05"], ["-8841.72", "8841.72"], ["-16101.8", "16101.8"], ["-30154.5", "30154.5"]];
        var repriced = tables["cds-repriced"];
        for (int i = 0; i < legs.Length; i++)
        {
            Assert.Equal("1.500000%", repriced[i][1]);
            Assert.Equal(legs[i], repriced[i][3..]);
        }

        var h = Examples.Credit.CreditDefaultSwaps.BuildHazardCurve();
        Assert.All(Examples.Credit.CreditDefaultSwaps.Reprice(h), r => Assert.True(Math.Abs(r.Result.NPV!.Value) < 1e-6));

        Assert.Equal(
            [
                ["2014-11-24", "6.08332e-05", "0.999994"], ["2014-12-23", "0.00044368", "0.999923"],
                ["2015-01-23", "0.000804988", "0.999793"], ["2015-04-23", "0.00184508", "0.99907"],
                ["2015-07-23", "0.00257462", "0.998062"], ["2015-10-23", "0.00339272", "0.996594"],
                ["2016-10-24", "0.00221731", "0.995551"], ["2017-10-23", "0.00274915", "0.991764"],
                ["2018-10-23", "0.00352107", "0.985986"], ["2019-10-23", "0.00451599", "0.977637"],
                ["2020-10-23", "0.0057249", "0.966173"], ["2021-10-25", "0.00707627", "0.951562"],
                ["2022-10-24", "0.00847959", "0.934305"], ["2023-10-23", "0.009823", "0.91529"],
                ["2024-10-23", "0.0110503", "0.895248"], ["2026-10-23", "0.0131302", "0.85407"],
                ["2029-10-23", "0.0153845", "0.793724"], ["2034-10-23", "0.0176039", "0.702988"],
                ["2044-10-24", "0.0188487", "0.567776"],
            ],
            tables["cds-isda-curve"]);
        Assert.Equal(
            [["First period's accrual on 24 October 2014", "88888.9"], ["NPV", "-40991.9"]], tables["cds-isda-trade"]);
    }

    /// <summary>
    /// The volatility page's surface reads its quotes back at its nodes, and its Heston calibration holds QuantLib
    /// 1.44's on the same data and criteria (testDAXCalibration's, AnalyticHestonEngine of order 64) at the six
    /// significant digits the page prints: QuantLib's run gives θ 0.074591206948721, κ 15.662702337081, σ
    /// 3.3619180059656, ρ −0.51149166184038, v0 0.19566150367967 and an error sum of 177.23333357909.
    /// </summary>
    [Fact]
    public void TheHestonCalibrationHoldsQuantLibs()
    {
        var surface = Examples.Equity.VolatilityAndHeston.BuildSurface();
        for (int s = 0; s < Examples.Equity.VolatilityAndHeston.Strikes.Count; s++)
        {
            for (int m = 0; m < Examples.Equity.VolatilityAndHeston.Days.Count; m++)
            {
                Date expiry = Examples.Equity.VolatilityAndHeston.Context.EvaluationDate +
                    Examples.Equity.VolatilityAndHeston.Days[m];
                double vol = surface.BlackVol(expiry, Examples.Equity.VolatilityAndHeston.Strikes[s]);
                Assert.True(Math.Abs(vol - Examples.Equity.VolatilityAndHeston.Vols[s, m]) < 1e-12, $"{s}, {m}: {vol}");
            }
        }

        var rows = Examples.Equity.VolatilityAndHeston.Outputs().Single(o => o.Name == "heston-calibration").Table.Rows;
        Assert.Equal(["0.0745912", "15.6627", "3.36192", "-0.511492", "0.195662", "177.233"], rows.Select(r => r[1]));
    }

    /// <summary>
    /// The short-rate page. Its unmarked values, Hull-White's analytic calibration, the finite-difference prices on it
    /// and the Bermudan's strikes, hold QuantLib 1.44's BermudanSwaption example at its printed digits. Its marked
    /// values hold QuantLib given nanoql's calibrated parameters: G2's model values its analytic engine gives on each
    /// helper's coupons restated on the curve's day counter, the rate times 365/360 (the check of
    /// G2AnalyticSwaptionCouponTests), and the finite-difference engines' Bermudan prices on G2 and on the numerically
    /// calibrated Hull-White, each within 1e-12 relative. Where QuantLib can't take the fix (the tree's dates), a tree
    /// value holds an independent engine: the numerical Hull-White's model values hold Jamshidian's exact price on
    /// QuantLib's run within 5% on the example's coarse 30-step grid, and within 0.5% on 300 steps, so the coarse gap
    /// is the grid's; and each tree's Bermudan price holds the finite-difference engine's on the same model within 0.1
    /// on the notional of 1,000.
    /// </summary>
    [Fact]
    public void TheShortRateModelsHoldQuantLibOrAnIndependentEngine()
    {
        var r = ShortRateModels.Run();
        var g2 = r.Calibrations[0];
        var hullWhite = r.Calibrations[1];
        var numerical = r.Calibrations[2];
        var ci = System.Globalization.CultureInfo.InvariantCulture;

        Assert.Equal(["10.62037", "10.62959", "10.63414", "10.64428", "10.66131"],
            hullWhite.ModelVols.Select(v => (v * 100).ToString("F5", ci)));
        Assert.Equal(["0.046416", "0.0058693"], hullWhite.Parameters.Select(p => p.Value.ToString("G5", ci)));
        Assert.Equal(["12.91", "2.4596", "42.215"], r.Bermudans.Select(b => b.HullWhiteFd.ToString("G5", ci)));
        Assert.Equal(["5.00000", "6.00000", "4.00000"], r.Bermudans.Select(b => (b.Strike * 100).ToString("F5", ci)));

        static void Near(double expected, double actual, double relative) =>
            Assert.True(Math.Abs(actual - expected) <= relative * Math.Abs(expected), $"{actual} against {expected}");

        double[] restated =
        [
            0.0087870109853507017, 0.0096826224704803805, 0.0086581763608473434, 0.0064855867635418398, 0.00353879477453397,
        ];
        for (int i = 0; i < 5; i++)
        {
            Near(restated[i], g2.ModelValues[i], 1e-12);
        }

        double[] g2Fd = [12.83175725574171, 2.4552659194277937, 42.205647861572125];
        double[] numericalFd = [13.157137623319752, 2.5828529492880041, 42.311253274771964];
        for (int i = 0; i < 3; i++)
        {
            Near(g2Fd[i], r.Bermudans[i].G2Fd, 1e-12);
            Near(numericalFd[i], r.Bermudans[i].NumericalFd, 1e-12);
        }

        double[] jamshidian =
        [
            0.0089075835819950148, 0.0098094646589436708, 0.0087878019332272694, 0.0065853665949613099, 0.0035953072279697935,
        ];
        var fine = new TimeGrid([.. r.Times], 300);
        for (int i = 0; i < 5; i++)
        {
            Near(jamshidian[i], numerical.ModelValues[i], 0.05);
            double onFineGrid = r.Swaptions[i]
                .SetPricingEngine<HullWhite>((s, c, m) => TreeSwaptionEngine.Calculate(s, c, m, fine))
                .ModelValue(numerical.Model, ShortRateModels.Context);
            Near(jamshidian[i], onFineGrid, 0.005);
        }

        Assert.All(r.Bermudans, b =>
        {
            Assert.True(Math.Abs(b.G2Tree - b.G2Fd) < 0.1, $"G2 {b.Label}: {b.G2Tree} against {b.G2Fd}");
            Assert.True(Math.Abs(b.HullWhiteTree - b.HullWhiteFd) < 0.1, $"Hull-White {b.Label}");
            Assert.True(Math.Abs(b.NumericalTree - b.NumericalFd) < 0.1, $"numerical {b.Label}");
        });
    }

    /// <summary>
    /// The basis and FX page's identities: the basis swap at its fair spread, and the FX forward at its fair rate, worth
    /// nothing; the curves from FX swap points and from cross-currency basis swaps reprice their quotes.
    /// </summary>
    [Fact]
    public void TheBasisAndFxIdentitiesHold()
    {
        Assert.True(Math.Abs(BasisAndFx.BasisSwap().NpvAtFair) < 1e-6);
        Assert.True(Math.Abs(BasisAndFx.FxForwardAtFair().NpvAtFair) < 1e-6);
        Assert.All(BasisAndFx.FxSwapCurve(), q => Assert.True(Math.Abs(q.Implied - q.Quote) < 1e-10, q.Tenor));
        Assert.All(CrossCurrencyBasis.Run(), q => Assert.True(Math.Abs(q.Implied - q.Quote) < 1e-10, q.Tenor));
    }

    /// <summary>The stateless page's scenarios, priced in parallel and then in sequence, give the same bits.</summary>
    [Fact]
    public void TheParallelScenariosEqualTheSequentialOnes()
    {
        var rows = Examples.Examples.All.Single(e => e.Page == "stateless").Outputs()
            .Single(o => o.Name == "stateless-scenarios").Table.Rows;
        Assert.All(rows, row => Assert.Equal("the same bits", row[^1]));
    }

    /// <summary>
    /// The stateless page's valuation as of a past date depends on that day's inputs alone: run in either order, both
    /// valuations give the same bits, and the later one's context holds the two fixings the index published since the
    /// trade.
    /// </summary>
    [Fact]
    public void AValuationAsOfAPastDateDependsOnThatDaysInputsAlone()
    {
        var tradeDateFirst = Examples.Stateless.StatelessPatterns.ValueAsOfTwoDates();
        var laterFirst = Examples.Stateless.StatelessPatterns.ValueAsOfTwoDates(laterFirst: true);
        Assert.Equal(
            tradeDateFirst.Select(v => BitConverter.DoubleToInt64Bits(v.Npv)),
            laterFirst.Select(v => BitConverter.DoubleToInt64Bits(v.Npv)));
        Assert.Equal([0, 2], tradeDateFirst.Select(v => v.Fixings));
    }
}
