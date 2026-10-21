// SPDX-FileCopyrightText: Copyright (C) 2016 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/constnotionalcrosscurrencyswap.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;

namespace NanoQL.Instruments;

/// <summary>
/// A generic constant-notional cross-currency swap: the notional amounts are constant and exchanged at inception and
/// maturity.
/// </summary>
/// <remarks>
/// The first leg holds the pay currency cash flows and the second leg holds the receive currency cash flows. Unsealed,
/// as in QuantLib, where the basis and fixed-vs-floating swaps derive from it; it has
/// <see cref="CrossCurrencySwap"/>'s constructors, as QuantLib's inherits them. Its engine is
/// <see cref="PricingEngines.DiscountingConstNotionalCrossCurrencySwapEngine"/>.
/// </remarks>
public class ConstNotionalCrossCurrencySwap : CrossCurrencySwap
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConstNotionalCrossCurrencySwap"/> class with two legs and their
    /// currencies: the first leg is paid and the second is received.
    /// </summary>
    /// <param name="firstLeg">The cash flows of the first leg.</param>
    /// <param name="firstLegCcy">The currency of the first leg's cash flows.</param>
    /// <param name="secondLeg">The cash flows of the second leg.</param>
    /// <param name="secondLegCcy">The currency of the second leg's cash flows.</param>
    /// <exception cref="ArgumentException">A leg is <c>default(ImmutableArray)</c> or holds a null cash
    /// flow.</exception>
    public ConstNotionalCrossCurrencySwap(
        ImmutableArray<CashFlow> firstLeg,
        Currency? firstLegCcy,
        ImmutableArray<CashFlow> secondLeg,
        Currency? secondLegCcy)
        : base(firstLeg, firstLegCcy, secondLeg, secondLegCcy)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstNotionalCrossCurrencySwap"/> class with any number of legs,
    /// each with its direction and currency.
    /// </summary>
    /// <param name="legs">The legs.</param>
    /// <param name="payer">For each leg, whether it is paid.</param>
    /// <param name="currencies">For each leg, the currency of its cash flows.</param>
    /// <exception cref="ArgumentException">The three sequences differ in size.</exception>
    public ConstNotionalCrossCurrencySwap(
        IEnumerable<ImmutableArray<CashFlow>> legs,
        IEnumerable<bool> payer,
        IEnumerable<Currency?> currencies)
        : base(legs, payer, currencies)
    {
    }
}
