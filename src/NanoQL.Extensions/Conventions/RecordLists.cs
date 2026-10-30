// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;

namespace NanoQL.Extensions.Conventions;

// A record's list fields compared and hashed by content, as a record's synthesized equality doesn't.
internal static class RecordLists
{
    public static bool Equal<T>(ImmutableArray<T> a, ImmutableArray<T> b) =>
        a.IsDefaultOrEmpty ? b.IsDefaultOrEmpty : !b.IsDefault && a.SequenceEqual(b);

    public static void Add<T>(ref HashCode hash, ImmutableArray<T> items)
    {
        hash.Add(items.IsDefault ? 0 : items.Length);
        if (!items.IsDefault)
        {
            foreach (T item in items)
            {
                hash.Add(item);
            }
        }
    }
}
