// SPDX-FileCopyrightText: Copyright (C) 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2016 Stefano Fondi
// SPDX-FileCopyrightText: Copyright (C) 2017 Joseph Jeisman
// SPDX-FileCopyrightText: Copyright (C) 2017 Fabrice Lecuyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/overnightindexedcouponpricer.hpp ql/cashflows/overnightindexedcouponpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;

namespace NanoQL.Coupons;

/// <summary>
/// <see cref="OvernightIndexedCouponPricer"/>'s modifiers, which return a modified copy of the receiver, keeping its
/// static type, where QuantLib's change the pricer.
/// </summary>
public static class OvernightIndexedCouponPricerExtensions
{
    /// <summary>A copy of the pricer holding another caplet volatility: QuantLib's <c>setCapletVolatility</c>.</summary>
    /// <typeparam name="T">The pricer's type.</typeparam>
    /// <param name="pricer">The pricer.</param>
    /// <param name="v">The optionlet volatility, or <see langword="null"/> for none.</param>
    /// <returns>The modified copy; the receiver is unchanged.</returns>
    public static T SetCapletVolatility<T>(this T pricer, OptionletVolatilityStructure? v = null)
        where T : OvernightIndexedCouponPricer
    {
        ArgumentNullException.ThrowIfNull(pricer);
        return (T)pricer.WithCapletVolatility(v);
    }

    /// <summary>
    /// A copy of the pricer holding another effective-volatility flag: QuantLib's <c>setEffectiveVolatilityInput</c>.
    /// </summary>
    /// <typeparam name="T">The pricer's type.</typeparam>
    /// <param name="pricer">The pricer.</param>
    /// <param name="effectiveVolatilityInput">Whether the volatility is the effective volatility of the period's rate.</param>
    /// <returns>The modified copy; the receiver is unchanged.</returns>
    public static T SetEffectiveVolatilityInput<T>(this T pricer, bool effectiveVolatilityInput)
        where T : OvernightIndexedCouponPricer
    {
        ArgumentNullException.ThrowIfNull(pricer);
        return (T)pricer.WithEffectiveVolatilityInput(effectiveVolatilityInput);
    }
}
