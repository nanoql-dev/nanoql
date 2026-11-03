// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/parametricexercise.hpp ql/methods/montecarlo/parametricexercise.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// The results of an early-exercise optimisation: the biased estimate of the value it obtains while optimising, and
/// each exercise's optimal parameters, which QuantLib writes into the caller's vector.
/// </summary>
/// <param name="BiasedEstimate">The value estimated on the optimisation's own paths, biased high.</param>
/// <param name="Parameters">Each exercise's parameters.</param>
public sealed record GenericEarlyExerciseOptimizationResults(
    double BiasedEstimate, ImmutableArray<ImmutableArray<double>> Parameters);
