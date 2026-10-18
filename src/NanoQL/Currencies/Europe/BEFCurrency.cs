// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/europe.hpp ql/currencies/europe.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Belgian franc.</summary>
/// <remarks>
/// <para>
/// The ISO three-letter code was BEF; the numeric code was 56. It had no subdivisions.
/// </para>
/// <para>
/// Obsoleted by the Euro since 1999.
/// </para>
/// </remarks>
public sealed class BEFCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();
    private static readonly EURCurrency DataTriangulationCurrency = new();

    /// <summary>Initializes a new instance of the <see cref="BEFCurrency"/> class.</summary>
    public BEFCurrency()
        : base("Belgian franc", "BEF", 56, "", "", 1, DataRounding, DataTriangulationCurrency)
    {
    }
}
