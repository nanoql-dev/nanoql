// SPDX-FileCopyrightText: Copyright (C) 2026 Colin Alberts
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/analyticroughhestonengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>The route <see cref="AnalyticRoughHestonEngine"/> solves the fractional Riccati equation by.</summary>
public enum AnalyticRoughHestonEngineApproximation
{
    /// <summary>Diethelm, Ford and Freed's fractional Adams predictor-corrector scheme, the reference route, O(N²).</summary>
    AdamsPredictorCorrector,

    /// <summary>Gatheral and Radoičić's (3, 3) rational approximation, a closed form.</summary>
    Pade,

    /// <summary>Abi Jaber's lifted approximation, the kernel a sum of exponentials, O(nN).</summary>
    Lifted,
}
