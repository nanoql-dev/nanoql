// SPDX-FileCopyrightText: Copyright (C) 2014 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/discretizedbarrieroption.hpp ql/pricingengines/barrier/discretizedbarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;

namespace NanoQL.PricingEngines;

/// <summary>
/// A discretized barrier asset a binomial barrier engine builds: the constructor QuantLib's engine template calls,
/// <c>D(arguments, process, grid)</c> (<c>binomialbarrierengine.hpp:161</c>), as a static factory (the S-bar plan's
/// BR2 (iv), S2R7's form).
/// </summary>
/// <typeparam name="TSelf">The asset's own type.</typeparam>
public interface IDiscretizedBarrierAsset<TSelf>
    where TSelf : DiscretizedAsset, IDiscretizedBarrierAsset<TSelf>
{
    /// <summary>Builds the asset of a barrier option.</summary>
    /// <param name="option">The barrier option.</param>
    /// <param name="process">The process, which converts the exercise dates to times.</param>
    /// <param name="grid">The time grid the exercise times are moved to the closest of, or <see langword="null"/>
    /// for none, QuantLib's empty grid.</param>
    /// <returns>The asset.</returns>
    static abstract TSelf Create(BarrierOption option, StochasticProcess process, TimeGrid? grid);
}
