// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// Multi-threaded recalculation: 2,000 curves and their discounts, all distinct, all priced, and a hundred bonds' cash
/// flows spilled as handles, each recalculated whole with Excel's threads on. A cell shows a handle through Excel's RTD
/// function, which Excel can refuse on its calculation threads, so every handle is shown on Excel's main thread (the
/// test build's <c>nqlxlStats</c> counts those shown on another) and Excel-DNA's log holds no error.
/// </summary>
[ExcelTestSettings(OutOfProcess = true)]
public partial class ThreadTests : RunnerTest
{
    /// <summary>2,000 curves and their discounts, a full recalculation on every thread Excel calculates on.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void TwoThousandCurvesOnExcelsThreads()
    {
        Threads();
        Worksheet ws = NewSheet();
        Log(clear: true);
        long offMain = OffMain();
        Xl.Put(ws, ("A1", "=DATE(2015,9,15)"), ("A3", "=nqlConventionSetDayCounter(,\"Actual365Fixed\")"));
        Xl.Put(ws, "G1:G2000", "=nqlFlatForward($A$1,0.02+ROW()/1000000,$A$3)");
        Xl.Put(ws, "H1:H2000", "=nqlYieldTermStructureDiscount2(G1,5)");
        Xl.Retry(Util.Application.CalculateFull);
        object?[][] curves = Xl.Range(ws, "G1:G2000"), discounts = Xl.Range(ws, "H1:H2000");
        Assert.Equal(2000, curves.Select(r => r[0]).Distinct().Count());
        Assert.All(curves, r => Assert.StartsWith("FlatForward#", r[0] as string, StringComparison.Ordinal));
        Assert.All(discounts, r => Assert.True(r[0] is double d && d > 0.8 && d < 1, $"{r[0]}"));
        Assert.Equal(Math.Exp(-5 * (0.02 + (1 / 1000000.0))), (double)discounts[0][0]!, 12);
        Assert.Equal(offMain, OffMain());
        NoError();
    }

    /// <summary>
    /// A hundred bonds, each a coupon of its own, and their cash flows spilled below them: arrays of handles, which a
    /// value function shows through an entry of its own.
    /// </summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void BondsCashFlowsSpilledOnExcelsThreads()
    {
        Threads();
        Worksheet ws = NewSheet();
        Log(clear: true);
        long offMain = OffMain();
        Xl.Put(
            ws,
            ("A1", "=DATE(2026,10,15)"),
            ("A2", "=nqlEvaluationContext(A1)"),
            ("A3", "=nqlConventionSetQuantLib()"),
            ("A4", "=nqlConventionSetCalendar(A3,\"TARGET\")"),
            ("A5", "=nqlConventionSetDayCounter(A3,\"Thirty360.BondBasis\")"),
            ("A6", "=nqlSchedule2(DATE(2026,10,19),DATE(2031,10,19),\"1Y\",A4,\"Unadjusted\",\"Unadjusted\",\"Backward\",FALSE,A2)"));
        Xl.Put(ws, "B7:CW7", "=nqlFixedRateBond(2,100,$A$6,0.01+COLUMN()/10000,$A$5,\"ModifiedFollowing\",100,DATE(2026,10,19))");
        Xl.Put(ws, "B8:CW8", "=nqlBondCashflows(B7)");
        Xl.Retry(Util.Application.CalculateFull);
        object?[][] bonds = Xl.Range(ws, "B7:CW7"), flows = Xl.Range(ws, "B8:CW13");
        Assert.Equal(100, bonds[0].Distinct().Count());
        Assert.All(bonds[0], b => Assert.StartsWith("FixedRateBond#", b as string, StringComparison.Ordinal));

        // Five annual coupons and the redemption under each bond, each a handle of its own.
        Assert.All(flows.SelectMany(r => r), f => Assert.Contains("#", f as string ?? $"{f}", StringComparison.Ordinal));
        Assert.Equal(600, flows.SelectMany(r => r).Distinct().Count());
        Assert.Equal(offMain, OffMain());
        NoError();
    }

    // Excel's multi-threaded calculation on, over more than one thread, as the suite's machine sets it.
    private static void Threads()
    {
        MultiThreadedCalculation mt = Xl.Retry(() => Util.Application.MultiThreadedCalculation);
        Assert.True(Xl.Retry(() => mt.Enabled), "Excel's multi-threaded calculation is off on this machine");
        Assert.True(Xl.Retry(() => mt.ThreadCount) > 1);
    }

    // The handles shown off Excel's main thread so far, from the test build's nqlxlStats, evaluated once.
    private static long OffMain()
    {
        string stats = Xl.Retry(() => Util.Application.Evaluate("nqlxlStats()")) as string ?? "nqlxlStats gave no text";
        Match m = OffMainText().Match(stats);
        Assert.True(m.Success, stats);
        return long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    // Excel-DNA's log through the test build's nqlxlLog, evaluated once: a cell holding it would recalculate it.
    private static string Log(bool clear = false)
        => Xl.Retry(() => Util.Application.Evaluate(clear ? "nqlxlLog(TRUE)" : "nqlxlLog()")) as string ?? "nqlxlLog gave no text";

    // No error in Excel-DNA's log since the test emptied it.
    private static void NoError()
    {
        string log = Log();
        Assert.True(log.StartsWith("errors 0 ", StringComparison.Ordinal), log);
    }

    [GeneratedRegex(@" offmain (\d+) ")]
    private static partial Regex OffMainText();
}
