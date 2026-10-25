// SPDX-FileCopyrightText: Copyright (C) 2014 Fabien Le Floc'h
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/hestonexpansionengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>A Heston expansion formula for the implied volatility.</summary>
/// <remarks>
/// During a calibration an expansion is typically built once per expiry slice of the implied-volatility surface, then
/// asked for each strike of the slice. An extension point: QuantLib's three expansions derive from it.
/// </remarks>
public abstract class HestonExpansion
{
    /// <summary>The implied volatility at a strike.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <returns>The implied volatility.</returns>
    public abstract double ImpliedVolatility(double strike, double forward);
}
