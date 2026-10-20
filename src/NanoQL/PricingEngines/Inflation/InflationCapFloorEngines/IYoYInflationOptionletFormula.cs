// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/inflation/inflationcapfloorengines.hpp ql/pricingengines/inflation/inflationcapfloorengines.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>
/// The formula a year-on-year inflation cap/floor engine prices each optionlet with: QuantLib's pure virtual
/// <c>YoYInflationCapFloorEngine::optionletImpl</c>, a stateless formula, so its member is static (playbook §2).
/// </summary>
internal interface IYoYInflationOptionletFormula
{
    /// <summary>An optionlet's value.</summary>
    /// <param name="type">Call for a caplet, put for a floorlet.</param>
    /// <param name="strike">The effective strike.</param>
    /// <param name="forward">The forward year-on-year rate.</param>
    /// <param name="stdDev">The standard deviation; 0 for an optionlet fixed at or before the volatility's base
    /// date.</param>
    /// <param name="d">The nominal times the gearing, the discount factor and the accrual time.</param>
    /// <returns>The value.</returns>
    static abstract double OptionletImpl(OptionType type, double strike, double forward, double stdDev, double d);
}
