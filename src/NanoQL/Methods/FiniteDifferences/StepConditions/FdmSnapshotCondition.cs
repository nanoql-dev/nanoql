// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/stepconditions/fdmsnapshotcondition.hpp ql/methods/finitedifferences/stepconditions/fdmsnapshotcondition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>The step condition that keeps a copy of the values at one time, read after the rollback.</summary>
/// <remarks>
/// A working type (the S-eq plan's SEQR1): it copies the values into a buffer of its own, allocated at its first
/// snapshot, where QuantLib assigns a new array.
/// </remarks>
public sealed class FdmSnapshotCondition : StepCondition
{
    private double[] values = [];

    /// <summary>Initializes a new instance of the <see cref="FdmSnapshotCondition"/> class.</summary>
    /// <param name="t">The time of the snapshot.</param>
    public FdmSnapshotCondition(double t)
    {
        Time = t;
    }

    /// <summary>Gets the time of the snapshot: QuantLib's <c>getTime</c>.</summary>
    public double Time { get; }

    /// <summary>Gets the values at the snapshot's time, empty before it is taken.</summary>
    internal ReadOnlySpan<double> Values => values;

    /// <inheritdoc/>
    public override void ApplyTo(Span<double> a, double t)
    {
        if (t == Time)
        {
            if (values.Length != a.Length)
            {
                values = new double[a.Length];
            }

            a.CopyTo(values);
        }
    }

    /// <summary>The values at the snapshot's time: QuantLib's <c>getValues</c>.</summary>
    /// <returns>A copy of the values, empty before the snapshot is taken.</returns>
    public Vector GetValues() => new(values);
}
