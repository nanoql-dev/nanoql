// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel;

/// <summary>A cell's last call: its function, its Excel arguments, its outcome and its workbook's date shift.</summary>
/// <param name="Fn">The worksheet function.</param>
/// <param name="Args">Its Excel arguments.</param>
/// <param name="Outcome">Its Excel result, an <see cref="XlFailure"/>, or an <see cref="XlPassed"/>.</param>
/// <param name="Shift">The calling workbook's date shift.</param>
internal sealed record XlCellCall(string Fn, object[] Args, object Outcome, int Shift);
