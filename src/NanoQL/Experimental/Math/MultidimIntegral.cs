// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/multidimintegrator.hpp ql/experimental/math/multidimintegrator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// Integrates a scalar function of a vector over a box, one one-dimensional integrator per dimension, the last
/// dimension outermost: it generalizes <see cref="TwoDimensionalIntegral"/> to any number of dimensions up to 15.
/// </summary>
/// <remarks>
/// Immutable; QuantLib's integration-variable buffer is a per-call buffer. An integration of no dimension is
/// refused: QuantLib's entry point reads <c>integrationLevelEntries_[size - 1]</c> of an empty set
/// (<c>multidimintegrator.hpp:55</c>), as one of more than 15 is, QuantLib's own check.
/// </remarks>
[Experimental("NQL0001")]
public sealed class MultidimIntegral
{
    private const int MaxDimensions = 15;

    private readonly ImmutableArray<Integrator> integrators;

    /// <summary>Initializes a new instance of the <see cref="MultidimIntegral"/> class.</summary>
    /// <param name="integrators">One integrator per dimension, the first dimension's first.</param>
    /// <exception cref="ArgumentNullException"><paramref name="integrators"/> or an element is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There's no integrator, or more than 15 ("Too many dimensions in
    /// integration.").</exception>
    public MultidimIntegral(IEnumerable<Integrator> integrators)
    {
        ArgumentNullException.ThrowIfNull(integrators);
        ImmutableArray<Integrator> i = [.. integrators];
        foreach (Integrator x in i)
        {
            ArgumentNullException.ThrowIfNull(x, nameof(integrators));
        }

        if (i.IsEmpty)
        {
            throw new ArgumentException("an integration needs at least one dimension", nameof(integrators));
        }

        if (i.Length > MaxDimensions)
        {
            throw new ArgumentException("Too many dimensions in integration.", nameof(integrators));
        }

        this.integrators = i;
    }

    /// <summary>The integral of a function over the box [a, b].</summary>
    /// <param name="f">The integrand.</param>
    /// <param name="a">The lower limit of each dimension.</param>
    /// <param name="b">The upper limit of each dimension.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The limits' sizes aren't the dimension ("Incompatible integration problem
    /// dimensions").</exception>
    public double Integrate(MultidimIntegrand f, ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        ArgumentNullException.ThrowIfNull(f);
        if (a.Length != b.Length || b.Length != integrators.Length)
        {
            throw new ArgumentException("Incompatible integration problem dimensions", nameof(a));
        }

        double[] lower = a.ToArray(), upper = b.ToArray();
        var varBuffer = new double[integrators.Length];
        return Level(integrators.Length - 1, f, lower, upper, varBuffer);
    }

    // actual integration of dimension nT, its cross-sections binding the dimension's value
    private double Level(int nT, MultidimIntegrand f, double[] a, double[] b, double[] varBuffer) =>
        integrators[nT].Integrate(
            z =>
            {
                varBuffer[nT] = z;
                return nT == 0 ? f(varBuffer) : Level(nT - 1, f, a, b, varBuffer);
            },
            a[nT],
            b[nT]);
}
