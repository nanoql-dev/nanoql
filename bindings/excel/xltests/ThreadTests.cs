// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>Multi-threaded recalculation (XLP9, XLP55): 2,000 curves built at once on Excel's threads, all distinct, all priced.</summary>
[ExcelTestSettings(OutOfProcess = true)]
public class ThreadTests : RunnerTest
{
    /// <summary>2,000 curves and their discounts, a full recalculation on every thread Excel calculates on.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void TwoThousandCurvesOnExcelsThreads()
    {
        MultiThreadedCalculation mt = Xl.Retry(() => Util.Application.MultiThreadedCalculation);
        Assert.True(Xl.Retry(() => mt.Enabled), "Excel's multi-threaded calculation is off on this machine");
        Assert.True(Xl.Retry(() => mt.ThreadCount) > 1);
        Worksheet ws = NewSheet();
        Xl.Put(ws, ("A1", "=DATE(2015,9,15)"), ("A3", "=nqlConventionSetDayCounter(,\"Actual365Fixed\")"));
        Xl.Put(ws, "G1:G2000", "=nqlFlatForward($A$1,0.02+ROW()/1000000,$A$3)");
        Xl.Put(ws, "H1:H2000", "=nqlYieldTermStructureDiscount2(G1,5)");
        Xl.Retry(Util.Application.CalculateFull);
        object?[][] curves = Xl.Range(ws, "G1:G2000"), discounts = Xl.Range(ws, "H1:H2000");
        Assert.Equal(2000, curves.Select(r => r[0]).Distinct().Count());
        Assert.All(curves, r => Assert.StartsWith("FlatForward#", r[0] as string, StringComparison.Ordinal));
        Assert.All(discounts, r => Assert.True(r[0] is double d && d > 0.8 && d < 1, $"{r[0]}"));
        Assert.Equal(Math.Exp(-5 * (0.02 + (1 / 1000000.0))), (double)discounts[0][0]!, 12);
    }
}
