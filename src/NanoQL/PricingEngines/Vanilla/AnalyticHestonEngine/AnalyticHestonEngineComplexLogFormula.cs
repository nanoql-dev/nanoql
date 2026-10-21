// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analytichestonengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>
/// The formulations of the Heston characteristic function's complex logarithm, and the control variates the integral
/// runs with: QuantLib's <c>AnalyticHestonEngine::ComplexLogFormula</c>.
/// </summary>
public enum AnalyticHestonEngineComplexLogFormula
{
    /// <summary>Gatheral's form of the characteristic function, without a control variate.</summary>
    Gatheral,

    /// <summary>Heston's original form with a branch correction of the logarithm, without a control variate.</summary>
    BranchCorrection,

    /// <summary>Gatheral's form with Andersen and Piterbarg's control variate.</summary>
    AndersenPiterbarg,

    /// <summary>Andersen and Piterbarg's form with a slightly better control variate.</summary>
    AndersenPiterbargOptCV,

    /// <summary>Gatheral's form with the characteristic function's asymptotic expansion as the control
    /// variate.</summary>
    AsymptoticChF,

    /// <summary>The integral on an angled contour, with a control variate.</summary>
    AngledContour,

    /// <summary>The integral on an angled contour, without a control variate.</summary>
    AngledContourNoCV,

    /// <summary>The best control variate of <see cref="AsymptoticChF"/> and <see cref="AngledContour"/>, chosen per
    /// option.</summary>
    OptimalCV,
}
