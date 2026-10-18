// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/europe.hpp ql/currencies/europe.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Romanian leu.</summary>
/// <remarks>
/// <para>
/// The ISO three-letter code was ROL; the numeric code was 642. It was divided in 100 bani.
/// </para>
/// <para>
/// Obsoleted by the new leu since July 2005.
/// </para>
/// </remarks>
public sealed class ROLCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="ROLCurrency"/> class.</summary>
    public ROLCurrency()
        : base("Romanian leu", "ROL", 642, "L", "", 100, DataRounding)
    {
    }
}
