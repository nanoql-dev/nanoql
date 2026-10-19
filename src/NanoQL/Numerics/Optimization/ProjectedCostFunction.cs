// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/projectedcostfunction.hpp ql/math/optimization/projectedcostfunction.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A cost function of a subset of another's parameters, the others fixed: the cost function evaluated at the fixed
/// values with the free ones replaced.
/// </summary>
/// <remarks>
/// QuantLib's class derives from both <see cref="CostFunction"/> and <see cref="Projection"/>; this one composes a
/// projection, a copy of the state of the one it is given, so a projection's overrides of
/// <see cref="Projection.Project"/> and <see cref="Projection.Include"/> aren't the ones it calls, as QuantLib's copy of
/// its base doesn't call them either. It keeps no state between calls: QuantLib's mutable copy of the parameters
/// becomes the new vector <see cref="Projection.Include"/> returns.
/// </remarks>
public sealed class ProjectedCostFunction : CostFunction
{
    private readonly CostFunction costFunction;
    private readonly FreeParameters projection;

    /// <summary>Initializes a new instance of the <see cref="ProjectedCostFunction"/> class on a projection it builds.</summary>
    /// <param name="costFunction">The cost function of all the parameters.</param>
    /// <param name="parameterValues">The values of all the parameters; the fixed ones keep these values.</param>
    /// <param name="fixParameters">Whether each parameter is fixed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="costFunction"/> is null.</exception>
    /// <exception cref="ArgumentException">The projection refuses the values or the flags.</exception>
    public ProjectedCostFunction(
        CostFunction costFunction, ReadOnlySpan<double> parameterValues, ReadOnlySpan<bool> fixParameters)
    {
        ArgumentNullException.ThrowIfNull(costFunction);
        this.costFunction = costFunction;
        projection = new FreeParameters(parameterValues, fixParameters);
    }

    /// <summary>Initializes a new instance of the <see cref="ProjectedCostFunction"/> class on a copy of a projection.</summary>
    /// <param name="costFunction">The cost function of all the parameters.</param>
    /// <param name="projection">The projection, whose state is copied.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public ProjectedCostFunction(CostFunction costFunction, Projection projection)
    {
        ArgumentNullException.ThrowIfNull(costFunction);
        ArgumentNullException.ThrowIfNull(projection);
        this.costFunction = costFunction;
        this.projection = new FreeParameters(projection);
    }

    /// <summary>The subset of free parameters of a set of parameters.</summary>
    /// <param name="parameters">All the parameters.</param>
    /// <returns>A new vector of the free ones, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="parameters"/> isn't one value per parameter.</exception>
    public Vector Project(Vector parameters) => projection.Project(parameters);

    /// <summary>The whole set of parameters for a set of free ones: the fixed values with the free ones replaced.</summary>
    /// <param name="projectedParameters">The free parameters, in order.</param>
    /// <returns>A new vector of all the parameters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="projectedParameters"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="projectedParameters"/> isn't one value per free parameter.
    /// </exception>
    public Vector Include(Vector projectedParameters) => projection.Include(projectedParameters);

    /// <summary>The cost function's value at the free parameters, the fixed ones at their values.</summary>
    /// <param name="freeParameters">The free parameters.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="freeParameters"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="freeParameters"/> isn't one value per free parameter.</exception>
    public override double Value(Vector freeParameters) => costFunction.Value(MapFreeParameters(freeParameters));

    /// <summary>The cost function's values at the free parameters, the fixed ones at their values.</summary>
    /// <param name="freeParameters">The free parameters.</param>
    /// <returns>The values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="freeParameters"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="freeParameters"/> isn't one value per free parameter.</exception>
    public override Vector Values(Vector freeParameters) => costFunction.Values(MapFreeParameters(freeParameters));

    // Projection::mapFreeParameters: its own text, then the free entries written over the fixed values.
    private Vector MapFreeParameters(Vector freeParameters)
    {
        ArgumentNullException.ThrowIfNull(freeParameters);
        if (!(freeParameters.Size == projection.Count))
        {
            throw new ArgumentException("parameterValues.size()!=numberOfFreeParameters", nameof(freeParameters));
        }

        return projection.Include(freeParameters);
    }

    // The projection, with its free count, which QuantLib's derived class reads, made readable.
    private sealed class FreeParameters : Projection
    {
        public FreeParameters(ReadOnlySpan<double> parameterValues, ReadOnlySpan<bool> fixParameters)
            : base(parameterValues, fixParameters)
        {
        }

        public FreeParameters(Projection projection)
            : base(projection)
        {
        }

        public int Count => NumberOfFreeParameters;
    }
}
