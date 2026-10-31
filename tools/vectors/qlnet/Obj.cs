// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.QLNetVectors;

/// <summary>A JSON object whose keys keep the order they were set in, as a Python dict does.</summary>
internal sealed class Obj
{
    private readonly List<(string Key, object? Value)> entries = [];

    /// <summary>Gets the entries, in the order they were first set.</summary>
    public IReadOnlyList<(string Key, object? Value)> Entries => entries;

    /// <summary>Sets a key, keeping its first position when it was already set.</summary>
    /// <param name="key">The key.</param>
    public object? this[string key]
    {
        get => entries.First(e => e.Key == key).Value;
        set
        {
            int i = entries.FindIndex(e => e.Key == key);
            if (i >= 0)
            {
                entries[i] = (key, value);
            }
            else
            {
                entries.Add((key, value));
            }
        }
    }

    /// <summary>Whether the key is set.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> if it is.</returns>
    public bool Has(string key) => entries.Exists(e => e.Key == key);
}
