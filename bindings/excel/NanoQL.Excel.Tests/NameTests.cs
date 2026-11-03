// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;
using NanoQL.Time;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// A calendar's or a day counter's name where its handle goes: text names it in the current convention set, an alias
/// too; a misspelt name #VALUE!; a handle's text never a name; a name where another type goes #REF!; each built once
/// per provider.
/// </summary>
[Collection(TestSettings.Name)]
public sealed class NameTests
{
    private static readonly double Date = new Date(15, Month.September, 2015).SerialNumber;

    /// <summary>Initializes a new instance of the <see cref="NameTests"/> class, on a clean runtime.</summary>
    public NameTests() => Reset();

    /// <summary>A calendar and a day counter by name give the handle's results; an alias names the same.</summary>
    [Fact]
    public void ANameGivesTheHandlesResult()
    {
        string target = H(Call("nqlConventionSetCalendar", null, "TARGET"));
        object byHandle = Call("nqlCalendarAdvance2", target, Date, "1M");
        Assert.Equal(byHandle, Call("nqlCalendarAdvance2", "TARGET", Date, "1M"));
        Assert.Equal(byHandle, Call("nqlCalendarAdvance2", "EUTA", Date, "1M"));
        string dc = H(Call("nqlConventionSetDayCounter", null, "Actual360"));
        Assert.Equal(Call("nqlDayCounterYearFraction", dc, Date, Date + 90), Call("nqlDayCounterYearFraction", "ACT/360", Date, Date + 90));
    }

    /// <summary>A misspelt name is #VALUE!, its message naming it; a name is as the set spells it.</summary>
    [Fact]
    public void AMisspeltNameIsRefused()
    {
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlCalendarAdvance2", "TARGT", Date, "1M"));
        Assert.Contains("no calendar named \"TARGT\"", Message(), StringComparison.Ordinal);
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlCalendarAdvance2", "target", Date, "1M"));
    }

    /// <summary>A handle's text stays a handle, 16 digits or 32, #REF! when the session doesn't hold it; a name where another type goes #REF!.</summary>
    [Fact]
    public void AHandlesTextIsNeverAName()
    {
        Assert.Equal(ExcelError.ExcelErrorRef, Call("nqlCalendarAdvance2", "Calendar#0123456789ABCDEF", Date, "1M"));
        Assert.Equal(ExcelError.ExcelErrorRef, Call("nqlCalendarAdvance2", "Calendar#0123456789ABCDEF0123456789ABCDEF", Date, "1M"));
        Assert.Equal(ExcelError.ExcelErrorRef, Call("nqlYieldTermStructureDiscount2", "TARGET", 1.0));
    }

    /// <summary>An array of calendars takes names too; each name built once per provider.</summary>
    [Fact]
    public void NamesInAnArrayBuiltOnce()
    {
        string before = X.StatsText();
        H(Call("nqlJointCalendar4", Col("TARGET", "UnitedStates.Settlement")));
        H(Call("nqlJointCalendar4", Row("TARGET", "UnitedStates.Settlement")));
        string after = X.StatsText();
        int Named(string s) => int.Parse(s[(s.LastIndexOf("named ", StringComparison.Ordinal) + 6)..], System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(Named(after) - Named(before), 0, 2);
    }
}
