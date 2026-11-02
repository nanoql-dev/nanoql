// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Globalization;
using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>
/// Each call's recipe kept by its calling cell with its outcome (XLR14): its Excel result, its failure, or the Excel
/// error an argument passed; the settings' <c>"recordCalls"</c> switches it off, leaving value cells unexportable.
/// </summary>
public static partial class X
{
    private static readonly ConcurrentDictionary<string, XlCellCall> ByCell = new(StringComparer.Ordinal);

    /// <summary>Gets or sets whether each call's recipe is kept by its cell; on unless the settings file says otherwise.</summary>
    internal static bool RecordCalls { get; set; } = true;

    /// <summary>The last call a cell made, with its outcome and its workbook's date system.</summary>
    /// <param name="r">The cell.</param>
    /// <param name="call">The call.</param>
    /// <returns>Whether the cell made one.</returns>
    internal static bool TryCellCall(ExcelReference r, out XlCellCall call)
        => ByCell.TryGetValue($"{r.SheetId}!R{r.RowFirst.ToString(CultureInfo.InvariantCulture)}C{r.ColumnFirst.ToString(CultureInfo.InvariantCulture)}", out call!);

    // A looped call's elements aren't kept: the cell keeps the looped call, its outcome the whole spill.
    private static void KeepCall(string fn, object[] args, object outcome)
    {
        if (!RecordCalls || inLift || CallerKey() is not { } cell)
        {
            return;
        }

        ByCell[cell] = new XlCellCall(fn, (object[])args.Clone(), outcome, DateShift());
    }
}
