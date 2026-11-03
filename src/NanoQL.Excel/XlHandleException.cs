// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel;

/// <summary>A handle this Excel session doesn't hold: the C ABI's error code 9, #REF!, though it derives from
/// <see cref="ArgumentException"/>.</summary>
/// <param name="message">The message.</param>
/// <param name="param">The argument's name.</param>
internal sealed class XlHandleException(string message, string param) : ArgumentException(message, param);
