// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/particleswarmoptimization.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The inertia that alters the swarm's velocities each iteration: QuantLib's <c>ParticleSwarmOptimization::Inertia</c>.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib. The optimizer copies its inertia (<see cref="Clone"/>) for each minimization,
/// so each starts from its seeds, where QuantLib's continues from the last; it calls <see cref="SetSize"/>, binds the copy
/// to that minimization's swarm, which the protected members read and change, and calls <see cref="Init"/>.
/// </remarks>
[Experimental("NQL0001")]
public abstract class ParticleSwarmOptimizationInertia
{
    private ParticleSwarmOptimization.Swarm? swarm;

    /// <summary>Initializes a new instance of the <see cref="ParticleSwarmOptimizationInertia"/> class.</summary>
    protected ParticleSwarmOptimizationInertia()
    {
    }

    /// <summary>Gets each particle's position, QuantLib's <c>X_</c>.</summary>
    protected IReadOnlyList<Vector> X => Bound.X;

    /// <summary>Gets each particle's velocity, which the inertia scales, QuantLib's <c>V_</c>.</summary>
    protected IReadOnlyList<Vector> V => Bound.V;

    /// <summary>Gets each particle's personal best position, QuantLib's <c>pBX_</c>.</summary>
    protected IReadOnlyList<Vector> PersonalBestX => Bound.PersonalBestX;

    /// <summary>Gets each particle's social best position, QuantLib's <c>gBX_</c>.</summary>
    protected IReadOnlyList<Vector> GlobalBestX => Bound.GlobalBestX;

    /// <summary>Gets each particle's personal best value, QuantLib's <c>pBF_</c>.</summary>
    protected Vector PersonalBestF => Bound.PersonalBestF;

    /// <summary>Gets each particle's social best value, QuantLib's <c>gBF_</c>.</summary>
    protected Vector GlobalBestF => Bound.GlobalBestF;

    /// <summary>Gets the problem's lower bounds, QuantLib's <c>lX_</c>.</summary>
    protected Vector LowerBound => Bound.LowerBound;

    /// <summary>Gets the problem's upper bounds, QuantLib's <c>uX_</c>.</summary>
    protected Vector UpperBound => Bound.UpperBound;

    private ParticleSwarmOptimization.Swarm Bound =>
        swarm ?? throw new NanoQLException("the inertia isn't bound to a minimization's swarm");

    /// <summary>Initializes the state for the problem (QuantLib's <c>setSize</c>).</summary>
    /// <param name="M">The swarm's size.</param>
    /// <param name="N">The dimension.</param>
    /// <param name="c0">The optimizer's inertia.</param>
    /// <param name="endCriteria">The minimization's end criteria.</param>
    public abstract void SetSize(int M, int N, double c0, EndCriteria endCriteria);

    /// <summary>Changes the swarm's velocities for the iteration (QuantLib's <c>setValues</c>).</summary>
    public abstract void SetValues();

    /// <summary>A copy for a minimization, unbound, its generators at their seeds.</summary>
    /// <returns>The copy.</returns>
    public abstract ParticleSwarmOptimizationInertia Clone();

    /// <summary>Prepares the inertia once it is bound to a swarm, QuantLib's <c>init</c>; nothing by default.</summary>
    protected virtual void Init()
    {
    }

    // QuantLib's init(pso): binds the copy to a minimization's swarm.
    internal void Bind(ParticleSwarmOptimization.Swarm s)
    {
        swarm = s;
        Init();
    }
}
