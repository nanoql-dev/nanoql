// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2014 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/region.hpp ql/indexes/region.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Indexes;

/// <summary>A region of any name and code, built without a class of its own.</summary>
public sealed class CustomRegion : Region
{
    /// <summary>Initializes a new instance of the <see cref="CustomRegion"/> class.</summary>
    /// <param name="name">The region's name.</param>
    /// <param name="code">The region's code.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="code"/> is null.</exception>
    public CustomRegion(string name, string code)
        : base(name, code)
    {
    }
}
