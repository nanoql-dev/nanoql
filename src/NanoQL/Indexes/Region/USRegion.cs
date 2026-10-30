// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2014 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/region.hpp ql/indexes/region.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Indexes;

/// <summary>The region of the United States: name "USA", code "US".</summary>
public sealed class USRegion : Region
{
    /// <summary>Initializes a new instance of the <see cref="USRegion"/> class.</summary>
    public USRegion()
        : base(ConventionProvider.ReadRegion("USRegion", Reference))
    {
    }

    // QuantLib's data.
    private static RegionData Reference() => new("USA", "US");
}
