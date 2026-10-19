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
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.PricingEngines;

/// <summary>The normal formula: QuantLib's <c>detail::BachelierSpec</c>, which ignores the displacement.</summary>
internal readonly struct BachelierSpec : IBlackStyleSwaptionSpec
{
    /// <inheritdoc/>
    public static VolatilityType Type => VolatilityType.Normal;

    /// <inheritdoc/>
    public static double Value(
        OptionType type, double strike, double atmForward, double stdDev, double annuity, double displacement) =>
        BachelierBlackFormula(type, strike, atmForward, stdDev, annuity);

    /// <inheritdoc/>
    public static double Vega(
        double strike, double atmForward, double stdDev, double exerciseTime, double annuity, double displacement) =>
        Math.Sqrt(exerciseTime) * BachelierBlackFormulaStdDevDerivative(strike, atmForward, stdDev, annuity);

    /// <inheritdoc/>
    public static double Delta(
        OptionType type, double strike, double atmForward, double stdDev, double annuity, double displacement) =>
        BachelierBlackFormulaForwardDerivative(type, strike, atmForward, stdDev, annuity);
}
