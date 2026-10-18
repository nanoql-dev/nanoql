// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/europe.hpp ql/currencies/europe.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Belarussian ruble.</summary>
/// <remarks>The ISO three-letter code is BYR; the numeric code is 974. It has no subdivisions.</remarks>
public sealed class BYRCurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="BYRCurrency"/> class.</summary>
    public BYRCurrency()
        : base("Belarussian ruble", "BYR", 974, "BR", "", 1, DataRounding)
    {
    }
}
