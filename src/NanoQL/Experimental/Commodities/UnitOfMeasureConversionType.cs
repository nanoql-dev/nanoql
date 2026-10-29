// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/unitofmeasureconversion.hpp ql/experimental/commodities/unitofmeasureconversion.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>Where a unit conversion comes from: QuantLib's <c>UnitOfMeasureConversion::Type</c>.</summary>
[Experimental("NQL0001")]
public enum UnitOfMeasureConversionType
{
    /// <summary>Given directly by the user.</summary>
    Direct = 0,

    /// <summary>Derived from conversion factors between other units.</summary>
    Derived = 1,
}
