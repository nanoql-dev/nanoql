// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/europe.hpp ql/currencies/europe.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Luxembourg franc.</summary>
/// <remarks>
/// <para>
/// The ISO three-letter code was LUF; the numeric code was 442. It was divided in 100 centimes.
/// </para>
/// <para>
/// Obsoleted by the Euro since 1999.
/// </para>
/// </remarks>
public sealed class LUFCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="LUFCurrency"/> class.</summary>
    public LUFCurrency()
        : base(ConventionProvider.ReadCurrency("LUF", Reference))
    {
    }

    // QuantLib's coded values, the reference the provider resolves.
    private static CurrencyData Reference() =>
        new("Luxembourg franc", "LUF", 442, "F", "", 100, DataRounding, new EURCurrency());
}
