// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/particleswarmoptimization.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The topology that sets each particle's social best from the swarm's personal bests each iteration: QuantLib's
/// <c>ParticleSwarmOptimization::Topology</c>.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib. The optimizer copies its topology (<see cref="Clone"/>) for each minimization,
/// so each starts from its seeds, where QuantLib's continues from the last; it calls <see cref="SetSize"/> and binds the
/// copy to that minimization's swarm, which the protected members read and change. A topology sets
/// <see cref="GlobalBestX"/>'s positions in place.
/// </remarks>
[Experimental("NQL0001")]
public abstract class ParticleSwarmOptimizationTopology
{
    private ParticleSwarmOptimization.Swarm? swarm;

    /// <summary>Initializes a new instance of the <see cref="ParticleSwarmOptimizationTopology"/> class.</summary>
    protected ParticleSwarmOptimizationTopology()
    {
    }

    /// <summary>Gets each particle's position, QuantLib's <c>X_</c>.</summary>
    protected IReadOnlyList<Vector> X => Bound.X;

    /// <summary>Gets each particle's velocity, QuantLib's <c>V_</c>.</summary>
    protected IReadOnlyList<Vector> V => Bound.V;

    /// <summary>Gets each particle's personal best position, QuantLib's <c>pBX_</c>.</summary>
    protected IReadOnlyList<Vector> PersonalBestX => Bound.PersonalBestX;

    /// <summary>Gets each particle's social best position, which the topology sets, QuantLib's <c>gBX_</c>.</summary>
    protected IReadOnlyList<Vector> GlobalBestX => Bound.GlobalBestX;

    /// <summary>Gets each particle's personal best value, QuantLib's <c>pBF_</c>.</summary>
    protected Vector PersonalBestF => Bound.PersonalBestF;

    /// <summary>Gets each particle's social best value, which the topology sets, QuantLib's <c>gBF_</c>.</summary>
    protected Vector GlobalBestF => Bound.GlobalBestF;

    private ParticleSwarmOptimization.Swarm Bound =>
        swarm ?? throw new NanoQLException("the topology isn't bound to a minimization's swarm");

    /// <summary>Initializes the state for the problem (QuantLib's <c>setSize</c>).</summary>
    /// <param name="M">The swarm's size.</param>
    public abstract void SetSize(int M);

    /// <summary>Sets each particle's social best for the iteration (QuantLib's <c>findSocialBest</c>).</summary>
    public abstract void FindSocialBest();

    /// <summary>A copy for a minimization, unbound, its generators at their seeds.</summary>
    /// <returns>The copy.</returns>
    public abstract ParticleSwarmOptimizationTopology Clone();

    /// <summary>Copies a personal best into a particle's social best, in place.</summary>
    /// <param name="particle">The particle whose social best it sets.</param>
    /// <param name="from">The personal best's particle.</param>
    /// <param name="value">The social best's value.</param>
    protected void SetSocialBest(int particle, int from, double value)
    {
        PersonalBestX[from].AsSpan().CopyTo(GlobalBestX[particle].AsSpan());
        GlobalBestF[particle] = value;
    }

    // QuantLib's init(pso): binds the copy to a minimization's swarm.
    internal void Bind(ParticleSwarmOptimization.Swarm s) => swarm = s;
}
