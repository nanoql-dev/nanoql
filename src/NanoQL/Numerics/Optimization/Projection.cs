// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/projection.hpp ql/math/optimization/projection.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// A parameter projection: a set of parameters, some of them fixed, mapped to its free parameters and back.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: it copies the values and the flags it is given. QuantLib's protected <c>mapFreeParameters</c> and its
/// <c>mutable</c> copy of the parameters, which only a class deriving from both <see cref="CostFunction"/> and
/// <c>Projection</c> reads, aren't ported: a class with one base composes a projection and calls <see cref="Include"/>,
/// which computes the same values.
/// </para>
/// <para>
/// An extension point, as in QuantLib: <see cref="Project"/> and <see cref="Include"/> are virtual, and the fixed
/// values, the flags and the number of free parameters are protected.
/// </para>
/// </remarks>
public class Projection
{
    /// <summary>Initializes a new instance of the <see cref="Projection"/> class.</summary>
    /// <param name="parameterValues">The values of all the parameters; the fixed ones keep these values.</param>
    /// <param name="fixParameters">Whether each parameter is fixed; empty, the default, for all free.</param>
    /// <exception cref="ArgumentException">
    /// The flags aren't one per parameter, or no parameter is free.
    /// </exception>
    public Projection(ReadOnlySpan<double> parameterValues, ReadOnlySpan<bool> fixParameters = default)
    {
        FixedParameters = [.. parameterValues];
        FixParameters = fixParameters.IsEmpty ? ImmutableArray.Create(new bool[parameterValues.Length]) : [.. fixParameters];

        if (!(FixedParameters.Length == FixParameters.Length))
        {
            throw new ArgumentException("fixedParameters_.size()!=parametersFreedoms_.size()", nameof(fixParameters));
        }

        foreach (bool fixParameter in FixParameters)
        {
            if (!fixParameter)
            {
                NumberOfFreeParameters++;
            }
        }

        if (!(NumberOfFreeParameters > 0))
        {
            throw new ArgumentException(
                "numberOfFreeParameters==0", fixParameters.IsEmpty ? nameof(parameterValues) : nameof(fixParameters));
        }
    }

    // QuantLib's implicit copy, which ProjectedConstraint takes: the state alone, as C++ copies a Projection.
    internal Projection(Projection other)
    {
        ArgumentNullException.ThrowIfNull(other);
        FixedParameters = other.FixedParameters;
        FixParameters = other.FixParameters;
        NumberOfFreeParameters = other.NumberOfFreeParameters;
    }

    /// <summary>Gets the number of free parameters.</summary>
    protected int NumberOfFreeParameters { get; }

    /// <summary>Gets the values of all the parameters, as given; the fixed ones keep these values.</summary>
    protected ImmutableArray<double> FixedParameters { get; }

    /// <summary>Gets whether each parameter is fixed.</summary>
    protected ImmutableArray<bool> FixParameters { get; }

    /// <summary>The subset of free parameters of a set of parameters.</summary>
    /// <param name="parameters">All the parameters.</param>
    /// <returns>A new vector of the free ones, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="parameters"/> isn't one value per parameter.</exception>
    public virtual Vector Project(Vector parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (!(parameters.Size == FixParameters.Length))
        {
            throw new ArgumentException("parameters.size()!=parametersFreedoms_.size()", nameof(parameters));
        }

        var projectedParameters = new Vector(NumberOfFreeParameters);
        int i = 0;
        for (int j = 0; j < FixParameters.Length; j++)
        {
            if (!FixParameters[j])
            {
                projectedParameters[i++] = parameters[j];
            }
        }

        return projectedParameters;
    }

    /// <summary>The whole set of parameters for a set of free ones: the fixed values with the free ones replaced.</summary>
    /// <param name="projectedParameters">The free parameters, in order.</param>
    /// <returns>A new vector of all the parameters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="projectedParameters"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="projectedParameters"/> isn't one value per free parameter.
    /// </exception>
    public virtual Vector Include(Vector projectedParameters)
    {
        ArgumentNullException.ThrowIfNull(projectedParameters);
        if (!(projectedParameters.Size == NumberOfFreeParameters))
        {
            throw new ArgumentException(
                "projectedParameters.size()!=numberOfFreeParameters", nameof(projectedParameters));
        }

        var y = new Vector(FixedParameters.AsSpan());
        int i = 0;
        for (int j = 0; j < y.Size; j++)
        {
            if (!FixParameters[j])
            {
                y[j] = projectedParameters[i++];
            }
        }

        return y;
    }
}
