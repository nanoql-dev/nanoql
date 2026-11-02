// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// A bound engine in cells (XLR10, XLP39): the swap engine bound with its curve in one cell and given to the swap's
/// builder in another, which solves its fixed rate to the C# reference's bits; an engine of the wrong shape refused.
/// </summary>
[ExcelTestSettings(OutOfProcess = true)]
public class BoundEngineTests : RunnerTest
{
    /// <summary>The fixed rate through the bound engine, and the wrong shape's #VALUE! and message.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void TheBoundEngineInCells()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(
            ws,
            ("B1", "=nqlDiscountingSwapEngineBind(A5)"),
            ("B2", "=nqlMakeVanillaSwapWithPricingEngine(A7,B1)"),
            ("B3", "=nqlMakeVanillaSwapToVanillaSwap(B2,A2)"),
            ("B4", "=nqlFixedVsFloatingSwapFixedRate(B3)"),
            ("B5", "=nqlAnalyticHestonEngineBind()"),
            ("B6", "=nqlMakeVanillaSwapToVanillaSwap(nqlMakeVanillaSwapWithPricingEngine(A7,B5),A2)"),
            ("B7", "=nqlxlError(B6)"));
        Xl.Calc(Util.Application);
        Assert.StartsWith("BoundEngine#", Xl.Text(ws, "B1"), StringComparison.Ordinal);
        Assert.Equal(Desk.BoundFixedRate, Xl.Bits(Xl.Get(ws, "B4")));
        Assert.Equal(Xl.Value, Xl.Get(ws, "B6"));
        Assert.NotEqual(string.Empty, Xl.Text(ws, "B7"));
    }
}
