// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace NanoQL.Bind.Model;

/// <summary>
/// A member that takes an engine, against one engine input set: the calculation the member takes from it and where each
/// argument the member supplies goes, or, when they form no pair, why.
/// </summary>
/// <param name="Member">The member's function.</param>
/// <param name="Parameter">The member's engine parameter.</param>
/// <param name="Set">The engine input set.</param>
/// <param name="Calculation">The calculation, when the pair forms.</param>
/// <param name="Targets">Per supplied argument, the calculation's parameter it goes into (an index past the instrument and the context).</param>
/// <param name="How">Per supplied argument, how it was placed: by type or by the roles table's name.</param>
/// <param name="Reason">Why no pair forms, or why it is refused at the call.</param>
/// <param name="Refused">Whether the pair is refused at the call (an engine with no parameter for a role).</param>
internal sealed record EnginePair(
    CFunction Member, EngineParameter Parameter, EngineInputSet Set, MethodInfo? Calculation, IReadOnlyList<int> Targets,
    IReadOnlyList<string> How, string? Reason, bool Refused)
{
    /// <summary>Gets whether the pair forms an adapter.</summary>
    public bool Formed => Calculation != null && Reason == null;
}
