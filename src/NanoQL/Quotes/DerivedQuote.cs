// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/quotes/derivedquote.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Quotes;

/// <summary>A quote whose value is a function of another quote's: QuantLib's <c>DerivedQuote</c>.</summary>
/// <remarks>
/// Immutable: its value is computed once, at construction, from the element's value (§2.2's quote row). QuantLib's
/// quote recomputes when its element notifies it; here a new element value is a new quote. QuantLib's <c>isValid</c>
/// isn't ported: a value is always valid.
/// </remarks>
public sealed class DerivedQuote
{
    /// <summary>Initializes a new instance of the <see cref="DerivedQuote"/> class.</summary>
    /// <param name="element">The element's value.</param>
    /// <param name="f">The function of the element's value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    public DerivedQuote(double element, Func<double, double> f)
    {
        ArgumentNullException.ThrowIfNull(f);
        Value = f(element);
    }

    /// <summary>Gets the value: the function of the element's value.</summary>
    public double Value { get; }
}
