// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/crypto.hpp ql/currencies/crypto.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>Bitcoin.</summary>
/// <remarks>https://bitcoin.org/</remarks>
public sealed class BTCCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="BTCCurrency"/> class.</summary>
    public BTCCurrency()
        : base("Bitcoin", "BTC", 10000, "BTC", "", 100000, DataRounding)
    {
    }
}
