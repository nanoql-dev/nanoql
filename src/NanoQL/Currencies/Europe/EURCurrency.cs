// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/europe.hpp ql/currencies/europe.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The European euro: ISO code EUR, numeric code 978, divided into 100 cents.</summary>
public sealed class EURCurrency : Currency
{
    /// <summary>Initializes a new instance of the <see cref="EURCurrency"/> class.</summary>
    public EURCurrency()
        : base("European Euro", "EUR", 978, "", "", 100, new ClosestRounding(2))
    {
    }
}
