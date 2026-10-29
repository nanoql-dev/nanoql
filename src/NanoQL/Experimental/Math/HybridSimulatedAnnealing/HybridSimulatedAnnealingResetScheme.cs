// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealing.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>Where the hybrid simulated annealing resets its current point: QuantLib's <c>HybridSimulatedAnnealing::ResetScheme</c>.</summary>
[Experimental("NQL0001")]
public enum HybridSimulatedAnnealingResetScheme
{
    /// <summary>It doesn't.</summary>
    NoResetScheme,

    /// <summary>To the best point so far.</summary>
    ResetToBestPoint,

    /// <summary>To the starting point.</summary>
    ResetToOrigin,
}
