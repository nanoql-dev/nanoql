// SPDX-FileCopyrightText: Copyright (C) 2010 Dimitri Reiswich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/quotes/deltavolquote.hpp ql/quotes/deltavolquote.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Quotes;

/// <summary>An at-the-money convention: QuantLib's <c>DeltaVolQuote::AtmType</c>.</summary>
public enum DeltaVolQuoteAtmType
{
    /// <summary>Not an at-the-money quote, the default.</summary>
    AtmNull = 0,

    /// <summary>The strike at the spot.</summary>
    AtmSpot = 1,

    /// <summary>The strike at the forward.</summary>
    AtmFwd = 2,

    /// <summary>The strike at which the call's and the put's deltas are equal and opposite.</summary>
    AtmDeltaNeutral = 3,

    /// <summary>The strike at which the vega is greatest.</summary>
    AtmVegaMax = 4,

    /// <summary>The strike at which the gamma is greatest.</summary>
    AtmGammaMax = 5,

    /// <summary>The strike at which the call's delta is 0.50 (forward deltas only).</summary>
    AtmPutCall50 = 6,
}
