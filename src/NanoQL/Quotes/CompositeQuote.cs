// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/quotes/compositequote.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Quotes;

/// <summary>A quote whose value is a function of two other quotes' values: QuantLib's <c>CompositeQuote</c>.</summary>
/// <remarks>
/// Immutable: its value is computed once, at construction, from the elements' values (§2.2's quote row). QuantLib's
/// quote recomputes when an element notifies it; here new element values are a new quote. QuantLib's <c>isValid</c>
/// isn't ported: a value is always valid.
/// </remarks>
public sealed class CompositeQuote
{
    /// <summary>Initializes a new instance of the <see cref="CompositeQuote"/> class.</summary>
    /// <param name="element1">The first element's value.</param>
    /// <param name="element2">The second element's value.</param>
    /// <param name="f">The function of the two values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    public CompositeQuote(double element1, double element2, Func<double, double, double> f)
    {
        ArgumentNullException.ThrowIfNull(f);
        Value1 = element1;
        Value2 = element2;
        Value = f(element1, element2);
    }

    /// <summary>Gets the first element's value.</summary>
    public double Value1 { get; }

    /// <summary>Gets the second element's value.</summary>
    public double Value2 { get; }

    /// <summary>Gets the value: the function of the two elements' values.</summary>
    public double Value { get; }
}
