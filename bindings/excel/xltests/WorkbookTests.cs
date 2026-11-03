// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// Workbooks across instances and date systems: a workbook saved and reopened in a new instance shows the saved handles
/// and, edited, recalculates every dependent with no #REF!; a 1900 and a 1904 workbook making the same raw call each
/// get an object of their own, its date back in their own system.
/// </summary>
public class WorkbookTests
{
    /// <summary>The swap saved, reopened in a new instance, its handles equal to the saved ones, then edited.</summary>
    [Fact]
    public void SavedAndReopened()
    {
        string path = Path.Combine(AddIns.Scratch(), "swap.xlsx");
        var saved = new List<object?>();
        OwnExcel.Run(AddIns.TestXll, app =>
        {
            Workbook wb = Xl.Retry(() => app.Workbooks.Add());
            Worksheet ws = Xl.Sheet(wb);
            Xl.Put(ws, Desk.Swap);
            Xl.Calc(app);
            saved.AddRange(Enumerable.Range(1, 11).Select(r => Xl.Get(ws, $"A{r}")));
            Assert.Equal(Desk.SwapNpv, Xl.Bits(saved[10]));
            Xl.Retry(() => wb.SaveAs(path, XlFileFormat.xlOpenXMLWorkbook));
        });

        OwnExcel.Run(AddIns.TestXll, app =>
        {
            Worksheet ws = Xl.Sheet(Xl.Retry(() => app.Workbooks.Open(path)));
            Xl.Calc(app);
            Assert.Equal(saved, Enumerable.Range(1, 11).Select(r => Xl.Get(ws, $"A{r}")));
            Xl.Put(ws, "A1", "=DATE(2015,9,16)");
            Xl.Calc(app);
            object?[] now = [.. Enumerable.Range(1, 11).Select(r => Xl.Get(ws, $"A{r}"))];
            Assert.DoesNotContain(now, v => v is int);
            Assert.NotEqual(saved[1], now[1]);
            Assert.NotEqual(saved[10], now[10]);
            Assert.IsType<double>(now[10]);
        });
    }

    /// <summary>A 1900 and a 1904 workbook in one instance, the same raw call in each: two objects, each its own date back.</summary>
    [Fact]
    public void The1900And1904DateSystems()
        => OwnExcel.Run(AddIns.TestXll, app =>
        {
            Worksheet w1 = Xl.Sheet(Xl.Retry(() => app.Workbooks.Add()));
            Xl.Put(w1, ("A1", "=nqlEvaluationContext(42262)"), ("A2", "=nqlEvaluationContextEvaluationDate(A1)"));
            Xl.Calc(app);
            Workbook b2 = Xl.Retry(() => app.Workbooks.Add());
            Xl.Retry(() => b2.Date1904 = true);
            Worksheet w2 = Xl.Sheet(b2);
            Xl.Put(w2, ("A1", "=nqlEvaluationContext(42262)"), ("A2", "=nqlEvaluationContextEvaluationDate(A1)"));
            Xl.Calc(app);
            Assert.NotEqual(Xl.Get(w1, "A1"), Xl.Get(w2, "A1"));
            Assert.Equal(42262.0, Xl.Get(w1, "A2"));
            Assert.Equal(42262.0, Xl.Get(w2, "A2"));
        });
}
