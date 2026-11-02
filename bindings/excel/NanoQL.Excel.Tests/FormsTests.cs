// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using ExcelDna.Integration;
using NanoQL.Experimental;
using NanoQL.Numerics;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// The values both ways (XLR2): dates as serials, the 1904 shift; ranges by rows, a trailing empty cell trimmed and an
/// inner one refused; nullable elements keeping the range's shape; none as #N/A; matrices, pairs of arrays, tuples, maps,
/// jagged ranges, complex numbers, distributions and draws; what feeds back (XLD27); and the left-out objects (XLP84, C0).
/// </summary>
[Collection(TestSettings.Name)]
public sealed class FormsTests
{
    private static readonly object Empty = ExcelEmpty.Value;

    /// <summary>Initializes a new instance of the <see cref="FormsTests"/> class, on a clean runtime.</summary>
    public FormsTests() => Reset();

    /// <summary>A serial with a time of day is refused, its message naming the argument.</summary>
    [Fact]
    public void ATimeOfDayIsRefused()
    {
        Assert.Throws<ArgumentException>(() => X.Date(42262.5, "d"));
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlEvaluationContext", 42262.5));
        Assert.Contains("evaluationDate: expected a date", Message(), StringComparison.Ordinal);
    }

    /// <summary>A 1904 workbook's serials shift by 1,462 days both ways.</summary>
    [Fact]
    public void The1904DateSystemShifts()
    {
        Host.Shift = 1462;
        Assert.Equal(42262, X.Date(40800.0, "d").SerialNumber);
        Assert.Equal(40800.0, X.Present("f", [], new NanoQL.Time.Date(42262)));
        Host.Shift = 0;
        Assert.Equal(42262.0, X.Present("f", [], new NanoQL.Time.Date(42262)));
    }

    /// <summary>A range by rows, its trailing empty cells trimmed and an inner one refused.</summary>
    [Fact]
    public void ARangesTrailingEmptyCellsAreTrimmed()
    {
        Assert.Equal([1.0, 2.0, 3.0, 4.0], X.Dbls(new object[,] { { 1.0, 2.0 }, { 3.0, 4.0 }, { Empty, Empty } }, "v"));
        var e = Assert.Throws<ArgumentException>(() => X.Dbls(Col(1.0, Empty, 2.0), "v"));
        Assert.Contains("an empty cell inside the range", e.Message, StringComparison.Ordinal);
        Assert.Empty(X.Dbls(ExcelMissing.Value, "v"));
    }

    /// <summary>A range of nullable elements keeps its shape: an empty cell is none, nothing trimmed.</summary>
    [Fact]
    public void NullableElementsKeepTheRangesShape()
    {
        Assert.Equal([1.0, null, 2.0, null], X.OptDbls(Col(1.0, Empty, 2.0, Empty), "v"));
        var o = Assert.IsType<object[,]>(X.Present("f", [], new double?[] { 1.0, null }));
        Assert.Equal(1.0, o[0, 0]);
        Assert.Equal(ExcelError.ExcelErrorNA, o[1, 0]);
    }

    /// <summary>None is #N/A, a non-finite number #NUM!, a non-finite complex number #NUM!, an empty matrix #N/A (C0).</summary>
    [Fact]
    public void NoValueAndNonFiniteNumbers()
    {
        Assert.Equal(ExcelError.ExcelErrorNA, X.Present("f", [], null));
        Assert.Equal(ExcelError.ExcelErrorNum, X.Present("f", [], double.NaN));
        Assert.Equal(ExcelError.ExcelErrorNum, X.Present("f", [], double.PositiveInfinity));
        Assert.Equal(ExcelError.ExcelErrorNum, X.Present("f", [], new Complex(1, double.NaN)));
        Assert.Equal(ExcelError.ExcelErrorNA, X.Present("f", [], new Matrix(0, 3)));
        Assert.Equal(ExcelError.ExcelErrorNA, X.Present("f", [], Array.Empty<double>()));
    }

    /// <summary>A matrix by rows both ways.</summary>
    [Fact]
    public void AMatrixByRows()
    {
        Matrix m = X.Mat(new object[,] { { 1.0, 2.0, 3.0 }, { 4.0, 5.0, 6.0 } }, "m");
        Assert.Equal((2, 3), (m.Rows, m.Columns));
        Assert.Equal(6.0, m[1, 2]);
        var o = Assert.IsType<object[,]>(X.Present("f", [], m));
        Assert.Equal(new object[,] { { 1.0, 2.0, 3.0 }, { 4.0, 5.0, 6.0 } }, o);
    }

    /// <summary>A tuple across a row; a pair of arrays as two columns, the shorter padded with #N/A.</summary>
    [Fact]
    public void ATupleAcrossARow()
    {
        Assert.Equal(new object[,] { { 1.5, "a" } }, X.Present("f", [], (1.5, "a")));
        var o = Assert.IsType<object[,]>(X.Present("f", [], (new[] { 1.0, 2.0, 3.0 }, new[] { 4.0, 5.0 })));
        Assert.Equal((3, 2), (o.GetLength(0), o.GetLength(1)));
        Assert.Equal(3.0, o[2, 0]);
        Assert.Equal(ExcelError.ExcelErrorNA, o[2, 1]);
    }

    /// <summary>A map as two columns both ways: the empty rows at its end trimmed, a duplicate key refused.</summary>
    [Fact]
    public void AMapAsTwoColumns()
    {
        var map = X.Map<string, double>(new object[,] { { "a", 1.0 }, { "b", 2.0 }, { Empty, Empty } }, "m", X.Str, X.Dbl);
        Assert.Equal(new Dictionary<string, double> { ["a"] = 1.0, ["b"] = 2.0 }, map);
        var e = Assert.Throws<ArgumentException>(() => X.Map<string, double>(new object[,] { { "a", 1.0 }, { "a", 2.0 } }, "m", X.Str, X.Dbl));
        Assert.Contains("appears twice", e.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => X.Map<string, double>(Col("a", "b"), "m", X.Str, X.Dbl));
        Assert.Equal(new object[,] { { "a", 1.0 }, { "b", 2.0 } }, X.Present("f", [], map));
    }

    /// <summary>A jagged range: each row's trailing empty cells trimmed, an inner one refused; out, padded with #N/A.</summary>
    [Fact]
    public void AJaggedRange()
    {
        double[][] rows = X.Jagged(new object[,] { { 1.0, 2.0, 3.0 }, { 4.0, Empty, Empty }, { Empty, Empty, Empty } }, "j");
        Assert.Equal(2, rows.Length);
        Assert.Equal([1.0, 2.0, 3.0], rows[0]);
        Assert.Equal([4.0], rows[1]);
        Assert.Throws<ArgumentException>(() => X.Jagged(new object[,] { { 1.0, Empty, 3.0 } }, "j"));
        var o = Assert.IsType<object[,]>(X.Present("f", [], new[] { new[] { 1.0, 2.0 }, new[] { 3.0 } }));
        Assert.Equal(ExcelError.ExcelErrorNA, o[1, 1]);
    }

    /// <summary>A complex number as Excel's own text both ways, round-trip digits, a zero imaginary part as Excel writes it.</summary>
    [Fact]
    public void AComplexNumberAsExcelsText()
    {
        Assert.Equal(new Complex(3, 4), X.Cplx("3+4i", "z"));
        Assert.Equal(new Complex(3, -1), X.Cplx("3-i", "z"));
        Assert.Equal(new Complex(0, 1), X.Cplx("i", "z"));
        Assert.Equal(new Complex(2.5, 0), X.Cplx(2.5, "z"));
        Assert.Equal(new Complex(1e-3, -2e5), X.Cplx("1E-3-2E+5j", "z"));
        Assert.Throws<ArgumentException>(() => X.Cplx("3+4k", "z"));
        Assert.Equal("3", X.CplxText(new Complex(3, 0)));
        Assert.Equal("0.1+0.2i", X.Present("f", [], new Complex(0.1, 0.2)));
        Assert.Equal(new Complex(0.1, Math.BitIncrement(0.2)), X.Cplx(X.CplxText(new Complex(0.1, Math.BitIncrement(0.2))), "z"));
    }

    /// <summary>A loss distribution: six columns under a header row.</summary>
    [Fact]
    public void ADistributionUnderAHeaderRow()
    {
        var d = new Distribution(4, 0.0, 1.0);
        d.Add(0.3);
        var o = Assert.IsType<object[,]>(X.Present("f", [], d));
        Assert.Equal((5, 6), (o.GetLength(0), o.GetLength(1)));
        Assert.Equal(["x", "dx", "density", "cumulative", "excess", "average"], Enumerable.Range(0, 6).Select(j => o[0, j]));
        Assert.Equal(d.X(1), o[2, 0]);
    }

    /// <summary>Draws and their next snapshot behind handles: values, weights, and the next batch as C#'s next draws.</summary>
    [Fact]
    public void DrawsAndTheNextSnapshot()
    {
        object made = Call("nqlMersenneTwisterUniformRng", 42.0);
        Assert.True(made is string, Message());
        string snapshot = H(made);
        object drawn = Call("nqlMersenneTwisterUniformRngDraws", snapshot, 3);
        Assert.True(drawn is string, Message());
        string draws = H(drawn);
        var rng = new MersenneTwisterUniformRng(42);
        var values = Assert.IsType<object[,]>(OwnFunctions.DrawsValues(draws));
        Assert.Equal(Enumerable.Range(0, 3).Select(_ => rng.Next().Value), values.Cast<object>().Cast<double>());
        Assert.Equal(new object[,] { { 1.0 }, { 1.0 }, { 1.0 } }, OwnFunctions.DrawsWeights(draws));
        string next = H(OwnFunctions.DrawsNext(draws));
        var more = Assert.IsType<object[,]>(OwnFunctions.DrawsValues(H(Call("nqlMersenneTwisterUniformRngDraws", next, 2))));
        Assert.Equal(Enumerable.Range(0, 2).Select(_ => rng.Next().Value), more.Cast<object>().Cast<double>());

        // Sequences: a draw per row.
        var seq = Assert.IsType<object[,]>(OwnFunctions.DrawsValues(H(Call("nqlSobolRsgDraws", H(Call("nqlSobolRsg", 2, 42.0)), 4))));
        Assert.Equal((4, 2), (seq.GetLength(0), seq.GetLength(1)));
    }

    /// <summary>An Excel error argument passes through whole; one inside a range is refused, so a returned none can't feed back (XLD27).</summary>
    [Fact]
    public void WhatFeedsBack()
    {
        Assert.Equal(ExcelError.ExcelErrorDiv0, Call("nqlEvaluationContext", ExcelError.ExcelErrorDiv0));
        var e = Assert.Throws<ArgumentException>(() => X.Dbls(Col(1.0, ExcelError.ExcelErrorNA), "v"));
        Assert.Contains("#N/A", e.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => X.OptDbls(Col(1.0, ExcelError.ExcelErrorNA), "v"));
        Assert.Throws<ArgumentException>(() => X.Jagged(new object[,] { { 1.0, ExcelError.ExcelErrorNA } }, "j"));
    }

    /// <summary>An optional handle left out is none (XLP84), and so is a required one C# annotates nullable (C0's finding).</summary>
    [Fact]
    public void ObjectsLeftOutAreNone()
    {
        string calendar = H(Call("nqlConventionSetCalendar", null, "TARGET"));
        string dc = H(Call("nqlConventionSetDayCounter", null, "Actual360"));

        // IborIndex's currency is Currency?, its forwarding curve optional: both left out.
        string index = H(Call("nqlIborIndex", "Euribor", "6M", 2, null, calendar, "ModifiedFollowing", false, dc));
        Assert.Equal("Euribor6M Actual/360", Call("nqlInterestRateIndexName", index));
        Assert.Equal(ExcelError.ExcelErrorValue, Call("nqlIborIndex", "Euribor", "6M", 2, null, null, "ModifiedFollowing", false, dc));
        Assert.Contains("fixingCalendar: expected a handle", Message(), StringComparison.Ordinal);
    }
}
