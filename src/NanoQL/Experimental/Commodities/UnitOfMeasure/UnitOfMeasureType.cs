// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/unitofmeasure.hpp ql/experimental/commodities/unitofmeasure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>What a unit of measure measures: QuantLib's <c>UnitOfMeasure::Type</c>.</summary>
[Experimental("NQL0001")]
public enum UnitOfMeasureType
{
    /// <summary>A mass, such as metric tonnes.</summary>
    Mass = 0,

    /// <summary>A volume, such as barrels.</summary>
    Volume = 1,

    /// <summary>An energy.</summary>
    Energy = 2,

    /// <summary>A count, such as lots.</summary>
    Quantity = 3,
}
