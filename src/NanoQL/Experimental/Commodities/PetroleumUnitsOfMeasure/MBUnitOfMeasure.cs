// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/petroleumunitsofmeasure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>Thousands of barrels: code MB, a volume, converted through barrels.</summary>
[Experimental("NQL0001")]
public sealed class MBUnitOfMeasure : UnitOfMeasure
{
    /// <summary>Initializes a new instance of the <see cref="MBUnitOfMeasure"/> class.</summary>
    public MBUnitOfMeasure()
        : base(ConventionProvider.ReadUnitOfMeasure("MBUnitOfMeasure", Reference))
    {
    }

    // QuantLib's data, built in the provider's scope.
    private static UnitOfMeasureData Reference() =>
        new("1000 Barrels", "MB", UnitOfMeasureType.Volume, new BarrelUnitOfMeasure());
}
