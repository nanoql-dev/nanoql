// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2012 Mateusz Kapturski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/constraint.hpp ql/math/optimization/constraint.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>A constraint imposing the i-th argument to be in [lowᵢ, highᵢ], for every i.</summary>
/// <remarks>
/// It copies its bounds. As in QuantLib, an argument compares as outside its bounds only when it is below lowᵢ or
/// above highᵢ, so a NaN satisfies the constraint.
/// </remarks>
public sealed class NonhomogeneousBoundaryConstraint : Constraint
{
    private readonly ImmutableArray<double> low;
    private readonly ImmutableArray<double> high;

    /// <summary>Initializes a new instance of the <see cref="NonhomogeneousBoundaryConstraint"/> class.</summary>
    /// <param name="low">The lower bound of each argument, which it copies.</param>
    /// <param name="high">The upper bound of each argument, which it copies.</param>
    /// <exception cref="NanoQLException">The bounds have different sizes.</exception>
    public NonhomogeneousBoundaryConstraint(ReadOnlySpan<double> low, ReadOnlySpan<double> high)
    {
        this.low = [.. low];
        this.high = [.. high];
        if (!(this.low.Length == this.high.Length))
        {
            throw new NanoQLException("Upper and lower boundaries sizes are inconsistent.");
        }
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The parameters have another size than the bounds.</exception>
    public override bool Test(Vector p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (!(p.Size == low.Length))
        {
            throw new NanoQLException("Number of parameters and boundaries sizes are inconsistent.");
        }

        for (int i = 0; i < p.Size; i++)
        {
            if ((p[i] < low[i]) || (p[i] > high[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    protected override Vector UpperBoundCore(Vector parameters) => new(high.AsSpan());

    /// <inheritdoc/>
    protected override Vector LowerBoundCore(Vector parameters) => new(low.AsSpan());
}
