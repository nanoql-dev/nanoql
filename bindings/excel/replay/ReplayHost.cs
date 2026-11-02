// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;

namespace NanoQL.Excel.Replay;

/// <summary>
/// Excel outside Excel: one calling cell, so a failed call's message is kept and read back for the report; a workbook's
/// date system, the 1900 one unless a vector records the 1904; an entry presented as a cell shows it, held by the replay.
/// </summary>
internal sealed class ReplayHost : IXlHost
{
    /// <summary>Gets the calling cell every call reports.</summary>
    public ExcelReference Cell { get; } = new(0, 0, 0, 0, 1);

    /// <summary>Gets or sets the date system's shift: 0 for the 1900 system, 1,462 days for the 1904.</summary>
    public int Shift { get; set; }

    /// <inheritdoc/>
    public ExcelReference? Caller() => Cell;

    /// <inheritdoc/>
    public object ValueOf(ExcelReference r) => ExcelEmpty.Value;

    /// <inheritdoc/>
    public ExcelReference? SpillParent(ExcelReference cell) => null;

    /// <inheritdoc/>
    public int DateShift() => Shift;

    /// <inheritdoc/>
    public object Present(XlEntry entry)
    {
        if (entry.Holders == 0)
        {
            entry.Hold();
        }

        return entry.Shown;
    }
}
