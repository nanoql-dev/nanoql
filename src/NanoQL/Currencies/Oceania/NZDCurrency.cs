// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/oceania.hpp ql/currencies/oceania.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The New Zealand dollar.</summary>
/// <remarks>The ISO three-letter code is NZD; the numeric code is 554. It is divided in 100 cents.</remarks>
public sealed class NZDCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="NZDCurrency"/> class.</summary>
    public NZDCurrency()
        : base("New Zealand dollar", "NZD", 554, "NZ$", "", 100, DataRounding)
    {
    }
}
