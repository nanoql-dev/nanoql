// SPDX-FileCopyrightText: Copyright (C) 2004 Jeff Yu
// SPDX-FileCopyrightText: Copyright (C) 2004 M-Dimension Consulting Inc.
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008, 2010 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2022 Oleg Kulkov
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bond.hpp ql/instruments/bond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>A bond price, clean or dirty, per 100 of notional: QuantLib's <c>Bond::Price</c>.</summary>
/// <remarks>
/// QuantLib's default-constructed price, whose amount is <c>Null&lt;Real&gt;()</c>, is "no price": its only use is
/// <c>BondFunctions::atmRate</c>'s default, which nanoql's <see cref="PricingEngines.BondFunctions"/> takes as a
/// <see langword="null"/> price. So every price holds an amount, <see cref="Amount"/> never refuses, and an amount equal to
/// QuantLib's sentinel is a price (a documented divergence).
/// </remarks>
public sealed class BondPrice
{
    /// <summary>Initializes a new instance of the <see cref="BondPrice"/> class.</summary>
    /// <param name="amount">The price, per 100 of notional.</param>
    /// <param name="type">Whether the price is clean or dirty.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its enum.</exception>
    public BondPrice(double amount, BondPriceType type)
    {
        EnumArguments.Check(type, nameof(type));
        Amount = amount;
        Type = type;
    }

    /// <summary>Gets the price, per 100 of notional.</summary>
    public double Amount { get; }

    /// <summary>Gets whether the price is clean or dirty.</summary>
    public BondPriceType Type { get; }
}
