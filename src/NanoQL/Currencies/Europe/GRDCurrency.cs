// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/europe.hpp ql/currencies/europe.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Greek drachma.</summary>
/// <remarks>
/// <para>
/// The ISO three-letter code was GRD; the numeric code was 300. It was divided in 100 lepta.
/// </para>
/// <para>
/// Obsoleted by the Euro since 2001.
/// </para>
/// </remarks>
public sealed class GRDCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="GRDCurrency"/> class.</summary>
    public GRDCurrency()
        : base(ConventionProvider.ReadCurrency("GRD", Reference))
    {
    }

    // QuantLib's coded values, the reference the provider resolves.
    private static CurrencyData Reference() =>
        new("Greek drachma", "GRD", 300, "", "", 100, DataRounding, new EURCurrency());
}
