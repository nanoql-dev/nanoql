// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/commoditytype.hpp ql/experimental/commodities/commoditytype.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// The commodity type of conversions that hold for any commodity: name and code "&lt;NULL&gt;", the type of QuantLib's
/// known unit conversions.
/// </summary>
[Experimental("NQL0001")]
public sealed class NullCommodityType : CommodityType
{
    /// <summary>Initializes a new instance of the <see cref="NullCommodityType"/> class.</summary>
    public NullCommodityType()
        : base("<NULL>", "<NULL>")
    {
    }
}
