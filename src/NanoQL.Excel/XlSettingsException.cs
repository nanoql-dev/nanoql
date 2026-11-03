// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel;

/// <summary>The add-in's conventions failed to load: code 2, #VALUE!, its message the load's error.</summary>
/// <param name="message">The load's error.</param>
internal sealed class XlSettingsException(string message) : ArgumentException(message);
