// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;

namespace NanoQL.Excel.Tests;

/// <summary>
/// Excel's stand-in behind the runtime's seam (round 8's F8-2): the calling cell, the values cells show, each cell's
/// spill anchor, the workbook's date system, and RTD topics, each an entry's holder kept by its key until a test
/// clears it as Excel disposes a topic when its last cell goes.
/// </summary>
internal sealed class TestHost : IXlHost
{
    /// <summary>The sheet the tests' cells are on.</summary>
    public const int Sheet = 7;

    private readonly Dictionary<(int Row, int Column), object> values = [];
    private readonly Dictionary<(int Row, int Column), (int Row, int Column)> spills = [];

    /// <summary>Gets or sets the calling cell, or null outside a cell's formula.</summary>
    public ExcelReference? Cell { get; set; } = At(0, 0);

    /// <summary>Gets or sets the workbook's date shift: 1,462 for the 1904 system.</summary>
    public int Shift { get; set; }

    /// <summary>Gets each RTD topic's holder by its key, in the order the topics opened.</summary>
    public Dictionary<string, XlHolder> Topics { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets every topic key presented, in order, a key again each time.</summary>
    public List<string> Presented { get; } = [];

    /// <summary>A cell on the tests' sheet.</summary>
    /// <param name="row">Its row, from 0.</param>
    /// <param name="column">Its column, from 0.</param>
    /// <returns>The reference.</returns>
    public static ExcelReference At(int row, int column) => new(row, row, column, column, Sheet);

    /// <summary>Sets what a cell shows.</summary>
    /// <param name="row">Its row.</param>
    /// <param name="column">Its column.</param>
    /// <param name="value">Its value.</param>
    public void Show(int row, int column, object value) => values[(row, column)] = value;

    /// <summary>Shows a result spilled from its anchor, each cell its element and a spilled cell's anchor the first.</summary>
    /// <param name="row">The anchor's row.</param>
    /// <param name="column">The anchor's column.</param>
    /// <param name="result">The spilled result.</param>
    public void Spill(int row, int column, object[,] result)
    {
        for (int i = 0; i < result.GetLength(0); i++)
        {
            for (int j = 0; j < result.GetLength(1); j++)
            {
                values[(row + i, column + j)] = result[i, j];
                spills[(row + i, column + j)] = (row, column);
            }
        }
    }

    /// <summary>Clears what Excel shows and holds: values, spills and every topic, released as Excel releases them.</summary>
    public void Reset()
    {
        values.Clear();
        spills.Clear();
        foreach (XlHolder h in Topics.Values)
        {
            h.Dispose();
        }

        Topics.Clear();
        Presented.Clear();
        Cell = At(0, 0);
        Shift = 0;
    }

    /// <summary>Closes a topic, as Excel does when the last cell holding it goes.</summary>
    /// <param name="key">The topic's key.</param>
    public void Close(string key)
    {
        if (Topics.Remove(key, out XlHolder? h))
        {
            h.Dispose();
        }
    }

    /// <inheritdoc/>
    public ExcelReference? Caller() => Cell;

    /// <inheritdoc/>
    public object ValueOf(ExcelReference r)
    {
        int rows = r.RowLast - r.RowFirst + 1, cols = r.ColumnLast - r.ColumnFirst + 1;
        if (rows * cols == 1)
        {
            return values.TryGetValue((r.RowFirst, r.ColumnFirst), out object? v) ? v : ExcelEmpty.Value;
        }

        var o = new object[rows, cols];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                o[i, j] = values.TryGetValue((r.RowFirst + i, r.ColumnFirst + j), out object? v) ? v : ExcelEmpty.Value;
            }
        }

        return o;
    }

    /// <inheritdoc/>
    public ExcelReference? SpillParent(ExcelReference cell)
        => spills.TryGetValue((cell.RowFirst, cell.ColumnFirst), out var a) ? At(a.Row, a.Column) : null;

    /// <inheritdoc/>
    public int DateShift() => Shift;

    /// <inheritdoc/>
    public object Present(XlEntry entry)
    {
        Presented.Add(entry.Key);
        if (!Topics.ContainsKey(entry.Key))
        {
            Topics[entry.Key] = new XlHolder(entry);
        }

        return entry.Shown;
    }
}
