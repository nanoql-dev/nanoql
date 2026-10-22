// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/crypto.hpp ql/currencies/crypto.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>Dash coin.</summary>
/// <remarks>https://www.dash.org/</remarks>
public sealed class DASHCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="DASHCurrency"/> class.</summary>
    public DASHCurrency()
        : base("Dash coin", "DASH", 10006, "DASH", "", 100000, DataRounding)
    {
    }
}
