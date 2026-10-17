// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2012 Mateusz Kapturski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/constraint.hpp ql/math/optimization/constraint.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>A constraint imposing positivity on all arguments.</summary>
public sealed class PositiveConstraint : Constraint
{
    /// <inheritdoc/>
    public override bool Test(Vector p)
    {
        ArgumentNullException.ThrowIfNull(p);
        foreach (double x in p)
        {
            if (!(x > 0.0))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    protected override Vector UpperBoundCore(Vector parameters) => new(parameters.Size, double.MaxValue);

    /// <inheritdoc/>
    protected override Vector LowerBoundCore(Vector parameters) => new(parameters.Size, 0.0);
}
