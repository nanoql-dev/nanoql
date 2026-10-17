// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2012 Mateusz Kapturski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/constraint.hpp ql/math/optimization/constraint.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>A constraint enforcing both given sub-constraints: its bounds are the tighter of theirs.</summary>
public sealed class CompositeConstraint : Constraint
{
    private readonly Constraint c1;
    private readonly Constraint c2;

    /// <summary>Initializes a new instance of the <see cref="CompositeConstraint"/> class.</summary>
    /// <param name="c1">The first constraint.</param>
    /// <param name="c2">The second constraint.</param>
    public CompositeConstraint(Constraint c1, Constraint c2)
    {
        ArgumentNullException.ThrowIfNull(c1);
        ArgumentNullException.ThrowIfNull(c2);
        this.c1 = c1;
        this.c2 = c2;
    }

    /// <inheritdoc/>
    public override bool Test(Vector p) => c1.Test(p) && c2.Test(p);

    /// <inheritdoc/>
    protected override Vector UpperBoundCore(Vector parameters)
    {
        Vector c1ub = c1.UpperBound(parameters);
        Vector c2ub = c2.UpperBound(parameters);
        var rtrnArray = new Vector(c1ub.Size, 0.0);
        for (int iter = 0; iter < c1ub.Size; iter++)
        {
            rtrnArray.At(iter) = Min(c1ub.At(iter), c2ub.At(iter));
        }

        return rtrnArray;
    }

    // std::min and std::max, which return their first argument unless the comparison holds (so for a NaN or a signed
    // zero they differ from Math.Min and Math.Max).
    private static double Min(double a, double b) => b < a ? b : a;

    private static double Max(double a, double b) => a < b ? b : a;

    /// <inheritdoc/>
    protected override Vector LowerBoundCore(Vector parameters)
    {
        Vector c1lb = c1.LowerBound(parameters);
        Vector c2lb = c2.LowerBound(parameters);
        var rtrnArray = new Vector(c1lb.Size, 0.0);
        for (int iter = 0; iter < c1lb.Size; iter++)
        {
            rtrnArray.At(iter) = Max(c1lb.At(iter), c2lb.At(iter));
        }

        return rtrnArray;
    }
}
