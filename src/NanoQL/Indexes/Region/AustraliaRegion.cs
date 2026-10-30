// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2014 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/region.hpp ql/indexes/region.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Indexes;

/// <summary>The region of Australia: name "Australia", code "AU".</summary>
public sealed class AustraliaRegion : Region
{
    /// <summary>Initializes a new instance of the <see cref="AustraliaRegion"/> class.</summary>
    public AustraliaRegion()
        : base(ConventionProvider.ReadRegion("AustraliaRegion", Reference))
    {
    }

    // QuantLib's data.
    private static RegionData Reference() => new("Australia", "AU");
}
