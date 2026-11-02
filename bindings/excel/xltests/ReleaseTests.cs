// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// Handles and their release (XLR2, XLR15, XLP78), read through the test build's <c>nqlxlStats</c> (XLD25): two cells
/// making the same call share one handle; 300 handle cells cleared release their objects; a spilled leg's coupons go
/// with the leg's cell; a looped cell's objects go when it is cleared, shrunk, or one element's input changes.
/// </summary>
[ExcelTestSettings(OutOfProcess = true)]
public partial class ReleaseTests : RunnerTest
{
    /// <summary>Two cells, one handle.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void TwoCellsOneHandle()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(ws, ("B1", "=nqlFlatForward(A1,0.03,A4)"), ("B2", "=nqlFlatForward(A1,0.03,A4)"));
        Xl.Calc(Util.Application);
        Assert.StartsWith("FlatForward#", Xl.Text(ws, "B1"), StringComparison.Ordinal);
        Assert.Equal(Xl.Text(ws, "B1"), Xl.Text(ws, "B2"));
        Assert.Equal(Xl.Text(ws, "A5"), Xl.Text(ws, "B1"));
    }

    /// <summary>300 handle cells cleared: their 300 objects released.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void ClearedCellsReleaseTheirObjects()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, ("Z1", "=nqlxlStats()"), ("A1", "=DATE(2015,9,15)"), ("A4", "=nqlConventionSetDayCounter(,\"Actual365Fixed\")"));
        int h0 = Settle(ws, null);
        Xl.Put(ws, "B1:B300", "=nqlFlatForward($A$1,0.02+ROW()/100000,$A$4)");
        Assert.Equal(h0 + 300, Settle(ws, h0 + 300));
        Assert.Equal(300, Xl.Range(ws, "B1:B300").Select(r => r[0]).Distinct().Count());
        Xl.Retry(() => ws.Range["B1:B300"].ClearContents());
        Assert.Equal(h0, Settle(ws, h0));
    }

    /// <summary>A spilled leg's coupons go with the leg's cell.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void ASpilledLegsCouponsAreReleased()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(ws, "Z1", "=nqlxlStats()");
        int h0 = Settle(ws, null);
        Xl.Put(ws, "J1", "=nqlSwapLeg(A9,0)");
        int h1 = Settle(ws, null);
        Assert.Equal(10, Xl.Column(ws, "J1").Length);
        Assert.True(h1 >= h0 + 10, $"{h0} handles, then {h1} with the leg spilled");
        Xl.Retry(() => ws.Range["J1"].ClearContents());
        Assert.Equal(h0, Settle(ws, h0));
    }

    /// <summary>A looped cell's objects (XLP78, F7-3): three deposits from one formula; the cell cleared, entered again, shrunk to two, one element's input changed.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void ALoopedCellsObjectsAreReleased()
    {
        Worksheet ws = NewSheet();
        const string helpers = "=nqlDepositRateHelper(B1:B3,\"3M\",2,A2,\"ModifiedFollowing\",FALSE,A3,A1)";
        Xl.Put(
            ws,
            ("Z1", "=nqlxlStats()"),
            ("A1", "=nqlEvaluationContext(DATE(2026,11,2))"),
            ("A2", "=nqlConventionSetCalendar(,\"TARGET\")"),
            ("A3", "=nqlConventionSetDayCounter(,\"Actual360\")"),
            ("B1", "0.03"),
            ("B2", "0.031"),
            ("B3", "0.032"));
        int h0 = Settle(ws, null);
        Xl.Put(ws, "D1", helpers);
        Assert.Equal(h0 + 3, Settle(ws, h0 + 3));
        Xl.Retry(() => ws.Range["D1"].ClearContents());
        Assert.Equal(h0, Settle(ws, h0));
        Xl.Put(ws, "D1", helpers);
        Assert.Equal(h0 + 3, Settle(ws, h0 + 3));
        Xl.Put(ws, "D1", helpers.Replace("B1:B3", "B1:B2", StringComparison.Ordinal));
        Assert.Equal(h0 + 2, Settle(ws, h0 + 2));
        long b0 = Stats(ws).Builds;
        Xl.Put(ws, "B2", "0.035");
        Assert.Equal(h0 + 2, Settle(ws, h0 + 2));
        Assert.Equal(b0 + 1, Stats(ws).Builds);
    }

    // Recalculates until the live handles reach a count, or settle when none is given: a cleared cell's objects go when
    // Excel disconnects its topic, after the recalculation that cleared it.
    private static int Settle(Worksheet ws, int? want)
    {
        int last = -1;
        for (int i = 0; i < 40; i++)
        {
            Xl.Calc(ws.Application);
            int h = Stats(ws).Handles;
            if (want == null ? h == last : h == want)
            {
                return h;
            }

            last = h;
            Thread.Sleep(250);
        }

        return last;
    }

    private static (int Handles, long Builds) Stats(Worksheet ws)
    {
        Match m = StatsText().Match(Xl.Text(ws, "Z1"));
        Assert.True(m.Success, Xl.Text(ws, "Z1"));
        return (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    [GeneratedRegex(@"^handles (\d+) calls \d+ builds (\d+)")]
    private static partial Regex StatsText();
}
