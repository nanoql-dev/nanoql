// SPDX-FileCopyrightText: Copyright (C) 2026 Eugene Toder
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/quotes/multicompositequote.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Quotes;

/// <summary>
/// A quote whose value is a function of an array of other quotes' values: QuantLib's <c>MultiCompositeQuote</c>.
/// </summary>
/// <remarks>
/// Immutable: its value is computed once, at construction, from the elements' values, which the function receives as
/// a new <see cref="Vector"/> (§2.2's quote row). QuantLib's quote recomputes when an element notifies it; here new
/// element values are a new quote. QuantLib's <c>isValid</c> isn't ported: a value is always valid.
/// </remarks>
public sealed class MultiCompositeQuote
{
    private readonly ImmutableArray<double> elements;

    /// <summary>Initializes a new instance of the <see cref="MultiCompositeQuote"/> class.</summary>
    /// <param name="elements">The elements' values.</param>
    /// <param name="f">The function of the values, given as a vector in the elements' order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="elements"/> or <paramref name="f"/> is
    /// <see langword="null"/>.</exception>
    public MultiCompositeQuote(IEnumerable<double> elements, Func<Vector, double> f)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(f);
        this.elements = [.. elements];
        Value = f(new Vector(this.elements.AsSpan()));
    }

    /// <summary>Gets the value: the function of the elements' values.</summary>
    public double Value { get; }

    /// <summary>The value of an element.</summary>
    /// <param name="i">The element's index.</param>
    /// <returns>The element's value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't an element's index.</exception>
    public double InputValue(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, elements.Length);
        return elements[i];
    }
}
