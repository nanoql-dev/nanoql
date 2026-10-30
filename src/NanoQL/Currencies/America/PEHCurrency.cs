// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/america.hpp ql/currencies/america.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Peruvian sol.</summary>
/// <remarks>
/// <para>
/// The ISO three-letter code was PEH. A numeric code is not available; as per ISO 3166-1, we assign 999 as a
/// user-defined code. It was divided in 100 centavos.
/// </para>
/// <para>
/// Obsoleted by the inti since February 1985.
/// </para>
/// </remarks>
public sealed class PEHCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="PEHCurrency"/> class.</summary>
    public PEHCurrency()
        : base(ConventionProvider.ReadCurrency("PEH", Reference))
    {
    }

    // QuantLib's coded values, the reference the provider resolves.
    private static CurrencyData Reference() =>
        new("Peruvian sol", "PEH", 999, "S./", "", 100, DataRounding);
}
