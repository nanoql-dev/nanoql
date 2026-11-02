// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;

namespace NanoQL.Excel.Replay;

/// <summary>
/// Excel outside Excel: one calling cell, so a failed call's message is kept and read back for the report; the 1900
/// date system; an entry presented as a cell shows it, held by the replay.
/// </summary>
internal sealed class ReplayHost : IXlHost
{
    /// <summary>Gets the calling cell every call reports.</summary>
    public ExcelReference Cell { get; } = new(0, 0, 0, 0, 1);

    /// <inheritdoc/>
    public ExcelReference? Caller() => Cell;

    /// <inheritdoc/>
    public object ValueOf(ExcelReference r) => ExcelEmpty.Value;

    /// <inheritdoc/>
    public ExcelReference? SpillParent(ExcelReference cell) => null;

    /// <inheritdoc/>
    public int DateShift() => 0;

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
