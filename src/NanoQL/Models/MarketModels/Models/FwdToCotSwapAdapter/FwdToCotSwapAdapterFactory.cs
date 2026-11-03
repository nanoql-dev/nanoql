// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/fwdtocotswapadapter.hpp ql/models/marketmodels/models/fwdtocotswapadapter.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// The factory of <see cref="FwdToCotSwapAdapter"/>s, each on a forward-rate model its forward factory creates.
/// </summary>
/// <remarks>Immutable.</remarks>
public sealed class FwdToCotSwapAdapterFactory : MarketModelFactory
{
    private readonly MarketModelFactory forwardFactory;

    /// <summary>Initializes a new instance of the <see cref="FwdToCotSwapAdapterFactory"/> class.</summary>
    /// <param name="forwardFactory">The factory of the forward-rate models.</param>
    public FwdToCotSwapAdapterFactory(MarketModelFactory forwardFactory)
    {
        ArgumentNullException.ThrowIfNull(forwardFactory);
        this.forwardFactory = forwardFactory;
    }

    /// <inheritdoc/>
    public override MarketModel Create(EvolutionDescription evolution, int numberOfFactors)
    {
        MarketModel forwardModel = forwardFactory.Create(evolution, numberOfFactors);
        return new FwdToCotSwapAdapter(forwardModel);
    }
}
