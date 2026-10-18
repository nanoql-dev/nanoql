// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/asia.hpp ql/currencies/asia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Hong Kong dollar.</summary>
/// <remarks>The ISO three-letter code is HKD; the numeric code is 344. It is divided in 100 cents.</remarks>
public sealed class HKDCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="HKDCurrency"/> class.</summary>
    public HKDCurrency()
        : base("Hong Kong dollar", "HKD", 344, "HK$", "", 100, DataRounding)
    {
    }
}
