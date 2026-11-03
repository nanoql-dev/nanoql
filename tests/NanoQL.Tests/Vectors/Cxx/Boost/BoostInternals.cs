// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Instruments;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// nanoql's Boost ports are internal types, so the Boost harness vectors' readers bind their functions through
/// reflection, as <c>LowDiscrepancySequencesTests</c> reads its table: each a delegate to an internal static method of
/// <c>NanoQL.Numerics</c>, found by its parameter types.
/// </summary>
internal static class BoostInternals
{
    private static readonly Assembly Library = typeof(VanillaOption).Assembly;

    /// <summary>A delegate to the internal static method <paramref name="method"/> of <paramref name="type"/>.</summary>
    /// <typeparam name="TDelegate">The delegate's type, whose parameters select the overload.</typeparam>
    /// <param name="type">The type's name in <c>NanoQL.Numerics</c>.</param>
    /// <param name="method">The method's name.</param>
    /// <returns>The delegate.</returns>
    internal static TDelegate Function<TDelegate>(string type, string method)
        where TDelegate : Delegate
    {
        Type t = Library.GetType("NanoQL.Numerics." + type, throwOnError: true)!;
        Type[] parameters = [.. typeof(TDelegate).GetMethod("Invoke")!.GetParameters().Select(p => p.ParameterType)];
        MethodInfo m = t.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static, parameters)
            ?? throw new InvalidOperationException($"{type}.{method} isn't found");
        return m.CreateDelegate<TDelegate>();
    }
}
