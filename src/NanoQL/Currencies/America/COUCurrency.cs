// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/america.hpp ql/currencies/america.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Unidad de Valor Real.</summary>
/// <remarks>The ISO three-letter code is COU; the numeric code is 970. A unit of account used in Colombia.</remarks>
public sealed class COUCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="COUCurrency"/> class.</summary>
    public COUCurrency()
        : base(ConventionProvider.ReadCurrency("COU", Reference))
    {
    }

    // QuantLib's coded values, the reference the provider resolves.
    private static CurrencyData Reference() =>
        new("Unidad de Valor Real (UVR) (funds code)", "COU", 970, "COU", "", 100, DataRounding);
}
