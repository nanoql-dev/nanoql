// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>The add-in's opening: its settings file read once, when Excel loads it.</summary>
public sealed class XlAddIn : IExcelAddIn
{
    /// <inheritdoc/>
    public void AutoOpen() => XlSettings.Load(ExcelDnaUtil.XllPath, Environment.GetEnvironmentVariable("NANOQL_CONVENTIONS"));

    /// <inheritdoc/>
    public void AutoClose()
    {
    }
}
