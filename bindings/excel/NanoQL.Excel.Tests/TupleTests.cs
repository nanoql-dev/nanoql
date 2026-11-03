// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;
using NanoQL.Instruments;
using NanoQL.Time;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// The tuple forms: a tuple from a row of its cells or a column, an array of tuples a row per element, each cell
/// through its item's conversion; out, a collection of tuples a row per element, a null one a row of #N/A, an object
/// inside keyed by its parent call, its row and its slot.
/// </summary>
[Collection(TestSettings.Name)]
public sealed class TupleTests
{
    private static readonly object Empty = ExcelEmpty.Value;

    /// <summary>Initializes a new instance of the <see cref="TupleTests"/> class, on a clean runtime.</summary>
    public TupleTests() => Reset();

    /// <summary>A pair and a triple from a row and from a column; another width refused, naming the form.</summary>
    [Fact]
    public void ATupleFromARowOrAColumn()
    {
        Assert.Equal((1.5, 2.5), X.Tuple<double, double>(Row(1.5, 2.5), "t", X.Dbl, X.Dbl));
        Assert.Equal((1.5, 2.5), X.Tuple<double, double>(Col(1.5, 2.5), "t", X.Dbl, X.Dbl));
        Assert.Equal((1.0, 2.0, true), X.Tuple<double, double, bool>(Row(1.0, 2.0, true), "t", X.Dbl, X.Dbl, X.Bool));
        var e = Assert.Throws<ArgumentException>(() => X.Tuple<double, double>(Row(1.0, 2.0, 3.0), "t", X.Dbl, X.Dbl));
        Assert.Contains("a row of 2 cells", e.Message, StringComparison.Ordinal);
    }

    /// <summary>An array of pairs by rows: the empty rows at its end trimmed, an inner one refused by its items' conversions.</summary>
    [Fact]
    public void AnArrayOfPairsByRows()
    {
        var rows = X.Tuples<int, int>(new object[,] { { 1.0, 2.0 }, { 3.0, 4.0 }, { Empty, Empty } }, "p", X.Int, X.Int);
        Assert.Equal([(1, 2), (3, 4)], rows);
        var e = Assert.Throws<ArgumentException>(() => X.Tuples<int, int>(new object[,] { { 1.0, 2.0 }, { Empty, Empty }, { 3.0, 4.0 } }, "p", X.Int, X.Int));
        Assert.Contains("p row 2, item 1", e.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => X.Tuples<int, int>(Col(1.0, 2.0), "p", X.Int, X.Int));
    }

    /// <summary>A nullable item's empty cell is none; handles and names inside, a calendar by its name.</summary>
    [Fact]
    public void ItemsTakeTheirKindsForms()
    {
        Assert.Equal((1.0, (double?)null), X.Tuple<double?, double?>(Row(1.0, Empty), "t", X.OptDbl, X.OptDbl));
        var named = X.Tuples<Calendar, double>(new object[,] { { "TARGET", 0.5 }, { "UnitedStates.Settlement", 0.5 } }, "c", X.Obj<Calendar>, X.Dbl);
        Assert.Equal("TARGET", named[0].Item1.Name);
        Assert.Equal(X.Obj<Calendar>("UnitedStates.Settlement", "c").Name, named[1].Item1.Name);
    }

    /// <summary>A collection of tuples out: a row per element, a null element a row of #N/A, an empty one #N/A.</summary>
    [Fact]
    public void ACollectionOfTuplesOut()
    {
        Assert.Equal(new object[,] { { 1.0, "a" }, { 2.0, "b" } }, X.Present("f", [], new List<(int, string)> { (1, "a"), (2, "b") }));
        var o = Assert.IsType<object[,]>(X.Present("f", [], new List<(int, int)?> { (1, 2), null }));
        Assert.Equal(ExcelError.ExcelErrorNA, o[1, 0]);
        Assert.Equal(ExcelError.ExcelErrorNA, o[1, 1]);
        Assert.Equal(ExcelError.ExcelErrorNA, X.Present("f", [], new List<(int, int)>()));
    }

    /// <summary>An object inside a collection of tuples: keyed by its parent call, its row and its slot, the same handles over fresh objects.</summary>
    [Fact]
    public void AnObjectInsideATupleIsKeyedByItsSlot()
    {
        static List<(CompositeInstrument, CompositeInstrument)> Fresh() => [(new(), new()), (new(), new())];
        object[] args = ["A", 1.0];
        var a = Assert.IsType<object[,]>(X.Present("nqlProbe", args, Fresh()));
        var b = Assert.IsType<object[,]>(X.Present("nqlProbe", args, Fresh()));
        Assert.Equal(a, b);
        Assert.Equal(4, a.Cast<object>().Distinct().Count());
        var c = Assert.IsType<object[,]>(X.Present("nqlProbe", ["B", 1.0], Fresh()));
        Assert.NotEqual(a[0, 0], c[0, 0]);
    }
}
