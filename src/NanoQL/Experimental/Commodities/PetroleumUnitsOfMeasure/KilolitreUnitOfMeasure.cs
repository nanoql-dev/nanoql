// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/petroleumunitsofmeasure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>Kilolitres: code kl, a volume, converted through barrels.</summary>
[Experimental("NQL0001")]
public sealed class KilolitreUnitOfMeasure : UnitOfMeasure
{
    /// <summary>Initializes a new instance of the <see cref="KilolitreUnitOfMeasure"/> class.</summary>
    public KilolitreUnitOfMeasure()
        : base("Kilolitres", "kl", UnitOfMeasureType.Volume, new BarrelUnitOfMeasure())
    {
    }
}
