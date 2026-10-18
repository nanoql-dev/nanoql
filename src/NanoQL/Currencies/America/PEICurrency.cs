// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/currencies/america.hpp ql/currencies/america.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Currencies;

/// <summary>The Peruvian inti.</summary>
/// <remarks>
/// <para>
/// The ISO three-letter code was PEI. It was divided in 100 centimos. A numeric code is not available; as per ISO
/// 3166-1, we assign 998 as a user-defined code.
/// </para>
/// <para>
/// Obsoleted by the nuevo sol since July 1991.
/// </para>
/// </remarks>
public sealed class PEICurrency : Currency
{
    // Built once and shared by every instance, as QuantLib's static data is.
    private static readonly Rounding DataRounding = new();

    /// <summary>Initializes a new instance of the <see cref="PEICurrency"/> class.</summary>
    public PEICurrency()
        : base("Peruvian inti", "PEI", 998, "I/.", "", 100, DataRounding)
    {
    }
}
