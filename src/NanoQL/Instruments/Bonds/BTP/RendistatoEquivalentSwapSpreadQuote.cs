// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/btp.hpp ql/instruments/bonds/btp.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The spread of a Rendistato calculator's basket yield over its equivalent swap's fair rate, as a quote.
/// </summary>
/// <remarks>
/// Immutable; its calculator computes when it is built, so the quote has no validity of its own (QuantLib's
/// <c>isValid</c> reports whether the calculator computes).
/// </remarks>
public sealed class RendistatoEquivalentSwapSpreadQuote
{
    /// <summary>Initializes a new instance of the <see cref="RendistatoEquivalentSwapSpreadQuote"/> class.</summary>
    /// <param name="r">The calculator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="r"/> is <see langword="null"/>.</exception>
    public RendistatoEquivalentSwapSpreadQuote(RendistatoCalculator r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Value = r.EquivalentSwapSpread;
    }

    /// <summary>Gets the basket's yield less the equivalent swap's fair rate.</summary>
    public double Value { get; }
}
