// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using ExcelDna.Logging;

namespace NanoQL.Excel;

/// <summary>Excel-DNA's log, as a test build's <c>nqlxlLog</c> reads it.</summary>
public static partial class X
{
    /// <summary>
    /// Excel-DNA's log, for a test build's <c>nqlxlLog</c>: its errors and warnings counted, and its first error's line.
    /// Excel-DNA keeps the log's lines behind <see cref="LogDisplay"/>'s internal text and lock, which this reads by
    /// reflection, under the lock its calculation threads take to add a line; a line's level stands in brackets after
    /// its category, as in "RtdServer [Error] …".
    /// </summary>
    /// <param name="clear">Whether to empty the log after reading it.</param>
    /// <returns>"errors N warnings M", then the first error's line after a line break when there is one; "unread" when
    /// Excel-DNA's text or lock isn't found.</returns>
    public static string LogText(bool clear)
    {
        MethodInfo? text = typeof(LogDisplay).GetMethod("GetAllText", BindingFlags.NonPublic | BindingFlags.Static);
        if (text == null || typeof(LogDisplay).GetField("SyncRoot", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) is not { } sync)
        {
            return "unread";
        }

        string all;
        lock (sync)
        {
            all = text.Invoke(null, null) as string ?? string.Empty;
            if (clear)
            {
                LogDisplay.Clear();
            }
        }

        string[] lines = all.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        string[] errors = [.. lines.Where(l => l.Contains(" [Error] ", StringComparison.Ordinal) || l.Contains(" [Critical] ", StringComparison.Ordinal))];
        int warnings = lines.Count(l => l.Contains(" [Warning] ", StringComparison.Ordinal));
        string counts = string.Create(CultureInfo.InvariantCulture, $"errors {errors.Length} warnings {warnings}");
        return errors.Length == 0 ? counts : counts + "\n" + errors[0];
    }
}
