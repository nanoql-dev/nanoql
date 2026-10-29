// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/fireflyalgorithm.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The random walk that moves each firefly besides its attraction: QuantLib's <c>FireflyAlgorithm::RandomWalk</c>. A
/// derived class implements <see cref="WalkImpl"/> only.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib. The algorithm copies its walk (<see cref="Clone"/>) for each minimization, so
/// each minimization starts the walk's generators from their seeds, where QuantLib's continues them from the last; it
/// binds the copy to that minimization's population, which the protected members read, and calls <see cref="Init"/>.
/// </remarks>
[Experimental("NQL0001")]
public abstract class FireflyAlgorithmRandomWalk
{
    private FireflyAlgorithm.Population? population;

    /// <summary>Initializes a new instance of the <see cref="FireflyAlgorithmRandomWalk"/> class.</summary>
    protected FireflyAlgorithmRandomWalk()
    {
    }

    /// <summary>Gets the number of fireflies the algorithm moves, QuantLib's <c>Mfa_</c>.</summary>
    protected int Mfa => Bound.Mfa;

    /// <summary>Gets the dimension, QuantLib's <c>N_</c>.</summary>
    protected int N => Bound.N;

    /// <summary>Gets each firefly's position, by its index, QuantLib's <c>x_</c>.</summary>
    protected IReadOnlyList<Vector> X => Bound.X;

    /// <summary>Gets the population's values and indexes, sorted brightest first after each sort, QuantLib's <c>values_</c>.</summary>
    protected IReadOnlyList<(double Value, int Index)> Values => Bound.Values;

    /// <summary>Gets each firefly's walk, by its index, which <see cref="Walk"/> sets, QuantLib's <c>xRW_</c>.</summary>
    protected IReadOnlyList<Vector> XRW => Bound.XRW;

    /// <summary>Gets the problem's lower bounds, QuantLib's <c>lX_</c>.</summary>
    protected Vector LowerBound => Bound.LowerBound;

    /// <summary>Gets the problem's upper bounds, QuantLib's <c>uX_</c>.</summary>
    protected Vector UpperBound => Bound.UpperBound;

    private FireflyAlgorithm.Population Bound =>
        population ?? throw new NanoQLException("the random walk isn't bound to a minimization's population");

    /// <summary>Draws each moved firefly's walk, in their sorted order (QuantLib's <c>walk</c>).</summary>
    public void Walk()
    {
        for (int i = 0; i < Mfa; i++)
        {
            WalkImpl(XRW[Values[i].Index]);
        }
    }

    /// <summary>A copy for a minimization, unbound, its generators at their seeds.</summary>
    /// <returns>The copy.</returns>
    public abstract FireflyAlgorithmRandomWalk Clone();

    /// <summary>Draws one firefly's walk.</summary>
    /// <param name="xRW">The walk, which it sets.</param>
    protected abstract void WalkImpl(Vector xRW);

    /// <summary>Prepares the walk once it is bound to a population, QuantLib's <c>init</c>; nothing by default.</summary>
    protected virtual void Init()
    {
    }

    // QuantLib's init(fa): binds the copy to a minimization's population.
    internal void Bind(FireflyAlgorithm.Population p)
    {
        population = p;
        Init();
    }
}
