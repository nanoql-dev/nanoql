// SPDX-FileCopyrightText: Copyright (C) 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2015 Michael von den Driesch
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/optionlet/strippedoptionlet.hpp ql/termstructures/volatility/optionlet/strippedoptionlet.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.TermStructures;

/// <summary>
/// A matrix of optionlet (caplet and floorlet) volatilities, also called forward-forward volatilities, calculated
/// elsewhere, as a <see cref="StrippedOptionletBase"/>.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's quotes are their values: a new volatility is a new object. The fixing times run from the reference date,
/// the settlement days after the context's evaluation date, computed at construction as QuantLib computes them; a
/// later evaluation date is a new object.
/// </para>
/// <para>
/// The at-the-money rates are the index's fixings on the fixing dates, read from the context at each call, today's
/// forecast, as QuantLib reads them at each call.
/// </para>
/// </remarks>
public sealed class StrippedOptionlet : StrippedOptionletBase
{
    private readonly Calendar calendar;
    private readonly int settlementDays;
    private readonly BusinessDayConvention businessDayConvention;
    private readonly DayCounter dc;
    private readonly IborIndex iborIndex;
    private readonly ImmutableArray<Date> optionletDates;
    private readonly ImmutableArray<double> optionletTimes;
    private readonly ImmutableArray<ImmutableArray<double>> optionletStrikes;
    private readonly ImmutableArray<ImmutableArray<double>> optionletVolatilities;

    /// <summary>
    /// Initializes a new instance of the <see cref="StrippedOptionlet"/> class with the same strikes at every fixing
    /// date.
    /// </summary>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="bdc">The business-day convention.</param>
    /// <param name="iborIndex">The index whose fixings are the at-the-money rates.</param>
    /// <param name="optionletDates">The fixing dates, increasing, the first after the evaluation date.</param>
    /// <param name="strikes">The strikes, increasing.</param>
    /// <param name="v">The volatilities: a row per fixing date, a volatility per strike.</param>
    /// <param name="dc">The day counter of the fixing times.</param>
    /// <param name="context">The evaluation context: its evaluation date sets the reference date.</param>
    /// <param name="type">The volatilities' type.</param>
    /// <param name="displacement">The displacement of shifted lognormal volatilities.</param>
    /// <exception cref="ArgumentNullException">A reference argument, or a row of <paramref name="v"/>, is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or
    /// <paramref name="bdc"/> or <paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The dates, strikes and volatilities are inconsistent (QuantLib's
    /// checks).</exception>
    public StrippedOptionlet(
        int settlementDays,
        Calendar calendar,
        BusinessDayConvention bdc,
        IborIndex iborIndex,
        IEnumerable<Date> optionletDates,
        IEnumerable<double> strikes,
        IEnumerable<IEnumerable<double>> v,
        DayCounter dc,
        EvaluationContext context,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double displacement = 0.0)
        : this(settlementDays, calendar, bdc, iborIndex, SharedStrikes(optionletDates, strikes), v, dc, context, type, displacement)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StrippedOptionlet"/> class with strikes per fixing date.
    /// </summary>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="bdc">The business-day convention.</param>
    /// <param name="iborIndex">The index whose fixings are the at-the-money rates.</param>
    /// <param name="optionletDates">The fixing dates, increasing, the first after the evaluation date.</param>
    /// <param name="strikes">The strikes: a row per fixing date, each increasing.</param>
    /// <param name="v">The volatilities: a row per fixing date, a volatility per strike of the row.</param>
    /// <param name="dc">The day counter of the fixing times.</param>
    /// <param name="context">The evaluation context: its evaluation date sets the reference date.</param>
    /// <param name="type">The volatilities' type.</param>
    /// <param name="displacement">The displacement of shifted lognormal volatilities.</param>
    /// <exception cref="ArgumentNullException">A reference argument, or a row of <paramref name="strikes"/> or
    /// <paramref name="v"/>, is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or
    /// <paramref name="bdc"/> or <paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The dates, strikes and volatilities are inconsistent (QuantLib's
    /// checks).</exception>
    public StrippedOptionlet(
        int settlementDays,
        Calendar calendar,
        BusinessDayConvention bdc,
        IborIndex iborIndex,
        IEnumerable<Date> optionletDates,
        IEnumerable<IEnumerable<double>> strikes,
        IEnumerable<IEnumerable<double>> v,
        DayCounter dc,
        EvaluationContext context,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double displacement = 0.0)
        : this(settlementDays, calendar, bdc, iborIndex, StrikesPerDate(optionletDates, strikes), v, dc, context, type, displacement)
    {
    }

    private StrippedOptionlet(
        int settlementDays,
        Calendar calendar,
        BusinessDayConvention bdc,
        IborIndex iborIndex,
        (ImmutableArray<Date> Dates, ImmutableArray<ImmutableArray<double>> Strikes) grid,
        IEnumerable<IEnumerable<double>> v,
        DayCounter dc,
        EvaluationContext context,
        VolatilityType type,
        double displacement)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        EnumArguments.Check(bdc, nameof(bdc));
        ArgumentNullException.ThrowIfNull(iborIndex);
        ArgumentNullException.ThrowIfNull(v);
        ArgumentNullException.ThrowIfNull(dc);
        ArgumentNullException.ThrowIfNull(context);
        EnumArguments.Check(type, nameof(type));

        this.calendar = calendar;
        this.settlementDays = settlementDays;
        businessDayConvention = bdc;
        this.dc = dc;
        this.iborIndex = iborIndex;
        VolatilityType = type;
        Displacement = displacement;
        optionletDates = grid.Dates;
        optionletStrikes = grid.Strikes;
        optionletVolatilities = [.. v.Select(row => (row ?? throw new ArgumentNullException(nameof(v))).ToImmutableArray())];
        CheckInputs(context.EvaluationDate);

        Date refDate = calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        optionletTimes = [.. optionletDates.Select(d => dc.YearFraction(refDate, d))];
    }

    /// <inheritdoc/>
    public override int OptionletMaturities => optionletDates.Length;

    /// <inheritdoc/>
    public override ImmutableArray<Date> OptionletFixingDates => optionletDates;

    /// <inheritdoc/>
    public override ImmutableArray<double> OptionletFixingTimes => optionletTimes;

    /// <inheritdoc/>
    public override DayCounter DayCounter => dc;

    /// <inheritdoc/>
    public override Calendar? Calendar => calendar;

    /// <inheritdoc/>
    public override int? SettlementDays => settlementDays;

    /// <inheritdoc/>
    public override BusinessDayConvention BusinessDayConvention => businessDayConvention;

    /// <inheritdoc/>
    public override VolatilityType VolatilityType { get; }

    /// <inheritdoc/>
    public override double Displacement { get; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative or not less than the number of
    /// fixing dates.</exception>
    public override ImmutableArray<double> OptionletStrikes(int i)
    {
        if (!(i >= 0 && i < optionletStrikes.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i), $"index ({i}) must be less than optionletStrikes size ({optionletStrikes.Length})");
        }

        return optionletStrikes[i];
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative or not less than the number of
    /// fixing dates.</exception>
    public override ImmutableArray<double> OptionletVolatilities(int i)
    {
        if (!(i >= 0 && i < optionletVolatilities.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i), $"index ({i}) must be less than optionletVolatilities size ({optionletVolatilities.Length})");
        }

        return optionletVolatilities[i];
    }

    /// <summary>The index's fixings on the fixing dates, with today's forecast.</summary>
    /// <param name="context">The evaluation context, whose fixings and forecasts the rates read.</param>
    /// <returns>The rates, a new array at each call.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">A fixing is missing, or the index can't forecast one.</exception>
    public override ImmutableArray<double> AtmOptionletRates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rates = ImmutableArray.CreateBuilder<double>(optionletDates.Length);
        foreach (Date d in optionletDates)
        {
            rates.Add(iborIndex.Fixing(d, context, true));
        }

        return rates.MoveToImmutable();
    }

    // QuantLib's first constructor: the same strikes at every date.
    private static (ImmutableArray<Date>, ImmutableArray<ImmutableArray<double>>) SharedStrikes(
        IEnumerable<Date> optionletDates, IEnumerable<double> strikes)
    {
        ArgumentNullException.ThrowIfNull(optionletDates);
        ArgumentNullException.ThrowIfNull(strikes);
        ImmutableArray<Date> dates = [.. optionletDates];
        ImmutableArray<double> row = [.. strikes];
        return (dates, [.. Enumerable.Repeat(row, dates.Length)]);
    }

    private static (ImmutableArray<Date>, ImmutableArray<ImmutableArray<double>>) StrikesPerDate(
        IEnumerable<Date> optionletDates, IEnumerable<IEnumerable<double>> strikes)
    {
        ArgumentNullException.ThrowIfNull(optionletDates);
        ArgumentNullException.ThrowIfNull(strikes);
        return (
            [.. optionletDates],
            [.. strikes.Select(row => (row ?? throw new ArgumentNullException(nameof(strikes))).ToImmutableArray())]);
    }

    private void CheckInputs(Date evaluationDate)
    {
        int nOptionletDates = optionletDates.Length;
        if (optionletDates.IsEmpty)
        {
            throw new ArgumentException("empty optionlet tenor vector", "optionletDates");
        }

        if (!(nOptionletDates == optionletVolatilities.Length))
        {
            throw new ArgumentException(
                $"mismatch between number of option tenors ({nOptionletDates}) and number of volatility rows "
                + $"({optionletVolatilities.Length})",
                "v");
        }

        if (!(optionletDates[0] > evaluationDate))
        {
            throw new ArgumentException($"first option date ({optionletDates[0]}) is in the past", "optionletDates");
        }

        for (int i = 1; i < nOptionletDates; ++i)
        {
            if (!(optionletDates[i] > optionletDates[i - 1]))
            {
                throw new ArgumentException(
                    $"non increasing option dates: {DataFormattersFunctions.Ordinal(i)} is {optionletDates[i - 1]}, "
                    + $"{DataFormattersFunctions.Ordinal(i + 1)} is {optionletDates[i]}",
                    "optionletDates");
            }
        }

        if (!(nOptionletDates == optionletStrikes.Length))
        {
            throw new ArgumentException(
                $"mismatch between number of option tenors ({nOptionletDates}) and number of strikes "
                + $"({optionletStrikes.Length})",
                "strikes");
        }

        for (int i = 0; i < nOptionletDates; ++i)
        {
            if (!(optionletStrikes[i].Length == optionletVolatilities[i].Length))
            {
                throw new ArgumentException(
                    $"mismatch between number of option tenors ({nOptionletDates}) and number of vol columns at date "
                    + $"{i} ({optionletVolatilities[i].Length}",
                    "v");
            }

            for (int j = 1; j < optionletStrikes[i].Length; ++j)
            {
                // QuantLib prints the first date's strikes, reading past them when that row is shorter.
                if (!(optionletStrikes[i][j - 1] < optionletStrikes[i][j]))
                {
                    throw new ArgumentException(
                        $"non increasing strikes at date {i}: {DataFormattersFunctions.Ordinal(j)} is "
                        + $"{DataFormattersFunctions.Rate(optionletStrikes[i][j - 1])}, "
                        + $"{DataFormattersFunctions.Ordinal(j + 1)} is "
                        + $"{DataFormattersFunctions.Rate(optionletStrikes[i][j])}",
                        "strikes");
                }
            }
        }
    }
}
