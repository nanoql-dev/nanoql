// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/stepconditions/fdmamericanstepcondition.hpp ql/methods/finitedifferences/stepconditions/fdmamericanstepcondition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The American exercise condition: each value floored at its inner value, from the exercise's start
/// on.</summary>
/// <remarks>
/// A working type (the S-eq plan's SEQR1): it walks the layout with an iterator it holds and resets, so applying it
/// allocates nothing.
/// </remarks>
[WorkingType]
public sealed class FdmAmericanStepCondition : StepCondition
{
    private readonly FdmMesher mesher;
    private readonly FdmInnerValueCalculator calculator;
    private readonly double exerciseStart;
    private readonly FdmLinearOpIterator iter;

    /// <summary>Initializes a new instance of the <see cref="FdmAmericanStepCondition"/> class.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="calculator">The inner value's calculator.</param>
    /// <param name="exerciseStart">The time the exercise starts, 0 by default.</param>
    /// <exception cref="ArgumentNullException">The mesher or the calculator is <see langword="null"/>.</exception>
    public FdmAmericanStepCondition(FdmMesher mesher, FdmInnerValueCalculator calculator, double exerciseStart = 0.0)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(calculator);
        this.mesher = mesher;
        this.calculator = calculator;
        this.exerciseStart = exerciseStart;
        iter = mesher.Layout.Begin();
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The values' length isn't the layout's size.</exception>
    public override void ApplyTo(Span<double> a, double t)
    {
        if (t < exerciseStart)
        {
            return;
        }

        int size = mesher.Layout.Size;
        if (!(size == a.Length))
        {
            throw new ArgumentException("inconsistent array dimensions", nameof(a));
        }

        iter.Reset();
        for (int k = 0; k < size; ++k, iter.Increment())
        {
            double innerValue = calculator.InnerValue(iter, t);
            if (innerValue > a[iter.Index])
            {
                a[iter.Index] = innerValue;
            }
        }
    }
}
