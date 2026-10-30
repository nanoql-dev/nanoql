// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/petroleumunitsofmeasure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>Metric tonnes: code MT, a mass.</summary>
[Experimental("NQL0001")]
public sealed class MTUnitOfMeasure : UnitOfMeasure
{
    /// <summary>Initializes a new instance of the <see cref="MTUnitOfMeasure"/> class.</summary>
    public MTUnitOfMeasure()
        : base(ConventionProvider.ReadUnitOfMeasure("MTUnitOfMeasure", Reference))
    {
    }

    // QuantLib's data, built in the provider's scope.
    private static UnitOfMeasureData Reference() => new("Metric Tonnes", "MT", UnitOfMeasureType.Mass, null);
}
