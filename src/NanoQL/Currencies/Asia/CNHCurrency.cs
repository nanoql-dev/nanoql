// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/asia.hpp ql/currencies/asia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Chinese yuan (Hong Kong).</summary>
/// <remarks>The ISO three-letter code is CNH; there is no numeric code. It is divided in 100 fen.</remarks>
public sealed class CNHCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="CNHCurrency"/> class.</summary>
    public CNHCurrency()
        : base("Chinese yuan (Hong Kong)", "CNH", 156, "CNH", "", 100, DataRounding)
    {
    }
}
