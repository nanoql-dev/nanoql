// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel;

/// <summary>The call presenting a result, whose objects inside are keyed by it and their place (XLD12).</summary>
/// <param name="Fn">The worksheet function.</param>
/// <param name="Args">Its Excel arguments.</param>
/// <param name="Children">The elements' entries made while presenting it.</param>
internal sealed record XlParent(string Fn, object[] Args, List<XlEntry> Children);
