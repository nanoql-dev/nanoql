// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/commodityunitcost.hpp ql/experimental/commodities/commodityunitcost.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A cost per unit of a commodity: an amount of money per unit of measure, such as 0.1 USD a gallon.</summary>
/// <remarks>Immutable. QuantLib's default <c>CommodityUnitCost()</c>, a zero amount in no currency per no unit, is
/// <c>new CommodityUnitCost(default, null)</c>.</remarks>
[Experimental("NQL0001")]
public sealed class CommodityUnitCost
{
    /// <summary>Initializes a new instance of the <see cref="CommodityUnitCost"/> class.</summary>
    /// <param name="amount">The amount per unit.</param>
    /// <param name="unitOfMeasure">The unit the amount is per; <see langword="null"/> for QuantLib's empty one.</param>
    public CommodityUnitCost(Money amount, UnitOfMeasure? unitOfMeasure)
    {
        Amount = amount;
        UnitOfMeasure = unitOfMeasure;
    }

    /// <summary>Gets the amount per unit.</summary>
    public Money Amount { get; }

    /// <summary>Gets the unit the amount is per; <see langword="null"/> for QuantLib's empty one.</summary>
    public UnitOfMeasure? UnitOfMeasure { get; }

    /// <summary>
    /// The cost as QuantLib's <c>operator&lt;&lt;</c> writes it: the amount's value in C++'s default stream format, the
    /// currency's code, a slash and the unit's code ("0.1 USD/GAL").
    /// </summary>
    /// <returns>The text.</returns>
    /// <exception cref="NanoQLException">The currency or the unit is QuantLib's empty one.</exception>
    public override string ToString()
    {
        Currency currency = Amount.Currency ?? throw new NanoQLException("no currency data provided");
        UnitOfMeasure unit = UnitOfMeasure ?? throw new NanoQLException("no unit of measure data provided");
        return $"{Utilities.DataFormattersFunctions.General(Amount.Value)} {currency.Code}/{unit.Code}";
    }
}
