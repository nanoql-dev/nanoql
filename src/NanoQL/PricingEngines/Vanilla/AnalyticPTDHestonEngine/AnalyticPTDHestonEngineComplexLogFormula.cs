// SPDX-FileCopyrightText: Copyright (C) 2010, 2017 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticptdhestonengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>The formulations <see cref="AnalyticPTDHestonEngine"/> integrates by.</summary>
public enum AnalyticPTDHestonEngineComplexLogFormula
{
    /// <summary>Gatheral's form of the characteristic function, the probabilities P1 and P2 integrated.</summary>
    Gatheral,

    /// <summary>Andersen and Piterbarg's form, with Black's price at the average variance as the control variate.</summary>
    AndersenPiterbarg,
}
