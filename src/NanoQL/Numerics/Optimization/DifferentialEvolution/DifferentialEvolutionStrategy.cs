// SPDX-FileCopyrightText: Copyright (C) 2012 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2012 Mateusz Kapturski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/differentialevolution.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The strategies for creating a mutant population, QuantLib's <c>DifferentialEvolution::Strategy</c>.</summary>
/// <remarks>
/// The names are taken from Price, K., Storn, R., 1997. Differential Evolution - A Simple and Efficient Heuristic for
/// Global Optimization over Continuous Spaces. Journal of Global Optimization, Kluwer Academic Publishers, 1997, Vol.
/// 11, pp. 341 - 359.
/// </remarks>
public enum DifferentialEvolutionStrategy
{
    /// <summary>A random member plus the weighted difference of two others.</summary>
    Rand1Standard,

    /// <summary>The best member plus a jittered weighted difference.</summary>
    BestMemberWithJitter,

    /// <summary>The current member moved toward the best one, plus a weighted difference.</summary>
    CurrentToBest2Diffs,

    /// <summary>A random member plus a difference weighted per coordinate with dither.</summary>
    Rand1DiffWithPerVectorDither,

    /// <summary>A random member plus a difference weighted with dither.</summary>
    Rand1DiffWithDither,

    /// <summary>Either the standard mutation or an optimal recombination, at random.</summary>
    EitherOrWithOptimalRecombination,

    /// <summary>Self-adapting weights, with a rotation of the best member.</summary>
    Rand1SelfadaptiveWithRotation,
}
