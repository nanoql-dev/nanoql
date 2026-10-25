// SPDX-FileCopyrightText: Copyright (C) 2014 Fabien Le Floc'h
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/hestonexpansionengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.PricingEngines;

/// <summary>The expansion <see cref="HestonExpansionEngine"/> prices by.</summary>
public enum HestonExpansionEngineHestonExpansionFormula
{
    /// <summary>Lorig, Pagliarani and Pascucci's expansion of order 2.</summary>
    LPP2,

    /// <summary>Lorig, Pagliarani and Pascucci's expansion of order 3.</summary>
    LPP3,

    /// <summary>Forde, Jacquier and Lee's small-time expansion.</summary>
    Forde,
}
