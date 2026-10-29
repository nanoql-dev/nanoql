// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/petroleumunitsofmeasure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>US gallons: code GAL, a volume, converted through barrels.</summary>
[Experimental("NQL0001")]
public sealed class GallonUnitOfMeasure : UnitOfMeasure
{
    /// <summary>Initializes a new instance of the <see cref="GallonUnitOfMeasure"/> class.</summary>
    public GallonUnitOfMeasure()
        : base("US Gallons", "GAL", UnitOfMeasureType.Volume, new BarrelUnitOfMeasure())
    {
    }
}
