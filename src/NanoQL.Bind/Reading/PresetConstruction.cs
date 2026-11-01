// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace NanoQL.Bind.Reading;

/// <summary>
/// A constructor call in a preset's lambda: per argument, a constant (<see cref="PresetArgument.Constant"/>), the
/// lambda's parameter (<see cref="PresetArgument.Parameter"/>), or neither, when the argument is computed.
/// </summary>
/// <param name="Constructor">The constructor.</param>
/// <param name="Arguments">Its arguments, in order.</param>
internal sealed record PresetConstruction(ConstructorInfo Constructor, IReadOnlyList<PresetArgument> Arguments)
{
    /// <summary>The lambda's parameters the call passes, each once, in the lambda's order.</summary>
    /// <returns>Their indexes.</returns>
    public int[] PassedParameters() => [.. Arguments.Where(a => a.Parameter >= 0).Select(a => a.Parameter).Distinct().Order()];
}
