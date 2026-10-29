// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2014 Ralph Schreyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdsimpleextoustorageengine.hpp ql/experimental/finitedifferences/fdsimpleextoustorageengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The two-dimensional finite-difference engine of a storage option on the spot e^(f(t) + X), X an extended
/// Ornstein-Uhlenbeck process: the storage level in the second direction, on QuantLib's elevator mesher (the levels
/// reached from empty and from full at the change rate) or a uniform one, and the injections and withdrawals by the
/// simple storage condition at the exercise dates.
/// </summary>
/// <remarks>
/// The maturity and the exercise times are read on the discount curve's day counter from its reference date, as
/// QuantLib's. A time past the seasonal shape's last is refused (<see cref="FdmExpExtOUInnerValueCalculator"/>).
/// </remarks>
[Experimental("NQL0001")]
public static class FdSimpleExtOUStorageEngine
{
    /// <summary>Prices a storage option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The process.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="tGrid">The time steps.</param>
    /// <param name="xGrid">X's mesher points.</param>
    /// <param name="yGrid">The storage mesher's points, or <see langword="null"/> for the elevator mesher.</param>
    /// <param name="shape">The seasonal shape, (time, value) pairs by increasing time, or <see langword="null"/> for
    /// none.</param>
    /// <param name="schemeDesc">The scheme, or <see langword="null"/> for Douglas's.</param>
    /// <returns>The results: the value alone.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size is negative.</exception>
    /// <exception cref="NanoQLException">The option is refused, its exercise isn't Bermudan, an exercise date is past
    /// ("exercise dates must not contain past date"), or a time is past the shape's last.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaStorageOption option,
        EvaluationContext context,
        ExtendedOrnsteinUhlenbeckProcess process,
        YieldTermStructure rTS,
        int tGrid = 50,
        int xGrid = 100,
        int? yGrid = null,
        IReadOnlyList<(double Time, double Value)>? shape = null,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(rTS);
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        if (yGrid is int y)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(y, nameof(yGrid));
        }

        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();

        // 1. Exercise
        if (!(option.Exercise.Type == ExerciseType.Bermudan))
        {
            throw new NanoQLException("Bermudan exercise supported only");
        }

        // 2. Mesher
        double maturity = rTS.DayCounter.YearFraction(rTS.ReferenceDate, option.Exercise.LastDate());
        var xMesher = new FdmSimpleProcess1dMesher(xGrid, process, maturity);
        Fdm1dMesher storageMesher = yGrid is int n
            ? new Uniform1dMesher(0, option.Capacity, n)
            : new Predefined1dMesher(ElevatorLevels(option.Capacity, option.ChangeRate));
        var mesher = new FdmMesherComposite(xMesher, storageMesher);

        // 3. Calculator
        var storageCalculator = new FdmStorageValue(mesher);

        // 4. Step conditions: the Bermudan exercises, taken by the storage condition
        var exerciseTimes = new List<double>(option.Exercise.Dates.Length);
        foreach (Time.Date d in option.Exercise.Dates)
        {
            double t = rTS.DayCounter.YearFraction(rTS.ReferenceDate, d);
            if (!(t >= 0))
            {
                throw new NanoQLException("exercise dates must not contain past date");
            }

            exerciseTimes.Add(t);
        }

        var payoff = new PlainVanillaPayoff(OptionType.Call, 0.0);
        var underlyingCalculator = new FdmExpExtOUInnerValueCalculator(payoff, mesher, shape);
        var conditions = new FdmStepConditionComposite(
            [[.. exerciseTimes]],
            [new FdmSimpleStorageCondition(exerciseTimes, mesher, underlyingCalculator, option.ChangeRate)]);

        // 5. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // 6. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, storageCalculator, maturity, tGrid, 0);
        var solver = new FdmSimple2dExtOUSolver(process, rTS, solverDesc, schemeDesc ?? FdmSchemeDesc.Douglas());
        return option.FetchResults(new OneAssetOptionResults { NPV = solver.ValueAt(process.X0, option.Load) });
    }

    // QuantLib's elevator levels: the capacity, then each level from empty and its complement from full at the change
    // rate, in a std::set ordered by "less but not close enough" (100 ulps), each value kept unless one already in the
    // set is close enough to it, as std::set's unique insert finds it
    private static List<double> ElevatorLevels(double capacity, double changeRate)
    {
        var values = new List<double> { capacity };
        for (double level = 0; level <= capacity; level += changeRate)
        {
            values.Add(level);
            values.Add(capacity - level);
        }

        var ordered = new List<double>(values.Count);
        foreach (double v in values)
        {
            // the first element the value is less than, and its predecessor, which the value must be less than
            int hi = 0;
            while (hi < ordered.Count && !LessButNotCloseEnough(v, ordered[hi]))
            {
                ++hi;
            }

            if (hi > 0 && !LessButNotCloseEnough(ordered[hi - 1], v))
            {
                continue;
            }

            ordered.Insert(hi, v);
        }

        return ordered;
    }

    private static bool LessButNotCloseEnough(double a, double b) => !(ComparisonFunctions.CloseEnough(a, b, 100) || b < a);

    // the stored value: the spot times the storage level
    private sealed class FdmStorageValue(FdmMesher mesher) : FdmInnerValueCalculator
    {
        public override double InnerValue(FdmLinearOpIterator iter, double t) =>
            Math.Exp(mesher.Location(iter, 0)) * mesher.Location(iter, 1);

        public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
    }
}
