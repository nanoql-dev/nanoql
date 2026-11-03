// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Office.Interop.Excel;
using NanoQL.Examples.Rates;
using NanoQL.Examples.SofrMarket;
using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// The example workbooks in <c>bindings/excel/examples/</c>: each opened read-only in an Excel of its own on the
/// stable add-in, recalculated whole, and its named results compared bit for bit with C#'s. The curve, the swaps and
/// the SOFR market are the docs' own examples (<c>docs/examples</c>), so the workbooks give the docs' numbers.
/// </summary>
public class ExampleWorkbookTests
{
    private static readonly string Folder = Path.Combine(AddIns.Repository, "bindings", "excel", "examples");

    /// <summary>The curve's discounts, zero rates and 6-month forwards at six tenors: the docs' curve's.</summary>
    [Fact]
    public void TheCurveWorkbookReadsTheDocsCurve()
    {
        YieldTermStructure curve = Curves.Build();
        var discounts = new List<double>();
        var zeros = new List<double>();
        var forwards = new List<double>();
        foreach (int years in new[] { 1, 2, 5, 10, 20, 25 })
        {
            Date d = Curves.Today + new Period(years, TimeUnit.Years);
            discounts.Add(curve.Discount(d));
            zeros.Add(curve.ZeroRate(d, new Actual365Fixed(), Compounding.Continuous).Rate);
            forwards.Add(curve.ForwardRate(d, d + new Period(6, TimeUnit.Months), new Actual360(), Compounding.Simple).Rate);
        }

        Recalculated("curve.xlsx", wb =>
        {
            Assert.Equal(discounts.Select(Xl.Bits), Named(wb, "Discounts"));
            Assert.Equal(zeros.Select(Xl.Bits), Named(wb, "ZeroRates"));
            Assert.Equal(forwards.Select(Xl.Bits), Named(wb, "Forwards"));
        });
    }

    /// <summary>The spot and forward Euribor swaps and the €STR swap: the docs' swaps' NPVs, fair rates and fixed
    /// legs' BPS.</summary>
    [Fact]
    public void TheSwapWorkbookPricesTheDocsSwaps()
    {
        YieldTermStructure curve = Curves.Build();
        EvaluationContext context = Curves.Context;
        var euribor = new Euribor6M(curve);
        FixedVsFloatingSwapResults[] results =
        [
            DiscountingSwapEngine.Calculate(
                new MakeVanillaSwap(new Period(5, TimeUnit.Years), euribor).WithFixedRate(0.025).WithNominal(10e6).ToVanillaSwap(context),
                context,
                curve),
            DiscountingSwapEngine.Calculate(
                new MakeVanillaSwap(new Period(5, TimeUnit.Years), euribor).WithForwardStart(new Period(5, TimeUnit.Years))
                    .WithFixedRate(0.025).WithNominal(10e6).ToVanillaSwap(context),
                context,
                curve),
            DiscountingSwapEngine.Calculate(
                new MakeOIS(new Period(5, TimeUnit.Years), new Estr(curve)).WithFixedRate(0.025).WithNominal(10e6).ToOvernightIndexedSwap(context),
                context,
                curve),
        ];

        Recalculated("swap.xlsx", wb =>
        {
            Assert.Equal(results.Select(r => Xl.Bits(r.NPV!.Value)), Named(wb, "NPVs"));
            Assert.Equal(results.Select(r => Xl.Bits(r.FairRate!.Value)), Named(wb, "FairRates"));
            Assert.Equal(results.Select(r => Xl.Bits(r.LegBPS[0]!.Value)), Named(wb, "FixedLegBPS"));
        });
    }

    /// <summary>The five-year bond's NPV, clean and dirty prices and yield.</summary>
    [Fact]
    public void TheBondWorkbookPricesItsBond()
    {
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        ConventionSet set = ConventionSet.QuantLib;
        DayCounter accrual = set.DayCounter("Thirty360.BondBasis");
        var curve = new FlatForward(today, 0.025, set.DayCounter("Actual365Fixed"));
        var issue = new Date(19, Month.October, 2026);
        var schedule = new Schedule(
            issue,
            new Date(19, Month.October, 2031),
            new Period(1, TimeUnit.Years),
            set.Calendar("TARGET"),
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            false,
            context);
        var bond = new FixedRateBond(2, 100, schedule, [0.03], accrual, BusinessDayConvention.ModifiedFollowing, 100, issue);
        BondResults results = DiscountingBondEngine.Calculate(bond, context, curve);

        Recalculated("bond.xlsx", wb =>
        {
            Assert.Equal([Xl.Bits(results.NPV!.Value)], Named(wb, "NPV"));
            Assert.Equal([Xl.Bits(bond.CleanPrice(results, context))], Named(wb, "CleanPrice"));
            Assert.Equal([Xl.Bits(bond.DirtyPrice(results, context))], Named(wb, "DirtyPrice"));
            Assert.Equal([Xl.Bits(bond.Yield(results, accrual, Compounding.Compounded, Frequency.Annual, context))], Named(wb, "Yield"));
        });
    }

    /// <summary>The call's and the put's NPVs, deltas, gammas and vegas.</summary>
    [Fact]
    public void TheOptionWorkbookPricesItsCallAndPut()
    {
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        ConventionSet set = ConventionSet.QuantLib;
        DayCounter dc = set.DayCounter("Actual365Fixed");
        var process = new BlackScholesMertonProcess(
            100,
            new FlatForward(today, 0.01, dc),
            new FlatForward(today, 0.03, dc),
            new BlackConstantVol(today, set.Calendar("TARGET"), 0.2, dc));
        var exercise = new EuropeanExercise(new Date(15, Month.October, 2027));
        OneAssetOptionResults[] results =
        [
            .. new[] { OptionType.Call, OptionType.Put }.Select(type =>
                AnalyticEuropeanEngine.Calculate(new VanillaOption(new PlainVanillaPayoff(type, 100), exercise), context, process)),
        ];

        Recalculated("option.xlsx", wb =>
        {
            Assert.Equal(results.Select(r => Xl.Bits(r.NPV!.Value)), Named(wb, "NPVs"));
            Assert.Equal(results.Select(r => Xl.Bits(r.Delta!.Value)), Named(wb, "Deltas"));
            Assert.Equal(results.Select(r => Xl.Bits(r.Gamma!.Value)), Named(wb, "Gammas"));
            Assert.Equal(results.Select(r => Xl.Bits(r.Vega!.Value)), Named(wb, "Vegas"));
        });
    }

    /// <summary>The SOFR page's three trades at QuantLib's defaults and at the market's convention, on its curve and
    /// its conventions file.</summary>
    [Fact]
    public void TheSofrWorkbookGivesTheDocsDefaultsAndMarketSteps()
    {
        // The workbook reads the conventions file beside it, the docs' own, byte for byte.
        string conventions = Path.Combine(Folder, "market.xml");
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(AddIns.Repository, "docs", "examples", "SofrMarket", "market.xml")),
            File.ReadAllBytes(conventions));

        // The docs' four steps for each trade; the workbook prices the first and the last.
        var rows = SofrExample.Run(conventions);
        Assert.Equal(12, rows.Count);
        int[] steps = [0, 3, 4, 7, 8, 11];

        Recalculated("sofr.xlsx", wb =>
        {
            Assert.Equal(steps.Select(i => Xl.Bits(rows[i].Npv)), Named(wb, "NPVs"));
            Assert.Equal(steps.Select(i => Xl.Bits(rows[i].FairRate)), Named(wb, "FairRates"));
        });
    }

    // The workbook opened read-only in an Excel of its own on the stable add-in, recalculated whole, then checked.
    private static void Recalculated(string file, Action<Workbook> check) => OwnExcel.Run(AddIns.Stable, app =>
    {
        Workbook wb = Xl.Retry(() => app.Workbooks.Open(Path.Combine(Folder, file), 0, true));
        Xl.Retry(app.CalculateFull);
        check(wb);
    });

    // A named range's values, by rows, each as its bits: an error or text shows as itself.
    private static string[] Named(Workbook wb, string name) => Xl.Retry<string[]>(() =>
    {
        object? value = wb.Names.Item(name).RefersToRange.Value2;
        return value is object[,] grid ? [.. grid.Cast<object?>().Select(Xl.Bits)] : [Xl.Bits(value)];
    });
}
