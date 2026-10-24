// SPDX-FileCopyrightText: Copyright (C) 2022 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/qdfpamericanengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>
/// The fixed-point equation of the QD fixed-point engine (L. Andersen, M. Lake and D. Offengenden, "High Performance
/// American Option Pricing", 2015): QuantLib's <c>QdFpAmericanEngine::FixedPointEquation</c>.
/// </summary>
public enum QdFpAmericanEngineFixedPointEquation
{
    /// <summary>Andersen, Lake and Offengenden's equation A (QuantLib's <c>FP_A</c>).</summary>
    FpA,

    /// <summary>Their equation B (QuantLib's <c>FP_B</c>).</summary>
    FpB,

    /// <summary>Equation A where |r − q| &lt; 0.001, B otherwise.</summary>
    Auto,
}
