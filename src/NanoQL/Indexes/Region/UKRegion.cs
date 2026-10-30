// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2014 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/region.hpp ql/indexes/region.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Indexes;

/// <summary>The region of the United Kingdom: name "UK", code "UK".</summary>
public sealed class UKRegion : Region
{
    /// <summary>Initializes a new instance of the <see cref="UKRegion"/> class.</summary>
    public UKRegion()
        : base(ConventionProvider.ReadRegion("UKRegion", Reference))
    {
    }

    // QuantLib's data.
    private static RegionData Reference() => new("UK", "UK");
}
