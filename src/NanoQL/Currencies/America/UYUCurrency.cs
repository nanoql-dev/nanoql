// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/america.hpp ql/currencies/america.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Uruguayan peso.</summary>
/// <remarks>The ISO three-letter code is UYU; the numeric code is 858. A unit of account used in Uruguay.</remarks>
public sealed class UYUCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="UYUCurrency"/> class.</summary>
    public UYUCurrency()
        : base("Uruguayan peso", "UYU", 858, "UYU", "", 1, DataRounding)
    {
    }
}
