// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;
using NanoQL.Time;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// Looping over a range: a range where a function takes one value loops, element by element, the ranges paired by
/// Excel's rule (a single row or column repeats, a position past a shorter range #N/A), each element the scalar call
/// keyed as it is; an array's range and a function returning a range don't loop.
/// </summary>
[Collection(TestSettings.Name)]
public sealed class LoopTests
{
    private static readonly double Date = new Date(15, Month.September, 2015).SerialNumber;

    /// <summary>Initializes a new instance of the <see cref="LoopTests"/> class, on a clean runtime.</summary>
    public LoopTests() => Reset();

    /// <summary>A range over each scalar kind: dates, periods, enums, booleans, names; each element the single call's result.</summary>
    [Fact]
    public void ARangeLoopsOverEachKind()
    {
        var dates = Assert.IsType<object[,]>(Call("nqlCalendarAdvance2", "TARGET", Col(Date, Date + 1), "1M"));
        Assert.Equal(Call("nqlCalendarAdvance2", "TARGET", Date + 1, "1M"), dates[1, 0]);
        var periods = Assert.IsType<object[,]>(Call("nqlCalendarAdvance2", "TARGET", Date, Row("1M", "1Y")));
        Assert.Equal(Call("nqlCalendarAdvance2", "TARGET", Date, "1Y"), periods[0, 1]);
        var conventions = Assert.IsType<object[,]>(Call("nqlCalendarAdvance2", "TARGET", Date, "1M", Col("Following", "Preceding")));
        Assert.Equal(Call("nqlCalendarAdvance2", "TARGET", Date, "1M", "Preceding"), conventions[1, 0]);
        var flags = Assert.IsType<object[,]>(Call("nqlCalendarAdvance2", "TARGET", Date, "1M", null, Col(true, false)));
        Assert.Equal(2, flags.Length);
        var calendars = Assert.IsType<object[,]>(Call("nqlCalendarAdvance2", Col("TARGET", "UnitedStates.Settlement"), Date, "1M"));
        Assert.Equal(Call("nqlCalendarAdvance2", "UnitedStates.Settlement", Date, "1M"), calendars[1, 0]);
    }

    /// <summary>A column by a row is a grid; a single row repeats; a position past a shorter range is #N/A.</summary>
    [Fact]
    public void RangesPairByExcelsRule()
    {
        var grid = Assert.IsType<object[,]>(Call("nqlCalendarAdvance2", "TARGET", Col(Date, Date + 1, Date + 2), Row("1M", "2M")));
        Assert.Equal((3, 2), (grid.GetLength(0), grid.GetLength(1)));
        Assert.Equal(Call("nqlCalendarAdvance2", "TARGET", Date + 2, "2M"), grid[2, 1]);
        var padded = Assert.IsType<object[,]>(Call("nqlCalendarAdvance2", "TARGET", Col(Date, Date + 1, Date + 2), Col("1M", "2M")));
        Assert.Equal(ExcelError.ExcelErrorNA, padded[2, 0]);
    }

    /// <summary>A passed error stays in its place; a failed element shows its error, the others their values.</summary>
    [Fact]
    public void AFailedElementInItsPlace()
    {
        var r = Assert.IsType<object[,]>(Call("nqlCalendarAdvance2", "TARGET", Col(Date, ExcelError.ExcelErrorDiv0, Date), Col("1M", "1M", "xx")));
        Assert.IsType<double>(r[0, 0]);
        Assert.Equal(ExcelError.ExcelErrorDiv0, r[1, 0]);
        Assert.Equal(ExcelError.ExcelErrorValue, r[2, 0]);
        Assert.Contains("row 3, column 1 of the spill", Message(), StringComparison.Ordinal);
    }

    /// <summary>A looped object is the single call's handle; a looped call over handles; a 1 × 1 range keyed as its value.</summary>
    [Fact]
    public void ALoopedObjectIsTheSingleCallsHandle()
    {
        string dc = H(Call("nqlConventionSetDayCounter", null, "Actual365Fixed"));
        var curves = Assert.IsType<object[,]>(Call("nqlFlatForward", Date, Col(0.02, 0.03), dc));
        Assert.Equal(H(Call("nqlFlatForward", Date, 0.03, dc)), curves[1, 0]);
        Assert.Equal(H(Call("nqlFlatForward", Date, 0.02, dc)), H(Call("nqlFlatForward", Date, Col(0.02), dc)));
        var discounts = Assert.IsType<object[,]>(Call("nqlYieldTermStructureDiscount2", curves, 1.0));
        Assert.Equal(Call("nqlYieldTermStructureDiscount2", curves[0, 0], 1.0), discounts[0, 0]);

        // A looped bind and a looped draws.
        var engines = Assert.IsType<object[,]>(Call("nqlDiscountingSwapEngineBind", curves));
        Assert.Equal(H(Call("nqlDiscountingSwapEngineBind", curves[1, 0])), engines[1, 0]);
        string snapshot = H(Call("nqlMersenneTwisterUniformRng", 42.0));
        var batches = Assert.IsType<object[,]>(Call("nqlMersenneTwisterUniformRngDraws", snapshot, Col(1, 2)));
        Assert.Equal(H(Call("nqlMersenneTwisterUniformRngDraws", snapshot, 2)), batches[1, 0]);
    }

    /// <summary>An array argument's range doesn't loop; a function returning a range doesn't loop, a range for its one value refused.</summary>
    [Fact]
    public void ArraysAndRangeResultsDontLoop()
    {
        H(Call("nqlJointCalendar4", Col("TARGET", "UnitedStates.Settlement")));
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlCalendarHolidayList", "TARGET", Col(Date, Date + 10), Date + 400));
    }

    /// <summary>A looped cell's call is kept by its cell with the whole spill as its outcome.</summary>
    [Fact]
    public void ALoopedCellsRecipeIsTheWholeSpill()
    {
        Host.Cell = TestHost.At(5, 2);
        var r = Assert.IsType<object[,]>(Call("nqlCalendarAdvance2", "TARGET", Col(Date, Date + 1), "1M"));
        Assert.True(X.TryCellCall(TestHost.At(5, 2), out XlCellCall call));
        Assert.Equal("nqlCalendarAdvance2", call.Fn);
        Assert.Same(r, call.Outcome);
    }
}
