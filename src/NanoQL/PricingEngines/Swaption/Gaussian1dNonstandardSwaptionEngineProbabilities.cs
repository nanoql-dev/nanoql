// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/gaussian1dnonstandardswaptionengine.hpp ql/pricingengines/swaption/gaussian1dnonstandardswaptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>
/// Which exercise probabilities the Gaussian1d nonstandard swaption engine computes: QuantLib's
/// <c>Gaussian1dNonstandardSwaptionEngine::Probabilities</c>.
/// </summary>
public enum Gaussian1dNonstandardSwaptionEngineProbabilities
{
    /// <summary>None.</summary>
    None,

    /// <summary>Naive probabilities: each exercise's indicator rolled back undiscounted.</summary>
    Naive,

    /// <summary>The digitals' probabilities: each exercise's indicator deflated by the discount to its date.</summary>
    Digital,
}
