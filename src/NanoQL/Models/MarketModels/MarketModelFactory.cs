// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/marketmodel.hpp ql/models/marketmodels/marketmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>A factory of <see cref="MarketModel"/>s on given evolutions.</summary>
/// <remarks>
/// Immutable (the S-mm plan's MR1 (i)): <see cref="Create"/> returns a new model at each call. QuantLib's factories are
/// observables, so a model is rebuilt when their market changes; here a changed market is a new factory. An extension
/// point, as in QuantLib.
/// </remarks>
public abstract class MarketModelFactory
{
    /// <summary>Initializes a new instance of the <see cref="MarketModelFactory"/> class.</summary>
    protected MarketModelFactory()
    {
    }

    /// <summary>A new model on an evolution.</summary>
    /// <param name="evolution">The evolution.</param>
    /// <param name="numberOfFactors">The number of factors.</param>
    /// <returns>A new model.</returns>
    public abstract MarketModel Create(EvolutionDescription evolution, int numberOfFactors);
}
