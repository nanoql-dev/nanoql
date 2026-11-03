// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.hpp ql/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Methods;

/// <summary>A composite of step conditions, applied in turn, with the union of their stopping times.</summary>
/// <remarks>
/// A working type, as the conditions it holds are; applying it allocates nothing beyond what they do.
/// </remarks>
[WorkingType]
public sealed class FdmStepConditionComposite : StepCondition
{
    private readonly ImmutableArray<double> stoppingTimes;
    private readonly ImmutableArray<StepCondition> conditions;

    /// <summary>Initializes a new instance of the <see cref="FdmStepConditionComposite"/> class.</summary>
    /// <param name="stoppingTimes">The conditions' stopping times, merged here into one sorted set.</param>
    /// <param name="conditions">The conditions, applied in this order.</param>
    /// <exception cref="ArgumentNullException">An argument, a list of times or a condition is
    /// <see langword="null"/>.</exception>
    public FdmStepConditionComposite(IEnumerable<IEnumerable<double>> stoppingTimes, IEnumerable<StepCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(stoppingTimes);
        ArgumentNullException.ThrowIfNull(conditions);
        this.conditions = [.. conditions];
        if (this.conditions.Any(c => c is null))
        {
            throw new ArgumentNullException(nameof(conditions), "a step condition is null");
        }

        // std::set<Real>
        var allStoppingTimes = new SortedSet<double>();
        foreach (IEnumerable<double> stoppingTime in stoppingTimes)
        {
            ArgumentNullException.ThrowIfNull(stoppingTime, nameof(stoppingTimes));
            allStoppingTimes.UnionWith(stoppingTime);
        }

        this.stoppingTimes = [.. allStoppingTimes];
    }

    /// <summary>Gets the stopping times, sorted and unique.</summary>
    public ImmutableArray<double> StoppingTimes => stoppingTimes;

    /// <summary>Gets the conditions, in the order they are applied.</summary>
    public ImmutableArray<StepCondition> Conditions => conditions;

    /// <inheritdoc/>
    public override void ApplyTo(Span<double> a, double t)
    {
        foreach (StepCondition condition in conditions)
        {
            condition.ApplyTo(a, t);
        }
    }

    /// <summary>A composite of a composite and a snapshot condition, applied after it.</summary>
    /// <param name="c1">The snapshot condition.</param>
    /// <param name="c2">The composite.</param>
    /// <returns>The new composite.</returns>
    /// <exception cref="ArgumentNullException">A condition is <see langword="null"/>.</exception>
    public static FdmStepConditionComposite JoinConditions(FdmSnapshotCondition c1, FdmStepConditionComposite c2)
    {
        ArgumentNullException.ThrowIfNull(c1);
        ArgumentNullException.ThrowIfNull(c2);
        return new FdmStepConditionComposite([c2.StoppingTimes, [c1.Time]], [c2, c1]);
    }

    /// <summary>
    /// The step conditions of a vanilla option on a grid in the logarithm of the underlying: its cash dividends between
    /// the reference date and the exercise's last date, and its American or Bermudan exercise.
    /// </summary>
    /// <param name="schedule">The cash dividends.</param>
    /// <param name="exercise">The exercise: European, American or Bermudan.</param>
    /// <param name="mesher">The mesher.</param>
    /// <param name="calculator">The inner value's calculator.</param>
    /// <param name="refDate">
    /// The date of time 0: for the Black-Scholes engine, <see cref="FdmBlackScholesOp.Clock"/>'s reference date.
    /// </param>
    /// <param name="dayCounter">
    /// The day counter measuring times: for the Black-Scholes engine, <see cref="FdmBlackScholesOp.Clock"/>'s.
    /// </param>
    /// <param name="context">The evaluation context the dividends' amounts are read in.</param>
    /// <returns>The composite.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The schedule is <c>default(ImmutableArray)</c> or holds a null dividend, or the reference date is
    /// <c>default(Date)</c>.
    /// </exception>
    /// <exception cref="NanoQLException">The exercise type isn't supported.</exception>
    public static FdmStepConditionComposite VanillaComposite(
        ImmutableArray<Dividend> schedule,
        Exercise exercise,
        FdmMesher mesher,
        FdmInnerValueCalculator calculator,
        Date refDate,
        DayCounter dayCounter,
        EvaluationContext context)
    {
        if (schedule.IsDefault)
        {
            throw new ArgumentException("default(ImmutableArray) is not a dividend schedule; an empty one is []", nameof(schedule));
        }

        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(dayCounter);
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(refDate, nameof(refDate));
        if (schedule.Any(d => d is null))
        {
            throw new ArgumentException("null dividend in schedule", nameof(schedule));
        }

        var stoppingTimes = new List<IEnumerable<double>>();
        var stepConditions = new List<StepCondition>();

        if (!schedule.IsEmpty)
        {
            Date maturityDate = exercise.LastDate();
            ImmutableArray<Dividend> dividends =
                [.. schedule.Where(div => div.Date >= refDate && div.Date <= maturityDate)];

            var dividendCondition = new FdmDividendHandler(dividends, mesher, refDate, dayCounter, 0, context);
            stepConditions.Add(dividendCondition);

            double[] dividendTimes = [.. dividendCondition.DividendTimes];
            double maturityTime = dayCounter.YearFraction(refDate, exercise.LastDate());

            // the dividend times, capped at maturity
            for (int i = 0; i < dividendTimes.Length; ++i)
            {
                dividendTimes[i] = Min(maturityTime, dividendTimes[i]);
            }

            stoppingTimes.Add([.. dividendTimes]);

            // and a moment after each, on the capped times
            for (int i = 0; i < dividendTimes.Length; ++i)
            {
                dividendTimes[i] = Min(maturityTime, dividendTimes[i] + 1e-5);
            }

            stoppingTimes.Add([.. dividendTimes]);
        }

        if (!(exercise.Type == ExerciseType.American
              || exercise.Type == ExerciseType.European
              || exercise.Type == ExerciseType.Bermudan))
        {
            throw new NanoQLException("exercise type is not supported");
        }

        if (exercise.Type == ExerciseType.American)
        {
            double exerciseStart = dayCounter.YearFraction(refDate, exercise.Date(0));
            stepConditions.Add(new FdmAmericanStepCondition(mesher, calculator, exerciseStart));
        }
        else if (exercise.Type == ExerciseType.Bermudan)
        {
            var bermudanCondition = new FdmBermudanStepCondition(exercise.Dates, refDate, dayCounter, mesher, calculator);
            stepConditions.Add(bermudanCondition);
            stoppingTimes.Add(bermudanCondition.ExerciseTimes);
        }

        return new FdmStepConditionComposite(stoppingTimes, stepConditions);
    }

    // std::min(a, b)
    private static double Min(double a, double b) => b < a ? b : a;
}
