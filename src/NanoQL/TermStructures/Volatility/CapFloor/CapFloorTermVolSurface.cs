// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Katiuscia Manzoni
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/capfloor/capfloortermvolsurface.hpp ql/termstructures/volatility/capfloor/capfloortermvolsurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.TermStructures;

/// <summary>
/// The cap/floor smile volatility surface: the volatility for a given cap/floor, interpolated in a surface of the
/// market term volatilities of a set of caps and floors of given lengths and strikes.
/// </summary>
/// <remarks>
/// <para>
/// The volatility matrix has a row per option tenor and a column per strike; the constructor copies it. The option
/// dates and times are computed from the reference date when the surface is built, and the interpolation is a
/// <see cref="BicubicSpline"/> over the strikes (x) and the option times (y), which always extrapolates inside the
/// structure's range checks: a time before the first option time, or a strike between the strikes' range and the
/// structure's, extrapolates.
/// </para>
/// <para>
/// QuantLib's constructors on quotes and on a <see cref="Matrix"/> coincide once a quote is a <see cref="double"/>: the
/// <see cref="Matrix"/> forms are kept, so an empty matrix reaches QuantLib's own checks, where QuantLib's quote forms
/// read its first row first. The floating forms, whose reference date QuantLib moves with the evaluation date, are
/// <see cref="Create"/>: a surface under a later evaluation date is a new surface, whose option dates are that
/// date's, where QuantLib's moving surface can keep the previous reference date's option dates (a documented
/// divergence).
/// </para>
/// </remarks>
public sealed class CapFloorTermVolSurface : CapFloorTermVolatilityStructure
{
    private readonly ImmutableArray<Period> optionTenors;
    private readonly ImmutableArray<double> strikes;
    private readonly ImmutableArray<Date> optionDates;
    private readonly ImmutableArray<double> optionTimes;
    private readonly Interpolation2D interpolation;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapFloorTermVolSurface"/> class with a fixed reference date: the
    /// settlement date.
    /// </summary>
    /// <param name="settlementDate">The settlement date, the reference date.</param>
    /// <param name="calendar">The calendar of the option dates, or <see langword="null"/>, QuantLib's empty calendar,
    /// with which the option dates can't be computed.</param>
    /// <param name="bdc">The convention adjusting the option dates.</param>
    /// <param name="optionTenors">The option tenors, positive and increasing.</param>
    /// <param name="strikes">The strikes, increasing.</param>
    /// <param name="volatilities">The volatilities: a row per option tenor and a column per strike.</param>
    /// <param name="dc">The day counter; <see cref="Actual365Fixed"/>, QuantLib's default, when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A collection or the volatilities is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The option tenors are empty, not positive or not increasing, the strikes aren't increasing, the matrix's size
    /// isn't the grid's, or there are fewer than two strikes or option tenors.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">Two tenors can't be compared, or the calendar is <see langword="null"/>.</exception>
    public CapFloorTermVolSurface(
        Date settlementDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        IEnumerable<Period> optionTenors,
        IEnumerable<double> strikes,
        Matrix volatilities,
        DayCounter? dc = null)
        : this(settlementDate, calendar, bdc, optionTenors, strikes, volatilities, dc, null)
    {
    }

    private CapFloorTermVolSurface(
        Date settlementDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        IEnumerable<Period> optionTenors,
        IEnumerable<double> strikes,
        Matrix volatilities,
        DayCounter? dc,
        int? settlementDays)
        : base(settlementDate, calendar, bdc, dc ?? new Actual365Fixed(), settlementDays)
    {
        ArgumentNullException.ThrowIfNull(optionTenors);
        ArgumentNullException.ThrowIfNull(strikes);
        ArgumentNullException.ThrowIfNull(volatilities);
        this.optionTenors = [.. optionTenors];
        this.strikes = [.. strikes];
        CheckInputs(volatilities.Rows, volatilities.Columns);
        (optionDates, optionTimes) = InitializeOptionDatesAndTimes();

        // The interpolation copies the matrix.
        interpolation = new BicubicSpline(this.strikes.AsSpan(), optionTimes.AsSpan(), volatilities);
    }

    /// <summary>Gets the latest date for which the surface returns volatilities: the last option tenor's date.</summary>
    public override Date MaxDate => OptionDateFromTenor(optionTenors[^1]);

    /// <summary>Gets the option tenors.</summary>
    public ImmutableArray<Period> OptionTenors => optionTenors;

    /// <summary>Gets the option dates, computed from the reference date.</summary>
    public ImmutableArray<Date> OptionDates => optionDates;

    /// <summary>Gets the option times, from the reference date.</summary>
    public ImmutableArray<double> OptionTimes => optionTimes;

    /// <summary>Gets the strikes.</summary>
    public ImmutableArray<double> Strikes => strikes;

    /// <summary>
    /// A surface whose reference date is the given number of business days after the evaluation date, on the
    /// calendar: QuantLib's floating forms.
    /// </summary>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="calendar">The calendar of the business days and of the option dates.</param>
    /// <param name="bdc">The convention adjusting the option dates.</param>
    /// <param name="optionTenors">The option tenors, positive and increasing.</param>
    /// <param name="strikes">The strikes, increasing.</param>
    /// <param name="volatilities">The volatilities: a row per option tenor and a column per strike.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="dc">The day counter; <see cref="Actual365Fixed"/>, QuantLib's default, when
    /// <see langword="null"/>.</param>
    /// <returns>The surface, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="calendar"/>, a collection or
    /// the volatilities is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or
    /// <paramref name="bdc"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// The option tenors are empty, not positive or not increasing, the strikes aren't increasing, the matrix's size
    /// isn't the grid's, or there are fewer than two strikes or option tenors.
    /// </exception>
    /// <exception cref="NanoQLException">Two tenors can't be compared.</exception>
    public static CapFloorTermVolSurface Create(
        int settlementDays,
        Calendar calendar,
        BusinessDayConvention bdc,
        IEnumerable<Period> optionTenors,
        IEnumerable<double> strikes,
        Matrix volatilities,
        EvaluationContext context,
        DayCounter? dc = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        Date referenceDate = calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new CapFloorTermVolSurface(
            referenceDate, calendar, bdc, optionTenors, strikes, volatilities, dc, settlementDays);
    }

    /// <inheritdoc/>
    public override double MinStrike() => strikes[0];

    /// <inheritdoc/>
    public override double MaxStrike() => strikes[^1];

    /// <inheritdoc/>
    protected override double VolatilityImpl(double length, double strike) =>
        interpolation.Evaluate(strike, length, true);

    // QuantLib's checkInputs, in order.
    private void CheckInputs(int volRows, int volColumns)
    {
        int nOptionTenors = optionTenors.Length;
        int nStrikes = strikes.Length;
        if (optionTenors.IsEmpty)
        {
            throw new ArgumentException("empty option tenor vector", nameof(optionTenors));
        }

        if (!(nOptionTenors == volRows))
        {
            throw new ArgumentException(
                $"mismatch between number of option tenors ({nOptionTenors}) and number of volatility rows ({volRows})",
                "volatilities");
        }

        if (!(optionTenors[0] > new Period(0, TimeUnit.Days)))
        {
            throw new ArgumentException($"negative first option tenor: {optionTenors[0]}", nameof(optionTenors));
        }

        for (int i = 1; i < nOptionTenors; ++i)
        {
            if (!(optionTenors[i] > optionTenors[i - 1]))
            {
                throw new ArgumentException(
                    $"non increasing option tenor: {DataFormattersFunctions.Ordinal(i)} is {optionTenors[i - 1]}, "
                    + $"{DataFormattersFunctions.Ordinal(i + 1)} is {optionTenors[i]}",
                    nameof(optionTenors));
            }
        }

        if (!(nStrikes == volColumns))
        {
            throw new ArgumentException(
                $"mismatch between strikes({strikes.Length}) and vol columns ({volColumns})", "volatilities");
        }

        for (int j = 1; j < nStrikes; ++j)
        {
            if (!(strikes[j - 1] < strikes[j]))
            {
                throw new ArgumentException(
                    $"non increasing strikes: {DataFormattersFunctions.Ordinal(j)} is "
                    + $"{DataFormattersFunctions.Rate(strikes[j - 1])}, {DataFormattersFunctions.Ordinal(j + 1)} is "
                    + DataFormattersFunctions.Rate(strikes[j]),
                    nameof(strikes));
            }
        }
    }

    private (ImmutableArray<Date> Dates, ImmutableArray<double> Times) InitializeOptionDatesAndTimes()
    {
        var dates = new Date[optionTenors.Length];
        var times = new double[optionTenors.Length];
        for (int i = 0; i < optionTenors.Length; ++i)
        {
            dates[i] = OptionDateFromTenor(optionTenors[i]);
            times[i] = TimeFromReference(dates[i]);
        }

        return ([.. dates], [.. times]);
    }
}
