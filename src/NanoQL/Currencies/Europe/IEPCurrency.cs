// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/europe.hpp ql/currencies/europe.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Irish punt.</summary>
/// <remarks>
/// <para>
/// The ISO three-letter code was IEP; the numeric code was 372. It was divided in 100 pence.
/// </para>
/// <para>
/// Obsoleted by the Euro since 1999.
/// </para>
/// </remarks>
public sealed class IEPCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();
    private static readonly EURCurrency DataTriangulationCurrency = new();

    /// <summary>Initializes a new instance of the <see cref="IEPCurrency"/> class.</summary>
    public IEPCurrency()
        : base("Irish punt", "IEP", 372, "", "", 100, DataRounding, DataTriangulationCurrency)
    {
    }
}
