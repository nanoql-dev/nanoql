// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Harness;

/// <summary>
/// The currencies nanoql ports, by their ISO code, for the vectors that name a currency by its code (an exchange
/// rate's, the money settings' base currency): each public currency class with a parameterless constructor.
/// </summary>
public static class VectorCurrencies
{
    private static readonly Dictionary<string, Func<Currency>> ByCodes = Build();

    /// <summary>The currency with a code.</summary>
    /// <param name="code">The ISO code, such as "EUR".</param>
    /// <returns>A new instance of the currency.</returns>
    /// <exception cref="KeyNotFoundException">No ported currency has the code.</exception>
    public static Currency ByCode(string code) =>
        ByCodes.TryGetValue(code, out Func<Currency>? make)
            ? make()
            : throw new KeyNotFoundException($"no ported currency has the code {code}");

    private static Dictionary<string, Func<Currency>> Build()
    {
        var map = new Dictionary<string, Func<Currency>>(StringComparer.Ordinal);
        foreach (Type t in typeof(Currency).Assembly.GetExportedTypes())
        {
            if (t.IsSubclassOf(typeof(Currency)) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is { } ctor)
            {
                var currency = (Currency)ctor.Invoke(null);
                map.Add(currency.Code, () => (Currency)ctor.Invoke(null));
            }
        }

        return map;
    }
}
