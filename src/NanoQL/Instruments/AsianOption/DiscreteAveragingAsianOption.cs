// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2025 Kareem Fareed
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/asianoption.hpp ql/instruments/asianoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A discrete-averaging Asian option: its payoff reads the arithmetic or geometric average of the underlying's fixings
/// on given dates.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. Two constructors, as in QuantLib: one taking the running sum or product of the past fixings and their
/// number beside the future fixing dates; one taking every fixing date with the past fixings' values, which an engine
/// splits by the evaluation context's date, the dates before it past (QuantLib's <c>setupArguments</c> on its global
/// evaluation date; the S-bar plan's SD18). Both sort their fixing dates, a stable sort, where QuantLib's second
/// constructor keeps the caller's order and its engines then read unsorted dates (BR5 (xl),
/// <c>Divergences/AsianUnsortedDatesTests.cs</c>); the past values are taken by count, as QuantLib takes them.
/// </para>
/// <para>
/// A geometric option's past fixings are carried as QuantLib's running product and, beside it, their log-sum, which the
/// geometric engines read where the product leaves double's range: 120 past fixings at 1,000 multiply to ∞, where
/// QuantLib's analytic engine prices ∞ (BR5 (xli), <c>Divergences/GeometricAsianProductRangeTests.cs</c>).
/// </para>
/// </remarks>
public sealed class DiscreteAveragingAsianOption : OneAssetOption
{
    private readonly ImmutableArray<double> allPastFixings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscreteAveragingAsianOption"/> class from the running sum or product
    /// of the past fixings: the fixing dates given may be the future ones alone.
    /// </summary>
    /// <param name="averageType">The averaging type.</param>
    /// <param name="runningAccumulator">The running sum (arithmetic) or product (geometric) of the past fixings;
    /// with no past fixing, QuantLib's 0 or 1 whatever is given.</param>
    /// <param name="pastFixings">The number of past fixings.</param>
    /// <param name="fixingDates">The fixing dates.</param>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="averageType"/> isn't a member of its type, or
    /// <paramref name="pastFixings"/> is negative.</exception>
    public DiscreteAveragingAsianOption(
        AverageType averageType,
        double runningAccumulator,
        int pastFixings,
        IEnumerable<Date> fixingDates,
        StrikedTypePayoff payoff,
        Exercise exercise)
        : base(Checked(payoff), exercise)
    {
        EnumArguments.Check(averageType, nameof(averageType));
        ArgumentOutOfRangeException.ThrowIfNegative(pastFixings);
        ArgumentNullException.ThrowIfNull(fixingDates);
        AverageType = averageType;
        FixingDates = [.. fixingDates.OrderBy(d => d)];

        // QuantLib's hard override of the accumulator where the option is unseasoned
        RunningAccumulator = pastFixings == 0 ? (averageType == AverageType.Geometric ? 1.0 : 0.0) : runningAccumulator;
        PastFixings = pastFixings;
        AllPastFixingsProvided = false;
        allPastFixings = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscreteAveragingAsianOption"/> class from every fixing date and
    /// the past fixings' values: an engine counts the dates before the evaluation date as past and takes that many
    /// values, in order.
    /// </summary>
    /// <param name="averageType">The averaging type.</param>
    /// <param name="fixingDates">Every fixing date, past and future.</param>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <param name="allPastFixings">The past fixings' values, in date order; none for an unseasoned option.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="averageType"/> isn't a member of its
    /// type.</exception>
    public DiscreteAveragingAsianOption(
        AverageType averageType,
        IEnumerable<Date> fixingDates,
        StrikedTypePayoff payoff,
        Exercise exercise,
        IEnumerable<double>? allPastFixings = null)
        : base(Checked(payoff), exercise)
    {
        EnumArguments.Check(averageType, nameof(averageType));
        ArgumentNullException.ThrowIfNull(fixingDates);
        AverageType = averageType;
        FixingDates = [.. fixingDates.OrderBy(d => d)];
        RunningAccumulator = 0.0;
        PastFixings = 0;
        AllPastFixingsProvided = true;
        this.allPastFixings = allPastFixings is null ? [] : [.. allPastFixings];
    }

    /// <summary>Gets the averaging type.</summary>
    public AverageType AverageType { get; }

    /// <summary>Gets the running sum or product the first constructor took; 0 for the second.</summary>
    public double RunningAccumulator { get; }

    /// <summary>Gets the number of past fixings the first constructor took; 0 for the second.</summary>
    public int PastFixings { get; }

    /// <summary>Gets the fixing dates, sorted.</summary>
    public ImmutableArray<Date> FixingDates { get; }

    /// <summary>Gets a value indicating whether the option was built from every fixing date and the past fixings'
    /// values, so that an engine splits them by the evaluation date.</summary>
    public bool AllPastFixingsProvided { get; }

    /// <summary>Gets the past fixings' values the second constructor took.</summary>
    public ImmutableArray<double> AllPastFixings => allPastFixings;

    /// <summary>
    /// The arguments an engine reads, QuantLib's <c>setupArguments</c> and <c>arguments::validate</c>: with every fixing
    /// date given, the dates before the context's date are past, as many values taken, and the running accumulator
    /// formed from them; the future dates the rest.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The arguments.</returns>
    /// <exception cref="NanoQLException">Too few past fixings are given for the past dates, the running sum is
    /// negative, or the running product (or a past fixing it multiplies) isn't positive.</exception>
    internal DiscreteAveragingAsianArguments Arguments(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        double runningAccumulator = RunningAccumulator;
        int pastFixings = PastFixings;
        ImmutableArray<Date> fixingDates = FixingDates;
        double runningLogSum;
        if (AllPastFixingsProvided)
        {
            Date today = context.EvaluationDate;
            pastFixings = 0;
            ImmutableArray<Date>.Builder future = ImmutableArray.CreateBuilder<Date>();
            foreach (Date fixingDate in FixingDates)
            {
                if (fixingDate < today)
                {
                    ++pastFixings;
                }
                else
                {
                    future.Add(fixingDate);
                }
            }

            fixingDates = future.ToImmutable();
            if (pastFixings > allPastFixings.Length)
            {
                throw new NanoQLException("Not enough past fixings have been provided for the required historical fixing dates");
            }

            runningAccumulator = AverageType == AverageType.Geometric ? 1.0 : 0.0;
            runningLogSum = 0.0;
            for (int i = 0; i < pastFixings; ++i)
            {
                if (AverageType == AverageType.Geometric)
                {
                    runningAccumulator *= allPastFixings[i];
                    runningLogSum += Math.Log(allPastFixings[i]);
                }
                else
                {
                    runningAccumulator += allPastFixings[i];
                }
            }
        }
        else
        {
            runningLogSum = AverageType == AverageType.Geometric ? Math.Log(runningAccumulator) : double.NaN;
        }

        if (AverageType == AverageType.Arithmetic)
        {
            if (!(runningAccumulator >= 0.0))
            {
                throw new NanoQLException(
                    $"non negative running sum required: {runningAccumulator.ToString(CultureInfo.InvariantCulture)} not allowed");
            }
        }
        else if (!(runningAccumulator > 0.0 || runningLogSum > double.NegativeInfinity))
        {
            // a product that underflows past positive fixings is still positive: its log-sum reads it (BR5 (xli))
            throw new NanoQLException(
                $"positive running product required: {runningAccumulator.ToString(CultureInfo.InvariantCulture)} not allowed");
        }

        return new DiscreteAveragingAsianArguments(AverageType, runningAccumulator, runningLogSum, pastFixings, fixingDates);
    }

    private static StrikedTypePayoff Checked(StrikedTypePayoff payoff)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return payoff;
    }
}
