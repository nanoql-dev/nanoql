// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/europe.hpp ql/currencies/europe.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Croatian kuna.</summary>
/// <remarks>
/// <para>
/// The ISO three-letter code was HRK; the numeric code was 191. It was divided into 100 lipa.
/// </para>
/// <para>
/// Obsoleted by the Euro since 2023.
/// </para>
/// </remarks>
public sealed class HRKCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="HRKCurrency"/> class.</summary>
    public HRKCurrency()
        : base(ConventionProvider.ReadCurrency("HRK", Reference))
    {
    }

    // QuantLib's coded values, the reference the provider resolves.
    private static CurrencyData Reference() =>
        new("Croatian kuna", "HRK", 191, "HRK", "", 100, DataRounding);
}
