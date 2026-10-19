// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/capfloor/capfloortermvolcurve.hpp ql/termstructures/volatility/capfloor/capfloortermvolcurve.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.TermStructures;

/// <summary>
/// The at-the-money cap and floor term-volatility curve: the volatility of a cap or floor of a given length,
/// interpolated in the market volatilities of caps and floors of given lengths, whatever the strike.
/// </summary>
/// <remarks>
/// <para>
/// The option dates and times are computed from the reference date when the curve is built, and the interpolation is
/// a natural cubic spline over the option times, built once, which extrapolates inside the structure's range checks.
/// </para>
/// <para>
/// QuantLib's constructors on quotes and on volatilities coincide once a quote is a <see cref="double"/>. The floating
/// forms, whose reference date QuantLib moves with the evaluation date, are <see cref="Create"/>: a curve under a later
/// evaluation date is a new curve, whose option dates are that date's, where QuantLib's moving curve can keep the
/// previous reference date's option dates (a documented divergence).
/// </para>
/// </remarks>
public sealed class CapFloorTermVolCurve : CapFloorTermVolatilityStructure
{
    private readonly ImmutableArray<Period> optionTenors;
    private readonly ImmutableArray<double> vols;
    private readonly ImmutableArray<Date> optionDates;
    private readonly ImmutableArray<double> optionTimes;
    private readonly Interpolation interpolation;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapFloorTermVolCurve"/> class with a fixed reference date: the
    /// settlement date.
    /// </summary>
    /// <param name="settlementDate">The settlement date, the reference date.</param>
    /// <param name="calendar">The calendar of the option dates, or <see langword="null"/>, QuantLib's empty calendar,
    /// with which the option dates can't be computed.</param>
    /// <param name="bdc">The convention adjusting the option dates.</param>
    /// <param name="optionTenors">The option tenors, positive and increasing.</param>
    /// <param name="vols">The volatilities, one per option tenor.</param>
    /// <param name="dc">The day counter; <see cref="Actual365Fixed"/>, QuantLib's default, when
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="optionTenors"/> or <paramref name="vols"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException">The option tenors are empty, not positive or not increasing, there is not
    /// one volatility per option tenor, or there are fewer than two option tenors.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">Two tenors can't be compared, or the calendar is <see langword="null"/>.</exception>
    public CapFloorTermVolCurve(
        Date settlementDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        IEnumerable<Period> optionTenors,
        IEnumerable<double> vols,
        DayCounter? dc = null)
        : this(settlementDate, calendar, bdc, optionTenors, vols, dc, null)
    {
    }

    private CapFloorTermVolCurve(
        Date settlementDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        IEnumerable<Period> optionTenors,
        IEnumerable<double> vols,
        DayCounter? dc,
        int? settlementDays)
        : base(settlementDate, calendar, bdc, dc ?? new Actual365Fixed(), settlementDays)
    {
        ArgumentNullException.ThrowIfNull(optionTenors);
        ArgumentNullException.ThrowIfNull(vols);
        this.optionTenors = [.. optionTenors];
        this.vols = [.. vols];
        CheckInputs();
        (optionDates, optionTimes) = InitializeOptionDatesAndTimes();
        interpolation = new Cubic(
                CubicInterpolationDerivativeApprox.Spline,
                false,
                CubicInterpolationBoundaryCondition.SecondDerivative,
                0.0,
                CubicInterpolationBoundaryCondition.SecondDerivative,
                0.0)
            .Interpolate(optionTimes.AsSpan(), this.vols.AsSpan());
    }

    /// <summary>Gets the latest date for which the curve returns volatilities: the last option tenor's date.</summary>
    public override Date MaxDate => OptionDateFromTenor(optionTenors[^1]);

    /// <summary>Gets the option tenors.</summary>
    public ImmutableArray<Period> OptionTenors => optionTenors;

    /// <summary>Gets the option dates, computed from the reference date.</summary>
    public ImmutableArray<Date> OptionDates => optionDates;

    /// <summary>Gets the option times, from the reference date.</summary>
    public ImmutableArray<double> OptionTimes => optionTimes;

    /// <summary>
    /// A curve whose reference date is the given number of business days after the evaluation date, on the calendar:
    /// QuantLib's floating forms.
    /// </summary>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="calendar">The calendar of the business days and of the option dates.</param>
    /// <param name="bdc">The convention adjusting the option dates.</param>
    /// <param name="optionTenors">The option tenors, positive and increasing.</param>
    /// <param name="vols">The volatilities, one per option tenor.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="dc">The day counter; <see cref="Actual365Fixed"/>, QuantLib's default, when
    /// <see langword="null"/>.</param>
    /// <returns>The curve, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="calendar"/> or a collection
    /// is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or
    /// <paramref name="bdc"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The option tenors are empty, not positive or not increasing, there is not
    /// one volatility per option tenor, or there are fewer than two option tenors.</exception>
    /// <exception cref="NanoQLException">Two tenors can't be compared.</exception>
    public static CapFloorTermVolCurve Create(
        int settlementDays,
        Calendar calendar,
        BusinessDayConvention bdc,
        IEnumerable<Period> optionTenors,
        IEnumerable<double> vols,
        EvaluationContext context,
        DayCounter? dc = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        Date referenceDate = calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new CapFloorTermVolCurve(referenceDate, calendar, bdc, optionTenors, vols, dc, settlementDays);
    }

    /// <inheritdoc/>
    public override double MinStrike() => NumericLimits.QL_MIN_REAL;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <inheritdoc/>
    protected override double VolatilityImpl(double length, double strike) => interpolation.Evaluate(length, true);

    // QuantLib's checkInputs, in order.
    private void CheckInputs()
    {
        int nOptionTenors = optionTenors.Length;
        if (optionTenors.IsEmpty)
        {
            throw new ArgumentException("empty option tenor vector", nameof(optionTenors));
        }

        if (!(nOptionTenors == vols.Length))
        {
            throw new ArgumentException(
                $"mismatch between number of option tenors ({nOptionTenors}) and number of volatilities ({vols.Length})",
                nameof(vols));
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
