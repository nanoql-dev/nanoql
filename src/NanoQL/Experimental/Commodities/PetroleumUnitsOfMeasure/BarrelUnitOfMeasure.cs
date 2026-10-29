// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/petroleumunitsofmeasure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>Barrels: code BBL, a volume, the base unit of <see cref="CommoditySettings"/> by default.</summary>
[Experimental("NQL0001")]
public sealed class BarrelUnitOfMeasure : UnitOfMeasure
{
    /// <summary>Initializes a new instance of the <see cref="BarrelUnitOfMeasure"/> class.</summary>
    public BarrelUnitOfMeasure()
        : base("Barrels", "BBL", UnitOfMeasureType.Volume, null)
    {
    }
}
