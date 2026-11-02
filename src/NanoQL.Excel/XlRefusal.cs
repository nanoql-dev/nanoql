// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel;

/// <summary>
/// A value a repro's renderer meets where the add-in's conversions refuse it: the rendered call fails as the cell's did,
/// before nanoql's code (XLR14).
/// </summary>
/// <param name="message">The add-in's message.</param>
internal sealed class XlRefusal(string message) : ArgumentException(message);
