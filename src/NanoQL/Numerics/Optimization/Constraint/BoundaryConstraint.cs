// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2012 Mateusz Kapturski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/constraint.hpp ql/math/optimization/constraint.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>A constraint imposing all arguments to be in [low, high].</summary>
public sealed class BoundaryConstraint : Constraint
{
    private readonly double low;
    private readonly double high;

    /// <summary>Initializes a new instance of the <see cref="BoundaryConstraint"/> class.</summary>
    /// <param name="low">The lower bound of every argument.</param>
    /// <param name="high">The upper bound of every argument.</param>
    public BoundaryConstraint(double low, double high)
    {
        this.low = low;
        this.high = high;
    }

    /// <inheritdoc/>
    public override bool Test(Vector p)
    {
        ArgumentNullException.ThrowIfNull(p);
        foreach (double x in p)
        {
            if (!(low <= x && x <= high))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    protected override Vector UpperBoundCore(Vector parameters) => new(parameters.Size, high);

    /// <inheritdoc/>
    protected override Vector LowerBoundCore(Vector parameters) => new(parameters.Size, low);
}
