// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/gaussian1dfloatfloatswaptionengine.hpp ql/pricingengines/swaption/gaussian1dfloatfloatswaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>
/// Which exercise probabilities the Gaussian1d float-float swaption engine computes: QuantLib's
/// <c>Gaussian1dFloatFloatSwaptionEngine::Probabilities</c>.
/// </summary>
public enum Gaussian1dFloatFloatSwaptionEngineProbabilities
{
    /// <summary>None.</summary>
    None,

    /// <summary>Naive probabilities: each exercise's indicator rolled back undiscounted.</summary>
    Naive,

    /// <summary>The digitals' probabilities: each exercise's indicator deflated by the discount to its date.</summary>
    Digital,
}
