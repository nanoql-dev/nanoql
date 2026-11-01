// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace NanoQL.Bind.Model;

/// <summary>
/// One set of inputs an engine's calculations take after the instrument and the context: the inputs' names and types,
/// and the calculations that take exactly them, one per instrument type. A <c>bind</c> function binds them.
/// </summary>
/// <param name="Engine">The engine, a class with static <c>Calculate</c> methods.</param>
/// <param name="Index">The set's index within its engine, in the order its first calculation appears.</param>
/// <param name="Inputs">The inputs, as the set's first calculation declares them.</param>
/// <param name="Calculations">The engine's calculations taking these inputs.</param>
internal sealed record EngineInputSet(Type Engine, int Index, IReadOnlyList<ParameterInfo> Inputs, IReadOnlyList<MethodInfo> Calculations)
{
    /// <summary>Gets the set's id in the native library and in <c>surface.json</c>: the engine's full name and the index.</summary>
    public string Id => Engine.FullName + "#" + Index;
}
