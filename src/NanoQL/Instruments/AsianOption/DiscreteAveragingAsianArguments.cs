// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2025 Kareem Fareed
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/asianoption.hpp ql/instruments/asianoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// The arguments a discrete-averaging Asian engine reads, QuantLib's <c>DiscreteAveragingAsianOption::arguments</c>
/// beyond the option's payoff and exercise: split by the evaluation date and checked
/// (<see cref="DiscreteAveragingAsianOption.Arguments(EvaluationContext)"/>).
/// </summary>
/// <param name="AverageType">The averaging type.</param>
/// <param name="RunningAccumulator">The running sum or product of the past fixings, QuantLib's.</param>
/// <param name="RunningLogSum">A geometric option's past fixings' log-sum (the log of the running product the first
/// constructor took); NaN for an arithmetic one.</param>
/// <param name="PastFixings">The number of past fixings.</param>
/// <param name="FixingDates">The future fixing dates, sorted.</param>
internal sealed record DiscreteAveragingAsianArguments(
    AverageType AverageType,
    double RunningAccumulator,
    double RunningLogSum,
    int PastFixings,
    ImmutableArray<Date> FixingDates)
{
    /// <summary>
    /// Gets the log of the running product: QuantLib's <c>log(runningAccumulator)</c> where the product is a finite
    /// normal double, its bits there; the past fixings' log-sum where the product has left double's range.
    /// </summary>
    public double RunningLog => double.IsNormal(RunningAccumulator) && RunningAccumulator > 0.0
        ? Math.Log(RunningAccumulator)
        : RunningLogSum;

    /// <summary>
    /// The arguments with each fixing dated from the evaluation date to an engine's clock origin taken at the origin:
    /// the spot, with no variance and no discount accrued, so a past fixing at the spot. Where the structures settle
    /// after the evaluation date, QuantLib's engines drop such a fixing from the average or refuse it as past
    /// (today's limit).
    /// </summary>
    /// <param name="evaluationDate">The evaluation date.</param>
    /// <param name="beforeOrigin">Whether a date is before the engine's clock origin.</param>
    /// <param name="spot">The spot.</param>
    /// <returns>These arguments, or new ones with those fixings past.</returns>
    public DiscreteAveragingAsianArguments AtOrigin(Date evaluationDate, Func<Date, bool> beforeOrigin, double spot)
    {
        int atOrigin = 0;
        foreach (Date d in FixingDates)
        {
            if (d >= evaluationDate && beforeOrigin(d))
            {
                ++atOrigin;
            }
        }

        if (atOrigin == 0)
        {
            return this;
        }

        double accumulator = RunningAccumulator, logSum = RunningLogSum;
        ImmutableArray<Date>.Builder future = ImmutableArray.CreateBuilder<Date>(FixingDates.Length - atOrigin);
        foreach (Date d in FixingDates)
        {
            if (d >= evaluationDate && beforeOrigin(d))
            {
                if (AverageType == AverageType.Geometric)
                {
                    accumulator *= spot;
                    logSum += Math.Log(spot);
                }
                else
                {
                    accumulator += spot;
                }
            }
            else
            {
                future.Add(d);
            }
        }

        return this with
        {
            RunningAccumulator = accumulator,
            RunningLogSum = logSum,
            PastFixings = PastFixings + atOrigin,
            FixingDates = future.MoveToImmutable(),
        };
    }
}
