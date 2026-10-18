// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/asia.hpp ql/currencies/asia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Chinese yuan.</summary>
/// <remarks>The ISO three-letter code is CNY; the numeric code is 156. It is divided in 100 fen.</remarks>
public sealed class CNYCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="CNYCurrency"/> class.</summary>
    public CNYCurrency()
        : base("Chinese yuan", "CNY", 156, "Y", "", 100, DataRounding)
    {
    }
}
