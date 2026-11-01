// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2014 Ralph Schreyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/stepconditions/fdmsimplestoragecondition.hpp ql/methods/finitedifferences/stepconditions/fdmsimplestoragecondition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The exercise condition of a simple storage option on a two-dimensional mesher, the price in direction 0 and the
/// storage level in direction 1: at each exercise time, the best of waiting, injecting or withdrawing up to the change
/// rate, or moving to an intermediate grid level (a bang-bang-wait strategy), the values between grid levels by
/// bilinear interpolation.
/// </summary>
/// <remarks>
/// A working type: it holds the layout's iterator and the step's buffer, so <see cref="ApplyTo"/> allocates only its
/// interpolation. QuantLib reads the layout's first two directions as the grid's and builds it assuming no more, so a
/// mesher of another number of dimensions is refused here; and QuantLib copies the values into its grid before
/// checking their size, which is checked first here.
/// </remarks>
[WorkingType]
public sealed class FdmSimpleStorageCondition : StepCondition
{
    private readonly double[] exerciseTimes;
    private readonly FdmMesher mesher;
    private readonly FdmInnerValueCalculator calculator;
    private readonly double changeRate;
    private readonly double[] x;
    private readonly double[] y;

    // the layout's iterator and QuantLib's retVal, held
    private readonly FdmLinearOpIterator iter;
    private readonly double[] retVal;

    /// <summary>Initializes a new instance of the <see cref="FdmSimpleStorageCondition"/> class.</summary>
    /// <param name="exerciseTimes">The exercise times.</param>
    /// <param name="mesher">The two-dimensional mesher: the price in direction 0, the storage level in direction 1.</param>
    /// <param name="calculator">The price's inner value.</param>
    /// <param name="changeRate">The most the storage changes at an exercise.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The mesher isn't two-dimensional.</exception>
    public FdmSimpleStorageCondition(
        IEnumerable<double> exerciseTimes, FdmMesher mesher, FdmInnerValueCalculator calculator, double changeRate)
    {
        ArgumentNullException.ThrowIfNull(exerciseTimes);
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(calculator);
        FdmLinearOpLayout layout = mesher.Layout;
        if (!(layout.Dim.Length == 2))
        {
            throw new ArgumentException(
                $"a storage condition needs a two-dimensional mesher, not {layout.Dim.Length}", nameof(mesher));
        }

        this.exerciseTimes = [.. exerciseTimes];
        this.mesher = mesher;
        this.calculator = calculator;
        this.changeRate = changeRate;

        var xs = new List<double>(layout.Dim[0]);
        var ys = new List<double>(layout.Dim[1]);

        iter = layout.Begin();
        for (int k = 0; k < layout.Size; ++k, iter.Increment())
        {
            if (iter.Coordinates[1] == 0)
            {
                xs.Add(mesher.Location(iter, 0));
            }

            if (iter.Coordinates[0] == 0)
            {
                ys.Add(mesher.Location(iter, 1));
            }
        }

        x = [.. xs];
        y = [.. ys];
        retVal = new double[layout.Size];
    }

    /// <summary>Gets the exercise times.</summary>
    public ImmutableArray<double> ExerciseTimes => [.. exerciseTimes];

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="a"/> isn't of the mesher's size.</exception>
    public override void ApplyTo(Span<double> a, double t)
    {
        if (!IsExerciseTime(t))
        {
            return;
        }

        FdmLinearOpLayout layout = mesher.Layout;
        if (!(layout.Size == a.Length))
        {
            throw new ArgumentException("inconsistent array dimensions", nameof(a));
        }

        var m = new Matrix(y.Length, x.Length, a);
        var interpl = new BilinearInterpolation(x, y, m);

        iter.Reset();
        for (int k = 0; k < layout.Size; ++k, iter.Increment())
        {
            ReadOnlySpan<int> coor = iter.Coordinates;
            double xi = x[coor[0]];
            double yi = y[coor[1]];

            double price = calculator.InnerValue(iter, t);

            double maxWithDraw = Math.Min(yi - y[0], changeRate);
            double sellPrice = interpl.Evaluate(xi, yi - maxWithDraw);

            double maxInject = Math.Min(y[^1] - yi, changeRate);
            double buyPrice = interpl.Evaluate(xi, yi + maxInject);

            // bang-bang-wait strategy: std::max over the list, its first largest
            double currentValue = a[iter.Index];
            currentValue = Max(currentValue, buyPrice - (price * maxInject));
            currentValue = Max(currentValue, sellPrice + (price * maxWithDraw));

            // check if intermediate grid points give a better value
            int yIter = Interpolation.UpperBound(y, yi - maxWithDraw);

            while (yIter != y.Length && y[yIter] < yi + maxInject)
            {
                if (y[yIter] != yi)
                {
                    double change = y[yIter] - yi;
                    double storagePrice = interpl.Evaluate(xi, y[yIter]);

                    currentValue = Max(currentValue, storagePrice - (change * price));
                }

                ++yIter;
            }

            retVal[iter.Index] = currentValue;
        }

        retVal.CopyTo(a);
    }

    // std::max: the second only where the first is less
    private static double Max(double first, double second) => first < second ? second : first;

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
