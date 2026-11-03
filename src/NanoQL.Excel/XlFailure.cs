// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>A call that failed: its message and the Excel error it returned.</summary>
/// <param name="Message">The message.</param>
/// <param name="Code">The Excel error.</param>
internal sealed record XlFailure(string Message, ExcelError Code);
