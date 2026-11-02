// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// Excel closes by itself after nqlxlError has read a spill (XLD18, XLP79): the read releases every object it takes
/// from Excel, so an instance quit ends without a kill, which <see cref="OwnExcel"/> checks.
/// </summary>
public class CloseTests
{
    /// <summary>A spilled looped call's failed element read by nqlxlError, then Excel quit.</summary>
    [Fact]
    public void ExcelClosesAfterASpillIsRead()
        => OwnExcel.Run(AddIns.TestXll, app =>
        {
            Worksheet ws = Xl.Sheet(Xl.Retry(() => app.Workbooks.Add()));
            Xl.Put(
                ws,
                ("B1", "=nqlConventionSetDayCounter(,\"Actual365Fixed\")"),
                ("C1", "=nqlFlatForward(DATE(2026,11,2),0.03,B1)"),
                ("A3", "=DATE(2027,1,1)"),
                ("A4", "=\"x\""),
                ("A5", "=DATE(2027,6,1)"),
                ("H3", "=nqlYieldTermStructureDiscount($C$1,A3:A5)"),
                ("I4", "=nqlxlError(H4)"),
                ("I5", "=nqlxlError(H5)"));
            Xl.Calc(app);
            Assert.Contains("row 2, column 1", Xl.Text(ws, "I4"), StringComparison.Ordinal);
            Assert.Equal(string.Empty, Xl.Text(ws, "I5"));
        });
}
