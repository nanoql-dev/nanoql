// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2012 Mateusz Kapturski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/constraint.hpp ql/math/optimization/constraint.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The base class of an optimization's constraints on its parameters.</summary>
/// <remarks>
/// <para>
/// Immutable. QuantLib's Pimpl becomes the virtual members: a constraint implements <see cref="Test"/>, and overrides
/// <see cref="UpperBoundCore"/> and <see cref="LowerBoundCore"/>, whose defaults give no bound, where its bounds are
/// known. QuantLib's empty constraint, <c>Constraint()</c>, is <see langword="null"/>.
/// </para>
/// <para>
/// An extension point, as in QuantLib: each constraint derives from it.
/// </para>
/// </remarks>
public abstract class Constraint
{
    /// <summary>Initializes a new instance of the <see cref="Constraint"/> class.</summary>
    protected Constraint()
    {
    }

    /// <summary>Whether the parameters satisfy the constraint.</summary>
    /// <param name="p">The parameters.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    public abstract bool Test(Vector p);

    /// <summary>The upper bound for the given parameters, element by element.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns>A new vector, of the parameters' size.</returns>
    /// <exception cref="NanoQLException">The constraint's bound has another size than the parameters.</exception>
    public Vector UpperBound(Vector parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Vector result = UpperBoundCore(parameters);
        if (!(parameters.Size == result.Size))
        {
            throw new NanoQLException(
                $"upper bound size ({result.Size}) not equal to params size ({parameters.Size})");
        }

        return result;
    }

    /// <summary>The lower bound for the given parameters, element by element.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns>A new vector, of the parameters' size.</returns>
    /// <exception cref="NanoQLException">The constraint's bound has another size than the parameters.</exception>
    public Vector LowerBound(Vector parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Vector result = LowerBoundCore(parameters);
        if (!(parameters.Size == result.Size))
        {
            throw new NanoQLException(
                $"lower bound size ({result.Size}) not equal to params size ({parameters.Size})");
        }

        return result;
    }

    /// <summary>
    /// Moves the parameters along a direction, in place, by the largest of β, β/2, β/4, ... (at most 201 halvings)
    /// whose point satisfies the constraint.
    /// </summary>
    /// <param name="p">The parameters, which move: every reference to the vector sees the change.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="beta">The first step.</param>
    /// <returns>The step taken.</returns>
    /// <exception cref="NanoQLException">No step up to the last halving satisfies the constraint.</exception>
    public double Update(Vector p, Vector direction, double beta)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(direction);
        double diff = beta;
        Vector newParams = p + (diff * direction);
        bool valid = Test(newParams);
        int icount = 0;
        while (!valid)
        {
            if (icount > 200)
            {
                throw new NanoQLException("can't update parameter vector");
            }

            diff *= 0.5;
            icount++;
            newParams = p + (diff * direction);
            valid = Test(newParams);
        }

        p += diff * direction;
        return diff;
    }

    /// <summary>The upper bound for the given parameters, which <see cref="UpperBound"/> checks: by default none.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns>A new vector, the largest double in each element by default.</returns>
    protected virtual Vector UpperBoundCore(Vector parameters) => new(parameters.Size, double.MaxValue);

    /// <summary>The lower bound for the given parameters, which <see cref="LowerBound"/> checks: by default none.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns>A new vector, the most negative double in each element by default.</returns>
    protected virtual Vector LowerBoundCore(Vector parameters) => new(parameters.Size, -double.MaxValue);
}
