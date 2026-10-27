// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/prices.hpp ql/prices.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>Price types.</summary>
public enum PriceType
{
    /// <summary>Bid price.</summary>
    Bid,

    /// <summary>Ask price.</summary>
    Ask,

    /// <summary>Last price.</summary>
    Last,

    /// <summary>Close price.</summary>
    Close,

    /// <summary>Mid price, calculated as the arithmetic average of bid and ask prices.</summary>
    Mid,

    /// <summary>
    /// Mid equivalent price, calculated as a) the arithmetic average of bid and ask prices when both are available; b)
    /// either the bid or the ask price if any of them is available; c) the last price; or d) the close price.
    /// </summary>
    MidEquivalent,

    /// <summary>Safe Mid price, returns the mid price only if both bid and ask are available.</summary>
    MidSafe,
}
