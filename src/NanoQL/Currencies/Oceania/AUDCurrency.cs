// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/oceania.hpp ql/currencies/oceania.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Australian dollar.</summary>
/// <remarks>The ISO three-letter code is AUD; the numeric code is 36. It is divided into 100 cents.</remarks>
public sealed class AUDCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="AUDCurrency"/> class.</summary>
    public AUDCurrency()
        : base(ConventionProvider.ReadCurrency("AUD", Reference))
    {
    }

    // QuantLib's coded values, the reference the provider resolves.
    private static CurrencyData Reference() =>
        new("Australian dollar", "AUD", 36, "A$", "", 100, DataRounding);
}
