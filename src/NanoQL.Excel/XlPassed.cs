// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>An Excel error an argument passed through, as against a result Excel shows as an error (no value: #N/A).</summary>
/// <param name="Code">The error.</param>
internal sealed record XlPassed(ExcelError Code);
