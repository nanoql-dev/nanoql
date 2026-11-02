// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel;

/// <summary>A looped call's spill: its anchor, its result, and each failed element's message by its place (XLR15).</summary>
/// <param name="SheetId">The anchor's sheet.</param>
/// <param name="Row">The anchor's row.</param>
/// <param name="Column">The anchor's column.</param>
/// <param name="Result">The spilled result.</param>
/// <param name="Messages">Each failed element's message, by its row and column in the spill.</param>
internal sealed record XlSpill(IntPtr SheetId, int Row, int Column, object[,] Result, Dictionary<(int Row, int Column), string> Messages);
