// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/america.hpp ql/currencies/america.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The U.S. dollar: ISO code USD, numeric code 840, divided into 100 cents.</summary>
public sealed class USDCurrency : Currency
{
    /// <summary>Initializes a new instance of the <see cref="USDCurrency"/> class.</summary>
    public USDCurrency()
        : base("U.S. dollar", "USD", 840, "$", "\u00A2", 100, new Rounding())
    {
    }
}
