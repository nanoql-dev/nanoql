// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2010, 2014 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/stepconditions/fdmsimpleswingcondition.hpp ql/methods/finitedifferences/stepconditions/fdmsimpleswingcondition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// The swing option's exercise condition, on a mesher whose swing direction counts the exercise rights used: at each
/// exercise time, a point with a right left takes the value one more right used plus the cash flow wherever that is
/// worth more, or wherever the rights still to use need every remaining exercise.
/// </summary>
/// <remarks>
/// A working type (the S-van plan's VD11): applied in place through a scratch buffer it holds, QuantLib's copy of the
/// values, so an application allocates nothing (VD12). The exercise times are found by equality, as QuantLib's
/// <c>std::find</c>.
/// </remarks>
public sealed class FdmSimpleSwingCondition : StepCondition
{
    private readonly double[] exerciseTimes;
    private readonly FdmMesher mesher;
    private readonly FdmInnerValueCalculator calculator;
    private readonly int minExercises;
    private readonly int swingDirection;

    // the layout's iterator and QuantLib's retVal, held
    private readonly FdmLinearOpIterator iter;
    private readonly double[] retVal;

    /// <summary>Initializes a new instance of the <see cref="FdmSimpleSwingCondition"/> class.</summary>
    /// <param name="exerciseTimes">The exercise times, in order.</param>
    /// <param name="mesher">The mesher.</param>
    /// <param name="calculator">The cash flow of one exercise at a point.</param>
    /// <param name="swingDirection">The direction counting the rights used.</param>
    /// <param name="minExercises">The number of rights the holder must use at least.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The swing direction isn't one of the layout's, or the minimum is negative.
    /// </exception>
    public FdmSimpleSwingCondition(
        IEnumerable<double> exerciseTimes,
        FdmMesher mesher,
        FdmInnerValueCalculator calculator,
        int swingDirection,
        int minExercises = 0)
    {
        ArgumentNullException.ThrowIfNull(exerciseTimes);
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentOutOfRangeException.ThrowIfNegative(swingDirection);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(swingDirection, mesher.Layout.Dim.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(minExercises);
        this.exerciseTimes = [.. exerciseTimes];
        this.mesher = mesher;
        this.calculator = calculator;
        this.minExercises = minExercises;
        this.swingDirection = swingDirection;
        iter = mesher.Layout.Begin();
        retVal = new double[mesher.Layout.Size];
    }

    /// <summary>Gets the exercise times.</summary>
    public ImmutableArray<double> ExerciseTimes => [.. exerciseTimes];

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The values aren't the layout's size, with QuantLib's message.</exception>
    public override void ApplyTo(Span<double> a, double t)
    {
        // std::find, by ==
        int found = Array.IndexOf(exerciseTimes, t);
        FdmLinearOpLayout layout = mesher.Layout;
        int maxExerciseValue = layout.Dim[swingDirection] - 1;

        if (found >= 0)
        {
            int size = layout.Size;
            if (!(size == a.Length))
            {
                throw new ArgumentException("inconsistent array dimensions", nameof(a));
            }

            a.CopyTo(retVal);

            int d = exerciseTimes.Length - found;

            iter.Reset();
            for (int k = 0; k < size; ++k, iter.Increment())
            {
                int exercisesUsed = iter.Coordinates[swingDirection];

                if (exercisesUsed < maxExerciseValue)
                {
                    double cashflow = calculator.InnerValue(iter, t);
                    double currentValue = a[iter.Index];
                    double valuePlusOneExercise = a[layout.Neighbourhood(iter, swingDirection, 1)];

                    if (currentValue < valuePlusOneExercise + cashflow || exercisesUsed + d <= minExercises)
                    {
                        retVal[iter.Index] = valuePlusOneExercise + cashflow;
                    }
                }
            }

            retVal.AsSpan().CopyTo(a);
        }
    }
}
