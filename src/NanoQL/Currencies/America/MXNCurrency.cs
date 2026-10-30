// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/america.hpp ql/currencies/america.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Mexican peso.</summary>
/// <remarks>The ISO three-letter code is MXN; the numeric code is 484. It is divided in 100 centavos.</remarks>
public sealed class MXNCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="MXNCurrency"/> class.</summary>
    public MXNCurrency()
        : base(ConventionProvider.ReadCurrency("MXN", Reference))
    {
    }

    // QuantLib's coded values, the reference the provider resolves.
    private static CurrencyData Reference() =>
        new("Mexican peso", "MXN", 484, "Mex$", "", 100, DataRounding);
}
