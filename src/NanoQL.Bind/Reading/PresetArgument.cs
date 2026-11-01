// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Reading;

/// <summary>One argument of a preset's constructor call: a constant, a lambda parameter's index, or computed.</summary>
/// <param name="Constant">The constant (an integer, a double, a string or null), when the argument is one.</param>
/// <param name="IsConstant">Whether the argument is a constant (null included).</param>
/// <param name="Parameter">The lambda parameter's index, or -1.</param>
internal sealed record PresetArgument(object? Constant, bool IsConstant, int Parameter)
{
    /// <summary>Gets an argument the lambda computes, which no lookup by name reproduces.</summary>
    public static PresetArgument Computed { get; } = new(null, false, -1);
}
