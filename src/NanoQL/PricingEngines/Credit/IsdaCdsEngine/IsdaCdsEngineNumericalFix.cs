// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/credit/isdacdsengine.hpp ql/pricingengines/credit/isdacdsengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>
/// How the ISDA engine avoids a zero denominator <c>f + h</c> in its integrals: QuantLib's
/// <c>IsdaCdsEngine::NumericalFix</c>, with its values.
/// </summary>
public enum IsdaCdsEngineNumericalFix
{
    /// <summary>10<sup>-50</sup> added to the denominators, as in the OpenGamma paper's footnote 26.</summary>
    None = 0,

    /// <summary>A Taylor expansion where <c>f + h</c> is below 10<sup>-4</sup>, as in Markit's proposed numerical
    /// fix.</summary>
    Taylor = 1,
}
