// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/btp.hpp ql/instruments/bonds/btp.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The length in years of a Rendistato calculator's equivalent swap, as a quote.</summary>
/// <remarks>
/// Immutable; its calculator computes when it is built, so the quote has no validity of its own (QuantLib's
/// <c>isValid</c> reports whether the calculator computes).
/// </remarks>
public sealed class RendistatoEquivalentSwapLengthQuote
{
    /// <summary>Initializes a new instance of the <see cref="RendistatoEquivalentSwapLengthQuote"/> class.</summary>
    /// <param name="r">The calculator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="r"/> is <see langword="null"/>.</exception>
    public RendistatoEquivalentSwapLengthQuote(RendistatoCalculator r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Value = r.EquivalentSwapLength;
    }

    /// <summary>Gets the equivalent swap's length in years.</summary>
    public double Value { get; }
}
