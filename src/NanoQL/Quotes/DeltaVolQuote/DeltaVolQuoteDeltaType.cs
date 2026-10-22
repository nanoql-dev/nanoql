// SPDX-FileCopyrightText: Copyright (C) 2010 Dimitri Reiswich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/quotes/deltavolquote.hpp ql/quotes/deltavolquote.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Quotes;

/// <summary>An FX delta's quotation convention: QuantLib's <c>DeltaVolQuote::DeltaType</c>.</summary>
public enum DeltaVolQuoteDeltaType
{
    /// <summary>The spot delta, the usual Black-Scholes delta.</summary>
    Spot = 0,

    /// <summary>The forward delta.</summary>
    Fwd = 1,

    /// <summary>The premium-adjusted spot delta.</summary>
    PaSpot = 2,

    /// <summary>The premium-adjusted forward delta.</summary>
    PaFwd = 3,
}
