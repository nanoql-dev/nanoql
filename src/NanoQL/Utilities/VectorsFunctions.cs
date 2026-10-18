// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/utilities/vectors.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Utilities;

/// <summary>Utilities for vector manipulation: QuantLib's <c>detail</c> functions of <c>vectors.hpp</c>.</summary>
internal static class VectorsFunctions
{
    /// <summary>
    /// An element of a vector of per-period values, as the leg builders read them: the default when the vector is
    /// empty, the element when the index is in range, and the last element past the end.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="v">The vector.</param>
    /// <param name="i">The index.</param>
    /// <param name="defaultValue">The value for an empty vector.</param>
    /// <returns>The element.</returns>
    internal static T Get<T>(ImmutableArray<T> v, int i, T defaultValue)
    {
        if (v.IsEmpty)
        {
            return defaultValue;
        }
        else if (i < v.Length)
        {
            return v[i];
        }
        else
        {
            return v[^1];
        }
    }
}
