// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/asia.hpp ql/currencies/asia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Indian rupee.</summary>
/// <remarks>The ISO three-letter code is INR; the numeric code is 356. It is divided in 100 paise.</remarks>
public sealed class INRCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="INRCurrency"/> class.</summary>
    public INRCurrency()
        : base("Indian rupee", "INR", 356, "Rs", "", 100, DataRounding)
    {
    }
}
