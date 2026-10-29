// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/commoditysettings.hpp ql/experimental/commodities/commoditysettings.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Currencies;

namespace NanoQL.Experimental;

/// <summary>
/// The base currency and unit of measure the energy instruments value in: QuantLib's process-wide
/// <c>CommoditySettings</c>, as a value that <see cref="EvaluationContext.CommoditySettings"/> holds.
/// </summary>
/// <remarks>The defaults are QuantLib's: U.S. dollars and barrels (<c>commoditysettings.cpp:27-28</c>).</remarks>
[Experimental("NQL0001")]
public sealed record CommoditySettings
{
    private readonly Currency currency = new USDCurrency();
    private readonly UnitOfMeasure unitOfMeasure = new BarrelUnitOfMeasure();

    /// <summary>Gets the base currency; QuantLib's default is <see cref="USDCurrency"/>.</summary>
    /// <exception cref="ArgumentNullException">Set to <see langword="null"/>.</exception>
    public Currency Currency
    {
        get => currency;
        init => currency = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets the base unit of measure; QuantLib's default is <see cref="BarrelUnitOfMeasure"/>.</summary>
    /// <exception cref="ArgumentNullException">Set to <see langword="null"/>.</exception>
    public UnitOfMeasure UnitOfMeasure
    {
        get => unitOfMeasure;
        init => unitOfMeasure = value ?? throw new ArgumentNullException(nameof(value));
    }
}
