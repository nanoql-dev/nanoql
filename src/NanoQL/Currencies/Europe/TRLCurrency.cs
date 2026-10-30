// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/europe.hpp ql/currencies/europe.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Turkish lira.</summary>
/// <remarks>
/// <para>
/// The ISO three-letter code was TRL; the numeric code was 792. It was divided in 100 kurus.
/// </para>
/// <para>
/// Obsoleted by the new Turkish lira since 2005.
/// </para>
/// </remarks>
public sealed class TRLCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="TRLCurrency"/> class.</summary>
    public TRLCurrency()
        : base(ConventionProvider.ReadCurrency("TRL", Reference))
    {
    }

    // QuantLib's coded values, the reference the provider resolves.
    private static CurrencyData Reference() =>
        new("Turkish lira", "TRL", 792, "TL", "", 100, DataRounding);
}
