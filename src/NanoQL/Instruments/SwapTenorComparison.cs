// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Instruments;

/// <summary>How a swap's tenor compares with a <see cref="SwapTenorThreshold"/>.</summary>
public enum SwapTenorComparison
{
    /// <summary>The swap's tenor is at most the threshold.</summary>
    AtMost,

    /// <summary>The swap's tenor is at least the threshold.</summary>
    AtLeast,
}
