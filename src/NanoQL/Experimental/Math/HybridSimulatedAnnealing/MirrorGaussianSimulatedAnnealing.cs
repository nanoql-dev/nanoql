// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealing.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The hybrid simulated annealing on the mirrored Gaussian sampler, the Boltzmann downhill probability, the exponential temperature and no
/// reannealing: QuantLib's <c>MirrorGaussianSimulatedAnnealing</c> typedef.
/// </summary>
[Experimental("NQL0001")]
public sealed class MirrorGaussianSimulatedAnnealing
    : HybridSimulatedAnnealing<SamplerMirrorGaussian, ProbabilityBoltzmannDownhill, TemperatureExponential, ReannealingTrivial>
{
    /// <summary>Initializes a new instance of the <see cref="MirrorGaussianSimulatedAnnealing"/> class.</summary>
    /// <param name="sampler">The sampler, which it copies.</param>
    /// <param name="probability">The probability, which it copies.</param>
    /// <param name="temperature">The temperature schedule, which it copies.</param>
    /// <param name="reannealing">The reannealing; <see langword="null"/> for QuantLib's default, a new one.</param>
    /// <param name="startTemperature">Each dimension's starting temperature.</param>
    /// <param name="endTemperature">The temperature every dimension must fall below to stop.</param>
    /// <param name="reAnnealSteps">The iterations between reannealings; 0 for never.</param>
    /// <param name="resetScheme">Where the current point resets.</param>
    /// <param name="resetSteps">The iterations between resets; 0 for never.</param>
    /// <param name="localOptimizer">The local optimizer; <see langword="null"/> for QuantLib's default, a <see cref="LevenbergMarquardt"/>.</param>
    /// <param name="optimizeScheme">When the local optimizer runs.</param>
    public MirrorGaussianSimulatedAnnealing(
        SamplerMirrorGaussian sampler,
        ProbabilityBoltzmannDownhill probability,
        TemperatureExponential temperature,
        ReannealingTrivial? reannealing = null,
        double startTemperature = 200.0,
        double endTemperature = 0.01,
        int reAnnealSteps = 50,
        HybridSimulatedAnnealingResetScheme resetScheme = HybridSimulatedAnnealingResetScheme.ResetToBestPoint,
        int resetSteps = 150,
        OptimizationMethod? localOptimizer = null,
        HybridSimulatedAnnealingLocalOptimizeScheme optimizeScheme = HybridSimulatedAnnealingLocalOptimizeScheme.EveryBestPoint)
        : base(
            sampler,
            probability,
            temperature,
            reannealing ?? new ReannealingTrivial(),
            startTemperature,
            endTemperature,
            reAnnealSteps,
            resetScheme,
            resetSteps,
            localOptimizer,
            optimizeScheme)
    {
    }
}
