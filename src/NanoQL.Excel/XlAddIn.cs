// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>The add-in's opening: Excel's main thread noted and the settings file read once, when Excel loads it.</summary>
public sealed class XlAddIn : IExcelAddIn
{
    /// <inheritdoc/>
    /// <remarks>Excel-DNA calls it on Excel's main thread, the thread the add-in shows its handles on.</remarks>
    public void AutoOpen()
    {
        X.MainThread = Environment.CurrentManagedThreadId;
        XlSettings.Load(ExcelDnaUtil.XllPath, Environment.GetEnvironmentVariable("NANOQL_CONVENTIONS"));
    }

    /// <inheritdoc/>
    public void AutoClose()
    {
    }
}
