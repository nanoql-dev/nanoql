// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/crypto.hpp ql/currencies/crypto.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>Litecoin.</summary>
/// <remarks>https://litecoin.com/</remarks>
public sealed class LTCCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="LTCCurrency"/> class.</summary>
    public LTCCurrency()
        : base(ConventionProvider.ReadCurrency("LTC", Reference))
    {
    }

    // QuantLib's coded values, the reference the provider resolves.
    private static CurrencyData Reference() =>
        new("Litecoin", "LTC", 10005, "LTC", "", 100000, DataRounding);
}
