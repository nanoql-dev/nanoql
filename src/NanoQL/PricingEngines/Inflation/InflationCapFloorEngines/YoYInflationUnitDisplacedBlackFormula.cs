// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/inflation/inflationcapfloorengines.hpp ql/pricingengines/inflation/inflationcapfloorengines.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Black formula with the strike and the forward displaced by 1: QuantLib's
/// <c>YoYInflationUnitDisplacedBlackCapFloorEngine::optionletImpl</c>.
/// </summary>
internal readonly struct YoYInflationUnitDisplacedBlackFormula : IYoYInflationOptionletFormula
{
    /// <inheritdoc/>
    public static double OptionletImpl(OptionType type, double strike, double forward, double stdDev, double d) =>
        // could use displacement parameter in blackFormula but this is clearer
        BlackFormula(type, strike + 1.0, forward + 1.0, stdDev, d);
}
