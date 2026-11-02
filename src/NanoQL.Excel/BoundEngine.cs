// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel;

/// <summary>
/// An engine bound with its inputs (XLR10), as the native host's: its input set, the instrument a <c>BindOn</c> gave,
/// and the inputs given. The engine adapters the generator shares with the native host read it; its refusals are
/// argument errors (#VALUE!).
/// </summary>
/// <param name="set">The input set's id: the engine's full name and the set's index.</param>
/// <param name="instrument">The instrument it's bound to, or null.</param>
/// <param name="inputs">The inputs given, by name.</param>
public sealed class BoundEngine(string set, object? instrument, IReadOnlyDictionary<string, object> inputs)
{
    /// <summary>Gets the input set's id.</summary>
    public string Set { get; } = set;

    /// <summary>Gets the instrument it's bound to, or null.</summary>
    public object? Instrument { get; } = instrument;

    /// <summary>An input the calculation needs: bound, or refused.</summary>
    /// <typeparam name="T">The input's type.</typeparam>
    /// <param name="name">The input's name.</param>
    /// <returns>The input.</returns>
    public T Take<T>(string name) =>
        inputs.TryGetValue(name, out object? v) ? (T)v : throw new ArgumentException($"engine: '{name}' is neither bound nor supplied", "engine");

    /// <summary>An input the calculation can default: bound, or its default.</summary>
    /// <typeparam name="T">The input's type.</typeparam>
    /// <param name="name">The input's name.</param>
    /// <param name="fallback">Its default.</param>
    /// <returns>The input.</returns>
    public T TakeOr<T>(string name, T fallback) => inputs.TryGetValue(name, out object? v) ? (T)v : fallback;

    /// <summary>Refuses an input the member supplies that the engine was bound with too.</summary>
    /// <param name="name">The input's name.</param>
    public void Open(string name)
    {
        if (inputs.ContainsKey(name))
        {
            throw new ArgumentException($"engine: '{name}' is bound, and the member supplies it", "engine");
        }
    }

    /// <summary>Refuses an engine bound to an instrument where the member supplies one.</summary>
    public void NoInstrument()
    {
        if (Instrument != null)
        {
            throw new ArgumentException("engine: bound to an instrument (BindOn), and the member supplies one; bind it with Bind", "engine");
        }
    }

    /// <summary>The instrument a member taking the context alone needs.</summary>
    /// <returns>The instrument.</returns>
    public object NeedInstrument() =>
        Instrument ?? throw new ArgumentException("engine: this member takes an engine bound to its instrument; bind it with BindOn", "engine");

    /// <summary>The refusal of a member the engine has no calculation for.</summary>
    /// <param name="member">The member's C name.</param>
    /// <returns>The exception.</returns>
    public ArgumentException NoCalculation(string member) =>
        new($"engine: {Set[..Set.LastIndexOf('#')]}, bound with these inputs, has no calculation {member} can take", "engine");

    /// <summary>The refusal of a pair the generator found unformable.</summary>
    /// <param name="reason">Why.</param>
    /// <returns>The exception.</returns>
    public ArgumentException Refuse(string reason) => new($"engine: {Set[..Set.LastIndexOf('#')]}: {reason}", "engine");
}
