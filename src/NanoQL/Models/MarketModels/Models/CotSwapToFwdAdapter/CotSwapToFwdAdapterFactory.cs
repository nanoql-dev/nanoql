// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/cotswaptofwdadapter.hpp ql/models/marketmodels/models/cotswaptofwdadapter.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// The factory of <see cref="CotSwapToFwdAdapter"/>s, each on a coterminal swap-rate model its coterminal factory
/// creates.
/// </summary>
/// <remarks>Immutable (the S-mm plan's MR1 (i)).</remarks>
public sealed class CotSwapToFwdAdapterFactory : MarketModelFactory
{
    private readonly MarketModelFactory coterminalFactory;

    /// <summary>Initializes a new instance of the <see cref="CotSwapToFwdAdapterFactory"/> class.</summary>
    /// <param name="coterminalFactory">The factory of the coterminal swap-rate models.</param>
    public CotSwapToFwdAdapterFactory(MarketModelFactory coterminalFactory)
    {
        ArgumentNullException.ThrowIfNull(coterminalFactory);
        this.coterminalFactory = coterminalFactory;
    }

    /// <inheritdoc/>
    public override MarketModel Create(EvolutionDescription evolution, int numberOfFactors)
    {
        MarketModel coterminalModel = coterminalFactory.Create(evolution, numberOfFactors);
        return new CotSwapToFwdAdapter(coterminalModel);
    }
}
