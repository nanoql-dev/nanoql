// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// Each failure's message by its cell (rounds 7 to 9), through the runtime's host seam, the test host standing in for
/// Excel's caller, values and spills: a cell Excel doesn't show as spilled gives its own call's message; a spilled cell
/// its element's while the spill shows the looped call's result, else the formula's.
/// </summary>
[Collection(TestSettings.Name)]
public sealed class MessageTests
{
    /// <summary>Initializes a new instance of the <see cref="MessageTests"/> class, on a clean runtime.</summary>
    public MessageTests() => Reset();

    /// <summary>A spilled cell gives its element's message; the anchor the first failed element's, with its place; a value cell none.</summary>
    [Fact]
    public void ASpilledCellGivesItsElementsMessage()
    {
        Host.Cell = TestHost.At(0, 0);
        var r = Assert.IsType<object[,]>(Calendars("TARGET", "Nope1", "Nope2"));
        Host.Spill(0, 0, r);
        Assert.Contains("Nope1", X.ErrorOf(TestHost.At(1, 0)), StringComparison.Ordinal);
        Assert.Contains("Nope2", X.ErrorOf(TestHost.At(2, 0)), StringComparison.Ordinal);
        Assert.Equal(string.Empty, OwnFunctions.Error(TestHost.At(0, 0)));  // the anchor shows the first element's value
        Assert.Contains("row 2, column 1 of the spill", Message(), StringComparison.Ordinal);
    }

    /// <summary>A reordered spill's failed cell gives the formula's message, which names the first failed element and its place.</summary>
    [Fact]
    public void AReorderedSpillGivesTheFormulasMessage()
    {
        Host.Cell = TestHost.At(0, 0);
        var r = Assert.IsType<object[,]>(Calendars("TARGET", "Nope1"));
        Host.Spill(0, 0, new object[,] { { r[1, 0] }, { r[0, 0] } });
        Assert.Contains("row 2, column 1 of the spill", X.ErrorOf(TestHost.At(1, 0)), StringComparison.Ordinal);
    }

    /// <summary>A nested looped call's message is the formula's cell's; a neighbour's own message kept.</summary>
    [Fact]
    public void ANestedLoopedCallKeepsItsNeighbours()
    {
        // The neighbour sits where the failed element would spill, had the formula spilled.
        Host.Cell = TestHost.At(1, 0);
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlConventionSetCalendar", null, "Nope9"));
        Host.Show(1, 0, ExcelError.ExcelErrorValue);
        Host.Cell = TestHost.At(0, 0);
        Calendars("TARGET", "Nope1");
        Host.Show(0, 0, ExcelError.ExcelErrorValue);  // the formula's own value, no spill: the looped call nested in it
        Assert.Contains("Nope9", X.ErrorOf(TestHost.At(1, 0)), StringComparison.Ordinal);
        Assert.Contains("Nope1", X.ErrorOf(TestHost.At(0, 0)), StringComparison.Ordinal);
        Assert.Contains("row 2, column 1", X.ErrorOf(TestHost.At(0, 0)), StringComparison.Ordinal);
    }

    /// <summary>
    /// A nested looped call whose first element fails, so the formula's #VALUE! equals the first element: a neighbour
    /// where the next element would spill keeps its own message, Excel showing no spill there.
    /// </summary>
    [Fact]
    public void TheFirstElementFailing()
    {
        Host.Cell = TestHost.At(1, 0);
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlConventionSetCalendar", null, "Nope9"));
        Host.Show(1, 0, ExcelError.ExcelErrorValue);
        Host.Cell = TestHost.At(0, 0);
        Calendars("Nope1", "TARGET");
        Host.Show(0, 0, ExcelError.ExcelErrorValue);
        Assert.Contains("Nope9", X.ErrorOf(TestHost.At(1, 0)), StringComparison.Ordinal);
        Assert.Contains("row 1, column 1", X.ErrorOf(TestHost.At(0, 0)), StringComparison.Ordinal);
    }

    /// <summary>A looped call becoming single gives the single call's message; a failed single call's message kept
    /// beside a looped call that succeeds.</summary>
    [Fact]
    public void ASingleCallsMessage()
    {
        Host.Cell = TestHost.At(0, 0);
        Calendars("Nope1", "TARGET");
        Call("nqlConventionSetCalendar", null, "Nope7");
        Assert.Contains("Nope7", X.ErrorOf(TestHost.At(0, 0)), StringComparison.Ordinal);
        Calendars("TARGET", "UnitedStates.Settlement");
        Assert.Contains("Nope7", X.ErrorOf(TestHost.At(0, 0)), StringComparison.Ordinal);
    }

    private static object Calendars(params object[] names) => Call("nqlConventionSetCalendar", null, Col(names));
}
