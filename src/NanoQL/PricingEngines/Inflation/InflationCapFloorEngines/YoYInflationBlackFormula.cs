// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/inflation/inflationcapfloorengines.hpp ql/pricingengines/inflation/inflationcapfloorengines.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.PricingEngines;

/// <summary>The Black formula: QuantLib's <c>YoYInflationBlackCapFloorEngine::optionletImpl</c>.</summary>
internal readonly struct YoYInflationBlackFormula : IYoYInflationOptionletFormula
{
    /// <inheritdoc/>
    public static double OptionletImpl(OptionType type, double strike, double forward, double stdDev, double d) =>
        BlackFormula(type, strike, forward, stdDev, d);
}
