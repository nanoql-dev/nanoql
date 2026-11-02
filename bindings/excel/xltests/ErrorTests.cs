// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// Errors and their messages (XLR4, XLP6): an unknown handle #REF!, text for a number #VALUE!, QuantLib's own check
/// #NUM!, an unknown day counter and an object of the wrong type #VALUE!, an Excel error passed through; nqlxlError
/// giving each failed cell's message and nothing for a value.
/// </summary>
[ExcelTestSettings(OutOfProcess = true)]
public class ErrorTests : RunnerTest
{
    /// <summary>Each error's code and message.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void ErrorsAndTheirMessages()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(
            ws,
            ("A14", "=nqlConventionSetCalendar(A3,\"TARGET\")"),
            ("B1", "=nqlYieldTermStructureDiscount2(\"FlatForward#0123456789ABCDEF\",1)"),
            ("B2", "=nqlYieldTermStructureDiscount2(A5,\"x\")"),
            ("B3", "=nqlYieldTermStructureDiscount2(A5,-1)"),
            ("B4", "=nqlConventionSetDayCounter(A3,\"Actual999\")"),
            ("B5", "=nqlFlatForward(A1,0.03,A14)"),
            ("B6", "=nqlFlatForward(A1,NA(),A4)"),
            ("C2", "=nqlxlError(B2)"),
            ("C3", "=nqlxlError(B3)"),
            ("C4", "=nqlxlError(B4)"),
            ("C5", "=nqlxlError(B5)"),
            ("C11", "=nqlxlError(A11)"));
        Xl.Calc(Util.Application);
        Assert.Equal(Xl.Ref, Xl.Get(ws, "B1"));
        Assert.Equal(Xl.Value, Xl.Get(ws, "B2"));
        Assert.Equal(Xl.Num, Xl.Get(ws, "B3"));
        Assert.Equal(Xl.Value, Xl.Get(ws, "B4"));
        Assert.Equal(Xl.Value, Xl.Get(ws, "B5"));
        Assert.Equal(Xl.NA, Xl.Get(ws, "B6"));
        Assert.Contains("got text \"x\"", Xl.Text(ws, "C2"), StringComparison.Ordinal);
        Assert.Contains("negative time", Xl.Text(ws, "C3"), StringComparison.Ordinal);
        Assert.Contains("Actual999", Xl.Text(ws, "C4"), StringComparison.Ordinal);
        Assert.Contains("dayCounter: expected a DayCounter, got a ", Xl.Text(ws, "C5"), StringComparison.Ordinal);
        Assert.Equal(string.Empty, Xl.Text(ws, "C11"));
    }
}
