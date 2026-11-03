// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// The settings file beside the add-in, read at load, so each case runs a copy of the test build with its own file in
/// an instance of its own: a desk's conventions by name and by handle; a missing file, a malformed one, an unknown key
/// and a wrong type each a failed load, the default conventions' every lookup refused with its message, and never
/// QuantLib's in their place, while an explicit set still works.
/// </summary>
public class SettingsTests
{
    /// <summary>The desk's TARGET, 24 December 2026 a holiday, by name and by handle.</summary>
    [Fact]
    public void TheDesksConventions()
        => WithSettings("{\"conventions\": \"desk.xml\"}", ws =>
        {
            Xl.Put(
                ws,
                ("B1", "=nqlCalendarAdjust(\"TARGET\",DATE(2026,12,24))"),
                ("B2", "=nqlCalendarAdjust(nqlConventionSetCalendar(,\"TARGET\"),DATE(2026,12,24))"),
                ("B3", "=nqlCalendarAdjust(nqlConventionSetCalendar(nqlConventionSetQuantLib(),\"TARGET\"),DATE(2026,12,24))"));
            Xl.Calc(ws.Application);
            Assert.Equal(46384.0, Xl.Get(ws, "B1"));
            Assert.Equal(46384.0, Xl.Get(ws, "B2"));
            Assert.Equal(46380.0, Xl.Get(ws, "B3"));
        });

    /// <summary>A conventions file that isn't there.</summary>
    [Fact]
    public void AMissingFile() => Fails("{\"conventions\": \"no-such-file.xml\"}", "FileNotFoundException");

    /// <summary>A conventions file that isn't well-formed.</summary>
    [Fact]
    public void AMalformedFile() => Fails("{\"conventions\": \"bad.xml\"}", "bad.xml");

    /// <summary>A key in another case is an unknown key.</summary>
    [Fact]
    public void AnUnknownKey() => Fails("{\"Conventions\": \"desk.xml\"}", "a key the add-in doesn't know: \"Conventions\"");

    /// <summary>A setting of the wrong type.</summary>
    [Fact]
    public void AWrongType() => Fails("{\"conventions\": \"desk.xml\", \"recordCalls\": \"yes\"}", "\"recordCalls\" has a value of the wrong type");

    private static void Fails(string settings, string why)
        => WithSettings(settings, ws =>
        {
            Xl.Put(
                ws,
                ("A1", "=nqlConventionSetDayCounter(,\"Actual365Fixed\")"),
                ("A2", "=nqlxlError(A1)"),
                ("A3", "=nqlCalendarAdjust(\"TARGET\",DATE(2026,12,24))"),
                ("A4", "=nqlxlError(A3)"),
                ("A5", "=nqlConventionSetDayCounter(nqlConventionSetQuantLib(),\"Actual365Fixed\")"));
            Xl.Calc(ws.Application);
            Assert.Equal(Xl.Value, Xl.Get(ws, "A1"));
            Assert.Contains("the add-in's conventions didn't load", Xl.Text(ws, "A2"), StringComparison.Ordinal);
            Assert.Contains(why, Xl.Text(ws, "A2"), StringComparison.Ordinal);
            Assert.Equal(Xl.Value, Xl.Get(ws, "A3"));
            Assert.Contains("the add-in's conventions didn't load", Xl.Text(ws, "A4"), StringComparison.Ordinal);
            Assert.Matches("^[A-Za-z0-9]+#[0-9A-F]{16}$", Xl.Text(ws, "A5"));
        });

    // A copy of the test build in a folder of its own, with the settings file and the conventions files beside it.
    private static void WithSettings(string settings, Action<Worksheet> check)
    {
        string dir = AddIns.Scratch();
        string xll = Path.Combine(dir, Path.GetFileName(AddIns.TestXll));
        File.Copy(AddIns.TestXll, xll);
        File.Copy(AddIns.Fixture("desk-conventions.xml"), Path.Combine(dir, "desk.xml"));
        File.WriteAllText(Path.Combine(dir, "bad.xml"), "<Conventions><Calendar name=\"TARGET\"");
        File.WriteAllText(Path.ChangeExtension(xll, ".settings.json"), settings);
        OwnExcel.Run(xll, app => check(Xl.Sheet(Xl.Retry(() => app.Workbooks.Add()))));
    }
}
