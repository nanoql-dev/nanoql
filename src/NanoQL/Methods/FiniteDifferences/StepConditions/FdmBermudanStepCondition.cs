// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/stepconditions/fdmbermudanstepcondition.hpp ql/methods/finitedifferences/stepconditions/fdmbermudanstepcondition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Methods;

/// <summary>The Bermudan exercise condition: at each exercise time, each value floored at its inner value.</summary>
/// <remarks>
/// A working type: it walks the layout with an iterator it holds and resets, so applying it allocates nothing. QuantLib
/// fills an array of each point's locations it never reads; the port drops it.
/// </remarks>
[WorkingType]
public sealed class FdmBermudanStepCondition : StepCondition
{
    private readonly double[] exerciseTimes;
    private readonly FdmMesher mesher;
    private readonly FdmInnerValueCalculator calculator;
    private readonly FdmLinearOpIterator iter;

    /// <summary>Initializes a new instance of the <see cref="FdmBermudanStepCondition"/> class.</summary>
    /// <param name="exerciseDates">The exercise dates.</param>
    /// <param name="referenceDate">The date of time 0.</param>
    /// <param name="dayCounter">The day counter measuring the exercise times.</param>
    /// <param name="mesher">The mesher.</param>
    /// <param name="calculator">The inner value's calculator.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    public FdmBermudanStepCondition(
        IEnumerable<Date> exerciseDates,
        Date referenceDate,
        DayCounter dayCounter,
        FdmMesher mesher,
        FdmInnerValueCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(exerciseDates);
        ArgumentNullException.ThrowIfNull(dayCounter);
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(calculator);
        Date.ThrowIfDefault(referenceDate, nameof(referenceDate));
        this.mesher = mesher;
        this.calculator = calculator;

        var times = new List<double>();
        foreach (Date exerciseDate in exerciseDates)
        {
            Date.ThrowIfDefault(exerciseDate, nameof(exerciseDates));
            times.Add(dayCounter.YearFraction(referenceDate, exerciseDate));
        }

        exerciseTimes = [.. times];
        iter = mesher.Layout.Begin();
    }

    /// <summary>Gets the exercise times.</summary>
    public ImmutableArray<double> ExerciseTimes => [.. exerciseTimes];

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The values' length isn't the layout's size.</exception>
    public override void ApplyTo(Span<double> a, double t)
    {
        if (IsExerciseTime(t))
        {
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

    // std::find, by ==
    private bool IsExerciseTime(double t)
    {
        foreach (double e in exerciseTimes)
        {
            if (e == t)
            {
                return true;
            }
        }

        return false;
    }
}
