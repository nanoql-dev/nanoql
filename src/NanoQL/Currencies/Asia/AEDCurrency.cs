// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/asia.hpp ql/currencies/asia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The United Arab Emirates dirham.</summary>
/// <remarks>The ISO three-letter code is AED; the numeric code is 784. It is divided into 100 fils.</remarks>
public sealed class AEDCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="AEDCurrency"/> class.</summary>
    public AEDCurrency()
        : base("United Arab Emirates dirham", "AED", 784, "AED", "", 100, DataRounding)
    {
    }
}
