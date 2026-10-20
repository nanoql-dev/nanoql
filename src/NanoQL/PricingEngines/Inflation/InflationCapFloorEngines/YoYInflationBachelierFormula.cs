// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/inflation/inflationcapfloorengines.hpp ql/pricingengines/inflation/inflationcapfloorengines.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.PricingEngines;

/// <summary>The Bachelier formula: QuantLib's <c>YoYInflationBachelierCapFloorEngine::optionletImpl</c>.</summary>
internal readonly struct YoYInflationBachelierFormula : IYoYInflationOptionletFormula
{
    /// <inheritdoc/>
    public static double OptionletImpl(OptionType type, double strike, double forward, double stdDev, double d) =>
        BachelierBlackFormula(type, strike, forward, stdDev, d);
}
