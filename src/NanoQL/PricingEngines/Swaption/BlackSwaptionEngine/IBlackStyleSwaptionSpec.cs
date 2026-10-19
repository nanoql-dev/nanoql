// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015, 2016, 2017 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Paul Giltinan
// SPDX-FileCopyrightText: Copyright (C) 2017 Werner Kuerzinger
// SPDX-FileCopyrightText: Copyright (C) 2020 Marcin Rybacki
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/blackswaptionengine.hpp ql/pricingengines/swaption/blackswaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The formula a Black-style swaption engine prices with: the concept of QuantLib's <c>Spec</c> template parameter of
/// <c>detail::BlackStyleSwaptionEngine</c>, a stateless traits struct, so its members are static (playbook §2).
/// </summary>
internal interface IBlackStyleSwaptionSpec
{
    /// <summary>Gets the type of the volatilities the formula takes.</summary>
    static abstract VolatilityType Type { get; }

    /// <summary>The swaption's value.</summary>
    /// <param name="type">Call for a payer swaption, put for a receiver.</param>
    /// <param name="strike">The strike, corrected for the floating leg's spread.</param>
    /// <param name="atmForward">The forward swap rate, corrected likewise.</param>
    /// <param name="stdDev">The standard deviation: the volatility times the square root of the time to expiry.</param>
    /// <param name="annuity">The annuity, the formula's discount.</param>
    /// <param name="displacement">The shift of a shifted lognormal volatility.</param>
    /// <returns>The value.</returns>
    static abstract double Value(
        OptionType type, double strike, double atmForward, double stdDev, double annuity, double displacement);

    /// <summary>The value's derivative with respect to the volatility.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="atmForward">The forward swap rate.</param>
    /// <param name="stdDev">The standard deviation.</param>
    /// <param name="exerciseTime">The time to expiry.</param>
    /// <param name="annuity">The annuity.</param>
    /// <param name="displacement">The shift of a shifted lognormal volatility.</param>
    /// <returns>The vega.</returns>
    static abstract double Vega(
        double strike, double atmForward, double stdDev, double exerciseTime, double annuity, double displacement);

    /// <summary>The value's derivative with respect to the forward swap rate.</summary>
    /// <param name="type">Call for a payer swaption, put for a receiver.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="atmForward">The forward swap rate.</param>
    /// <param name="stdDev">The standard deviation.</param>
    /// <param name="annuity">The annuity.</param>
    /// <param name="displacement">The shift of a shifted lognormal volatility.</param>
    /// <returns>The delta.</returns>
    static abstract double Delta(
        OptionType type, double strike, double atmForward, double stdDev, double annuity, double displacement);
}
