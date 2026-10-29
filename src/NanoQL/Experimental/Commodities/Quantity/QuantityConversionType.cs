// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/quantity.hpp ql/experimental/commodities/quantity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// How quantities in different units are combined: QuantLib's <c>Quantity::ConversionType</c>, read from
/// <see cref="QuantitySettings.ConversionType"/>.
/// </summary>
[Experimental("NQL0001")]
public enum QuantityConversionType
{
    /// <summary>Do not convert: combining quantities in different units is refused.</summary>
    NoConversion = 0,

    /// <summary>Convert both operands to the base unit before combining them.</summary>
    BaseUnitOfMeasureConversion = 1,

    /// <summary>Convert the second operand into the first's unit, where the result is.</summary>
    AutomatedConversion = 2,
}
