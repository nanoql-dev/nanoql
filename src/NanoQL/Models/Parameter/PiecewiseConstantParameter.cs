// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/parameter.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// A piecewise-constant parameter: a(t) = aᵢ for tᵢ₋₁ ≤ t &lt; tᵢ, one parameter per piece, so a time equal to a
/// breakpoint takes the next piece. This kind of parameter is usually used to enhance the fitting of a model.
/// </summary>
public sealed class PiecewiseConstantParameter : Parameter
{
    /// <summary>Initializes a new instance of the <see cref="PiecewiseConstantParameter"/> class, its parameters at 0.</summary>
    /// <param name="times">The breakpoints: one piece more than the breakpoints.</param>
    /// <param name="constraint">The constraint on the parameters, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="times"/> is null.</exception>
    /// <remarks>QuantLib leaves the parameters uninitialized.</remarks>
    public PiecewiseConstantParameter(IEnumerable<double> times, Constraint? constraint = null)
        : this(Copied(times), constraint ?? new NoConstraint())
    {
    }

    private PiecewiseConstantParameter(ImmutableArray<double> times, Constraint constraint)
        : base(times.Length + 1, new PiecewiseConstantImpl(times), constraint)
    {
    }

    private static ImmutableArray<double> Copied(IEnumerable<double> times)
    {
        ArgumentNullException.ThrowIfNull(times);
        return [.. times];
    }

    private sealed class PiecewiseConstantImpl(ImmutableArray<double> times) : Impl
    {
        public override double Value(ImmutableArray<double> parameters, double t)
        {
            // std::upper_bound: the first breakpoint t is less than, by its algorithm, so a NaN takes the last piece
            int first = 0;
            int count = times.Length;
            while (count > 0)
            {
                int step = count / 2;
                int it = first + step;
                if (!(t < times[it]))
                {
                    first = it + 1;
                    count -= step + 1;
                }
                else
                {
                    count = step;
                }
            }

            return parameters[first];
        }
    }
}
