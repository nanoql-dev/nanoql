// SPDX-FileCopyrightText: Copyright (C) 2012 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2012 Mateusz Kapturski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/differentialevolution.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The crossover types, QuantLib's <c>DifferentialEvolution::CrossoverType</c>.</summary>
public enum DifferentialEvolutionCrossoverType
{
    /// <summary>Each coordinate mutates with the crossover probability.</summary>
    Normal,

    /// <summary>The probability scaled so that at least one coordinate mutates on average.</summary>
    Binomial,

    /// <summary>The probability of an exponential crossover's run.</summary>
    Exponential,
}
