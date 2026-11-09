// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using ExcelDna.Integration;

namespace NanoQL.Excel.Replay;

/// <summary>
/// Excel outside Excel: one calling cell, so a failed call's message is kept and read back for the report; a workbook's
/// date system, the 1900 one unless a vector records the 1904; an entry presented as a cell shows it, held by the replay.
/// A worksheet function runs through <see cref="Invoke"/>, which notes one Excel registers thread-safe while it runs:
/// a handle it shows is counted, since a cell shows a handle through Excel's RTD function, which Excel can refuse on
/// its calculation threads.
/// </summary>
internal sealed class ReplayHost : IXlHost
{
    // Each worksheet function's Excel name when Excel registers it thread-safe, null otherwise, read once.
    private static readonly Dictionary<MethodInfo, string?> ThreadSafe = [];

    // The thread-safe function running now, or null.
    private static string? running;

    /// <summary>Gets the functions Excel registers thread-safe that showed a handle, each with its count.</summary>
    public static SortedDictionary<string, long> ShownThreadSafe { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the calling cell every call reports.</summary>
    public ExcelReference Cell { get; } = new(0, 0, 0, 0, 1);

    /// <summary>Gets or sets the date system's shift: 0 for the 1900 system, 1,462 days for the 1904.</summary>
    public int Shift { get; set; }

    /// <summary>Runs a worksheet function as a cell calls it, noting it while it runs when Excel registers it thread-safe.</summary>
    /// <param name="m">The worksheet function.</param>
    /// <param name="args">Its arguments.</param>
    /// <returns>Its result.</returns>
    public static object? Invoke(MethodInfo m, object[] args)
    {
        if (!ThreadSafe.TryGetValue(m, out string? name))
        {
            ExcelFunctionAttribute? a = m.GetCustomAttribute<ExcelFunctionAttribute>();
            ThreadSafe[m] = name = a is { IsThreadSafe: true } ? a.Name : null;
        }

        running = name;
        try
        {
            return m.Invoke(null, args);
        }
        finally
        {
            running = null;
        }
    }

    /// <summary>The report's line on the thread-safe functions that showed a handle: none, or each with its count.</summary>
    /// <returns>The line.</returns>
    public static string ShownThreadSafeLine() => ShownThreadSafe.Count == 0
        ? "functions registered thread-safe that showed a handle: none"
        : "functions registered thread-safe that showed a handle: " + string.Join(", ", ShownThreadSafe.Select(kv => $"{kv.Key} {kv.Value:N0}"));

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
        if (running != null)
        {
            ShownThreadSafe[running] = ShownThreadSafe.GetValueOrDefault(running) + 1;
        }

        if (entry.Holders == 0)
        {
            entry.Hold();
        }

        return entry.Shown;
    }
}
