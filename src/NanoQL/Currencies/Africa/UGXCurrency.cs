// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2023 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/africa.hpp ql/currencies/africa.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Ugandan shilling.</summary>
/// <remarks>The ISO three-letter code is UGX; the numeric code is 800. It is the smallest unit.</remarks>
public sealed class UGXCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="UGXCurrency"/> class.</summary>
    public UGXCurrency()
        : base("Ugandan shilling", "UGX", 800, "UGX", "", 1, DataRounding)
    {
    }
}
