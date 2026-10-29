// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/unitofmeasure.hpp ql/experimental/commodities/unitofmeasure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A lot: QuantLib's <c>LotUnitOfMeasure</c>, code Lot, a count.</summary>
[Experimental("NQL0001")]
public sealed class LotUnitOfMeasure : UnitOfMeasure
{
    /// <summary>Initializes a new instance of the <see cref="LotUnitOfMeasure"/> class.</summary>
    public LotUnitOfMeasure()
        : base("Lot", "Lot", UnitOfMeasureType.Quantity, null)
    {
    }
}
