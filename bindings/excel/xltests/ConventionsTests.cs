// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// A conventions file edited under its handle: both readers keyed by the file's content, so the reader of paths gives
/// the edit on F2 and Enter, the reader of one path on Ctrl+Alt+F9 alone, as neither is volatile; a looped reader over
/// two paths gives a snapshot each; a relative path refused.
/// </summary>
[ExcelTestSettings(OutOfProcess = true)]
public class ConventionsTests : RunnerTest
{
    /// <summary>The desk's file edited: 24 December no longer a holiday, 23 December one.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void AnEditedFile()
    {
        string dir = AddIns.Scratch();
        string p1 = Path.Combine(dir, "a.xml"), p2 = Path.Combine(dir, "b.xml");
        File.Copy(AddIns.Fixture("desk-conventions.xml"), p1);
        File.Copy(AddIns.Fixture("desk-conventions.xml"), p2);
        Worksheet ws = NewSheet();
        Xl.Put(
            ws,
            ("B1", $"=\"{p1}\""),
            ("A1", "=nqlConventionsXmlRead2(B1:B2)"),
            ("A2", "=nqlConventionSetWith(nqlConventionSetQuantLib(),A1)"),
            ("A3", "=nqlCalendarIsHoliday(nqlConventionSetCalendar(A2,\"TARGET\"),DATE(2026,12,23))"),
            ("A4", "=nqlCalendarIsHoliday(nqlConventionSetCalendar(A2,\"TARGET\"),DATE(2026,12,24))"),
            ("C1", $"=nqlConventionsXmlRead(\"{p2}\")"),
            ("C2", "=nqlConventionSetWith(nqlConventionSetQuantLib(),C1)"),
            ("C3", "=nqlCalendarIsHoliday(nqlConventionSetCalendar(C2,\"TARGET\"),DATE(2026,12,23))"),
            ("E1", "=nqlConventionsXmlRead(\"desk-conventions.xml\")"),
            ("E2", "=nqlxlError(E1)"));
        Xl.Calc(Util.Application);
        string a1 = Xl.Text(ws, "A1"), c1 = Xl.Text(ws, "C1");
        Assert.StartsWith("ConventionSnapshot#", a1, StringComparison.Ordinal);
        Assert.Equal(false, Xl.Get(ws, "A3"));
        Assert.Equal(true, Xl.Get(ws, "A4"));
        Assert.Equal(false, Xl.Get(ws, "C3"));
        Assert.Equal(Xl.Value, Xl.Get(ws, "E1"));
        Assert.NotEqual(string.Empty, Xl.Text(ws, "E2"));

        foreach (string p in new[] { p1, p2 })
        {
            File.WriteAllText(p, File.ReadAllText(p).Replace("2026-12-24", "2026-12-23", StringComparison.Ordinal));
        }

        // F2 and Enter on the reader of paths: the formula entered again.
        Xl.Put(ws, "A1", Xl.Retry(() => (string)Xl.GetProp(ws.Range["A1"], "Formula2")!));
        Xl.Calc(Util.Application);
        Assert.NotEqual(a1, Xl.Text(ws, "A1"));
        Assert.Equal(true, Xl.Get(ws, "A3"));
        Assert.Equal(false, Xl.Get(ws, "A4"));
        Assert.Equal(c1, Xl.Text(ws, "C1"));
        Assert.Equal(false, Xl.Get(ws, "C3"));

        // Ctrl+Alt+F9 alone, for the reader of one path.
        Xl.Retry(Util.Application.CalculateFull);
        Assert.NotEqual(c1, Xl.Text(ws, "C1"));
        Assert.Equal(true, Xl.Get(ws, "C3"));
    }

    /// <summary>A looped reader: one formula over two paths gives two snapshots.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void ALoopedReader()
    {
        string d1 = AddIns.Scratch(), d2 = AddIns.Scratch();
        string p1 = Path.Combine(d1, "c.xml"), p2 = Path.Combine(d2, "c.xml");
        File.Copy(AddIns.Fixture("desk-conventions.xml"), p1);
        File.Copy(AddIns.Fixture("desk-conventions.xml"), p2);
        Worksheet ws = NewSheet();
        Xl.Put(ws, ("A1", $"=\"{p1}\""), ("A2", $"=\"{p2}\""), ("B1", "=nqlConventionsXmlRead(A1:A2)"));
        Xl.Calc(Util.Application);
        object?[] got = Xl.Column(ws, "B1");
        Assert.Equal(2, got.Length);
        Assert.All(got, g => Assert.StartsWith("ConventionSnapshot#", g as string, StringComparison.Ordinal));
        Assert.NotEqual(got[0], got[1]);
    }
}
