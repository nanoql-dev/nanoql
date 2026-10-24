// SPDX-FileCopyrightText: Copyright (C) 2022 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/qdplusamericanengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>The QD+ engine's root finder for the exercise boundary: QuantLib's <c>QdPlusAmericanEngine::SolverType</c>.</summary>
public enum QdPlusAmericanEngineSolverType
{
    /// <summary>Brent's method.</summary>
    Brent,

    /// <summary>Newton's method.</summary>
    Newton,

    /// <summary>Ridder's method.</summary>
    Ridder,

    /// <summary>The engine's own Halley iteration, Brent's method where it doesn't converge.</summary>
    Halley,

    /// <summary>The engine's own super-Halley iteration, Brent's method where it doesn't converge.</summary>
    SuperHalley,
}
