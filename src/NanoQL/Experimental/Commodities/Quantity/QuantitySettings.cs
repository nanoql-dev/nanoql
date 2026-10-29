// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/quantity.hpp ql/experimental/commodities/quantity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// The settings that say how <see cref="Quantity"/> amounts in different units combine: QuantLib's static
/// <c>Quantity::conversionType</c> and <c>Quantity::baseUnitOfMeasure</c>, as a value that
/// <see cref="EvaluationContext.QuantitySettings"/> holds.
/// </summary>
/// <remarks>The defaults are QuantLib's: no conversion, and no base unit (<c>quantity.cpp:27-29</c>).</remarks>
[Experimental("NQL0001")]
public sealed record QuantitySettings
{
    /// <summary>Gets how quantities in different units combine; QuantLib's default is
    /// <see cref="QuantityConversionType.NoConversion"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Set to a value that isn't a member of the enum.</exception>
    public QuantityConversionType ConversionType
    {
        get;
        init
        {
            EnumArguments.Check(value, nameof(ConversionType));
            field = value;
        }
    }

    /// <summary>Gets the unit both operands convert to under
    /// <see cref="QuantityConversionType.BaseUnitOfMeasureConversion"/>; QuantLib's default is none, a
    /// <see langword="null"/>.</summary>
    public UnitOfMeasure? BaseUnitOfMeasure { get; init; }
}
