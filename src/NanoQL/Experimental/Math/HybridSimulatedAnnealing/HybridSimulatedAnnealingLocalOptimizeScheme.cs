// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealing.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// When the hybrid simulated annealing runs its local optimizer: QuantLib's
/// <c>HybridSimulatedAnnealing::LocalOptimizeScheme</c>.
/// </summary>
[Experimental("NQL0001")]
public enum HybridSimulatedAnnealingLocalOptimizeScheme
{
    /// <summary>Never.</summary>
    NoLocalOptimize,

    /// <summary>At every accepted point.</summary>
    EveryNewPoint,

    /// <summary>At every new best point.</summary>
    EveryBestPoint,
}
