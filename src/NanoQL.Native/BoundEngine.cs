// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Native;

/// <summary>
/// A bound engine: one of nanoql's engines with the inputs given to its <c>Bind</c> filled in, each by its parameter's
/// name, and with <c>BindOn</c> its instrument too. A member that takes an engine turns it into the delegate it takes
/// (the generated adapters): its own arguments go into their roles' parameters, the bound inputs fill the rest, and a
/// parameter open on both sides keeps its default. Immutable, so any thread may use it.
/// </summary>
/// <param name="set">The engine input set's id: the engine's full name and the set's index.</param>
/// <param name="instrument">The instrument <c>BindOn</c> was given, or null.</param>
/// <param name="inputs">The bound inputs by parameter name.</param>
internal sealed class BoundEngine(string set, object? instrument, IReadOnlyDictionary<string, object> inputs)
{
    /// <summary>Gets the engine input set's id.</summary>
    public string Set { get; } = set;

    /// <summary>Gets the instrument <c>BindOn</c> was given, or null.</summary>
    public object? Instrument { get; } = instrument;

    /// <summary>A bound input no default fills: refused when it is open.</summary>
    /// <typeparam name="T">The parameter's type.</typeparam>
    /// <param name="name">The parameter.</param>
    /// <returns>The input.</returns>
    public T Take<T>(string name) =>
        inputs.TryGetValue(name, out object? v) ? (T)v : throw new HandleException(10, $"engine: '{name}' is neither bound nor supplied", "engine");

    /// <summary>A bound input, or its default when open.</summary>
    /// <typeparam name="T">The parameter's type.</typeparam>
    /// <param name="name">The parameter.</param>
    /// <param name="fallback">The parameter's default.</param>
    /// <returns>The input.</returns>
    public T TakeOr<T>(string name, T fallback) => inputs.TryGetValue(name, out object? v) ? (T)v : fallback;

    /// <summary>A parameter the member supplies: refused when it is bound too.</summary>
    /// <param name="name">The parameter.</param>
    public void Open(string name)
    {
        if (inputs.ContainsKey(name))
        {
            throw new HandleException(10, $"engine: '{name}' is bound, and the member supplies it", "engine");
        }
    }

    /// <summary>For a member that supplies the instrument: refused when <c>BindOn</c> bound one.</summary>
    public void NoInstrument()
    {
        if (Instrument != null)
        {
            throw new HandleException(10, "engine: bound to an instrument (BindOn), and the member supplies one; bind it with Bind", "engine");
        }
    }

    /// <summary>For a member that takes the context alone: the instrument <c>BindOn</c> bound, refused when none.</summary>
    /// <returns>The instrument.</returns>
    public object NeedInstrument() =>
        Instrument ?? throw new HandleException(10, "engine: this member takes an engine bound to its instrument; bind it with BindOn", "engine");

    /// <summary>The refusal of an engine with no calculation for a member.</summary>
    /// <param name="member">The member's C function.</param>
    /// <returns>The refusal.</returns>
    public HandleException NoCalculation(string member) =>
        new(10, $"engine: {Set[..Set.LastIndexOf('#')]}, bound with these inputs, has no calculation {member} can take", "engine");

    /// <summary>The refusal of a pair the generator lists as refused: an engine with no parameter for a role.</summary>
    /// <param name="reason">Why.</param>
    /// <returns>The refusal.</returns>
    public HandleException Refuse(string reason) => new(10, $"engine: {Set[..Set.LastIndexOf('#')]}: {reason}", "engine");
}
