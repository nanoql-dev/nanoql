// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>
/// What the runtime reads from Excel and hands to it, through one seam (round 8's F8-2): the calling cell, a range's
/// values, the anchor of the spill a cell belongs to, the calling workbook's date system, and an entry presented
/// through its RTD topic. Excel's in the add-in; a test host stands in outside Excel (the tests, the replayer).
/// </summary>
internal interface IXlHost
{
    /// <summary>The calling cell, or null outside a cell's formula.</summary>
    /// <returns>The cell.</returns>
    ExcelReference? Caller();

    /// <summary>A range's values as Excel shows them.</summary>
    /// <param name="r">The range.</param>
    /// <returns>Its value, or an array of them.</returns>
    object ValueOf(ExcelReference r);

    /// <summary>The anchor of the spill a cell belongs to, or null when it belongs to none.</summary>
    /// <param name="cell">The cell.</param>
    /// <returns>The anchor.</returns>
    ExcelReference? SpillParent(ExcelReference cell);

    /// <summary>The calling workbook's date shift: 1,462 in the 1904 date system, 0 in the 1900 one (XLD8).</summary>
    /// <returns>The shift.</returns>
    int DateShift();

    /// <summary>Presents an entry: through its RTD topic in Excel, which holds it while a cell does.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>What the cell shows.</returns>
    object Present(XlEntry entry);
}
