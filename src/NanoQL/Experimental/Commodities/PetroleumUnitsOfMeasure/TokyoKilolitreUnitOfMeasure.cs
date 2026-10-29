// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/petroleumunitsofmeasure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>Tokyo kilolitres: code KL_tk, a volume, converted through barrels.</summary>
[Experimental("NQL0001")]
public sealed class TokyoKilolitreUnitOfMeasure : UnitOfMeasure
{
    /// <summary>Initializes a new instance of the <see cref="TokyoKilolitreUnitOfMeasure"/> class.</summary>
    public TokyoKilolitreUnitOfMeasure()
        : base("Tokyo Kilolitres", "KL_tk", UnitOfMeasureType.Volume, new BarrelUnitOfMeasure())
    {
    }
}
