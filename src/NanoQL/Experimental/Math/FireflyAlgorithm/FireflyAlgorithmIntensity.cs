// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/fireflyalgorithm.hpp ql/experimental/math/fireflyalgorithm.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The intensity with which fireflies attract each other, which moves each toward the brighter ones: QuantLib's
/// <c>FireflyAlgorithm::Intensity</c>. A derived class implements <see cref="IntensityImpl"/> only.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib. The algorithm copies its intensity (<see cref="Clone"/>) for each minimization
/// and binds the copy to that minimization's population, which the protected members read; a minimization's copy is
/// its working state.
/// </para>
/// <para>
/// <see cref="FindBrightest"/> attracts each firefly to every brighter one, k &lt; i in the sorted population, where
/// QuantLib's loop stops at k &lt; i − 1, skipping the next brighter one and leaving the second brightest unattracted
/// (<c>Divergences/FireflyAlgorithmTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class FireflyAlgorithmIntensity
{
    private FireflyAlgorithm.Population? population;

    /// <summary>Initializes a new instance of the <see cref="FireflyAlgorithmIntensity"/> class.</summary>
    protected FireflyAlgorithmIntensity()
    {
    }

    /// <summary>Gets the number of fireflies the algorithm moves, QuantLib's <c>Mfa_</c>.</summary>
    protected int Mfa => Bound.Mfa;

    /// <summary>Gets the dimension, QuantLib's <c>N_</c>.</summary>
    protected int N => Bound.N;

    /// <summary>Gets each firefly's position, by its index, QuantLib's <c>x_</c>.</summary>
    protected IReadOnlyList<Vector> X => Bound.X;

    /// <summary>
    /// Gets the population's values and indexes, sorted brightest (lowest value) first after each sort, QuantLib's
    /// <c>values_</c>.
    /// </summary>
    protected IReadOnlyList<(double Value, int Index)> Values => Bound.Values;

    /// <summary>Gets each firefly's intensity move, by its index, which <see cref="FindBrightest"/> sets, QuantLib's <c>xI_</c>.</summary>
    protected IReadOnlyList<Vector> XI => Bound.XI;

    private FireflyAlgorithm.Population Bound =>
        population ?? throw new NanoQLException("the intensity isn't bound to a minimization's population");

    /// <summary>
    /// Sets each firefly's intensity move: the brightest none, every other the sum of each brighter firefly's
    /// intensity times its displacement (QuantLib's <c>findBrightest</c>).
    /// </summary>
    public void FindBrightest()
    {
        // the brightest ignores all others
        Vector xI0 = XI[Values[0].Index];
        for (int j = 0; j < N; j++)
        {
            xI0[j] = 0.0;
        }

        for (int i = 1; i < Mfa; i++)
        {
            // values are already sorted
            int index = Values[i].Index;
            Vector x = X[index];
            Vector xI = XI[index];
            for (int j = 0; j < N; j++)
            {
                xI[j] = 0.0;
            }

            double valueX = Values[i].Value;
            for (int k = 0; k < i; k++)
            {
                Vector y = X[Values[k].Index];
                double valueY = Values[k].Value;
                double intensity = IntensityImpl(valueX, valueY, Distance(x, y));
                xI += intensity * (y - x);
            }
        }
    }

    /// <summary>A copy for a minimization, unbound.</summary>
    /// <returns>The copy.</returns>
    public abstract FireflyAlgorithmIntensity Clone();

    /// <summary>The intensity between two fireflies.</summary>
    /// <param name="valueX">The attracted firefly's value.</param>
    /// <param name="valueY">The brighter firefly's value.</param>
    /// <param name="distance">The squared distance between them, <see cref="Distance"/>'s.</param>
    /// <returns>The intensity.</returns>
    protected abstract double IntensityImpl(double valueX, double valueY, double distance);

    /// <summary>The squared Euclidean distance between two positions, QuantLib's <c>distance</c>.</summary>
    /// <param name="x">One position.</param>
    /// <param name="y">The other.</param>
    /// <returns>Σ (xᵢ − yᵢ)².</returns>
    protected double Distance(Vector x, Vector y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        double d = 0.0;
        for (int i = 0; i < N; i++)
        {
            double diff = x[i] - y[i];
            d += diff * diff;
        }

        return d;
    }

    // QuantLib's init(fa): binds the copy to a minimization's population.
    internal void Init(FireflyAlgorithm.Population p) => population = p;
}
