// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/europe.hpp ql/currencies/europe.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Cyprus pound.</summary>
/// <remarks>
/// <para>
/// The ISO three-letter code is CYP; the numeric code is 196. It is divided in 100 cents.
/// </para>
/// <para>
/// Obsoleted by the Euro since 2008.
/// </para>
/// </remarks>
public sealed class CYPCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="CYPCurrency"/> class.</summary>
    public CYPCurrency()
        : base("Cyprus pound", "CYP", 196, "\u00A3C", "", 100, DataRounding)
    {
    }
}
