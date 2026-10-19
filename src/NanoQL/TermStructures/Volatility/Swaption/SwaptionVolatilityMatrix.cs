// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/swaptionvolmatrix.hpp ql/termstructures/volatility/swaption/swaptionvolmatrix.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The at-the-money swaption-volatility matrix: the volatility of a swaption, bilinearly interpolated in the swap
/// length and the option time between the market volatilities of a grid of option dates and swap tenors.
/// </summary>
/// <remarks>
/// <para>
/// The volatility matrix has a row per option date and a column per swap tenor: the element (i, j) is the volatility
/// of the i-th option and the j-th tenor; the shifts, when given, are laid out alike. The constructor copies both.
/// Outside the grid the interpolation extends linearly, or, with flat extrapolation, takes the nearest edge's value; a
/// query outside the structure's range is refused unless extrapolation is enabled or requested.
/// </para>
/// <para>
/// QuantLib's constructors on quotes and on a <see cref="Matrix"/> coincide once a quote is a <see cref="double"/>: the
/// <see cref="Matrix"/> forms are kept, and the floating forms, whose reference date is the evaluation date adjusted on
/// the calendar, are <see cref="Create"/>. As in QuantLib, the smile sections are by time, flat at the at-the-money
/// volatility (read at a dummy strike of 5%), and the shift is read without the lognormal check that
/// <see cref="ConstantSwaptionVolatility"/> makes.
/// </para>
/// </remarks>
public sealed class SwaptionVolatilityMatrix : SwaptionVolatilityDiscrete
{
    private readonly VolatilityType volatilityType;
    private readonly Interpolation2D interpolation;
    private readonly Interpolation2D interpolationShifts;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwaptionVolatilityMatrix"/> class with a fixed reference date, on
    /// option tenors.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar of the option dates, or <see langword="null"/>, QuantLib's empty calendar,
    /// with which option dates can't be computed from tenors.</param>
    /// <param name="bdc">The convention adjusting the option dates.</param>
    /// <param name="optionTenors">The option tenors, positive and increasing.</param>
    /// <param name="swapTenors">The swap tenors, positive, increasing, and in months or years.</param>
    /// <param name="volatilities">The volatilities: a row per option tenor and a column per swap tenor.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="flatExtrapolation">Whether the volatilities extrapolate flat outside the grid, rather than
    /// linearly.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shifts">The shifts, laid out as the volatilities; zero when <see langword="null"/> or empty, QuantLib's
    /// default <c>Matrix()</c>.</param>
    /// <exception cref="ArgumentNullException">A collection, the volatilities or the day counter is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The tenors are empty, not positive or not increasing, a swap tenor isn't in months or years, a matrix's size
    /// isn't the grid's, or there are fewer than two option or swap tenors.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> or <paramref name="type"/> isn't a member
    /// of its type.</exception>
    /// <exception cref="NanoQLException">Two tenors can't be compared, or the calendar is <see langword="null"/>.</exception>
    public SwaptionVolatilityMatrix(
        Date referenceDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        IEnumerable<Period> optionTenors,
        IEnumerable<Period> swapTenors,
        Matrix volatilities,
        DayCounter dayCounter,
        bool flatExtrapolation = false,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        Matrix? shifts = null)
        : this(referenceDate, calendar, bdc, optionTenors, swapTenors, volatilities, dayCounter, flatExtrapolation, type, shifts, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwaptionVolatilityMatrix"/> class with a fixed reference date, on
    /// option dates.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar of option dates computed from tenors, or <see langword="null"/>, QuantLib's
    /// empty calendar.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="optionDates">The option dates, after the reference date and increasing.</param>
    /// <param name="swapTenors">The swap tenors, positive, increasing, and in months or years.</param>
    /// <param name="volatilities">The volatilities: a row per option date and a column per swap tenor.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="flatExtrapolation">Whether the volatilities extrapolate flat outside the grid, rather than
    /// linearly.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shifts">The shifts, laid out as the volatilities; zero when <see langword="null"/> or empty, QuantLib's
    /// default <c>Matrix()</c>.</param>
    /// <exception cref="ArgumentNullException">A collection, the volatilities or the day counter is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The dates are empty, not after the reference date or not increasing, the swap tenors are empty, not positive,
    /// not increasing or not in months or years, a matrix's size isn't the grid's, or there are fewer than two option
    /// dates or swap tenors.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> or <paramref name="type"/> isn't a member
    /// of its type.</exception>
    /// <exception cref="NanoQLException">Two swap tenors can't be compared.</exception>
    public SwaptionVolatilityMatrix(
        Date referenceDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        IEnumerable<Date> optionDates,
        IEnumerable<Period> swapTenors,
        Matrix volatilities,
        DayCounter dayCounter,
        bool flatExtrapolation = false,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        Matrix? shifts = null)
        : base(optionDates, swapTenors, referenceDate, calendar, bdc, dayCounter)
    {
        (volatilityType, interpolation, interpolationShifts) = Initialize(volatilities, flatExtrapolation, type, shifts);
    }

    private SwaptionVolatilityMatrix(
        Date referenceDate,
        Calendar? calendar,
        BusinessDayConvention bdc,
        IEnumerable<Period> optionTenors,
        IEnumerable<Period> swapTenors,
        Matrix volatilities,
        DayCounter dayCounter,
        bool flatExtrapolation,
        VolatilityType type,
        Matrix? shifts,
        int? settlementDays)
        : base(optionTenors, swapTenors, referenceDate, calendar, bdc, dayCounter, settlementDays)
    {
        (volatilityType, interpolation, interpolationShifts) = Initialize(volatilities, flatExtrapolation, type, shifts);
    }

    /// <summary>Gets the latest date for which the structure returns volatilities: the last option date.</summary>
    public override Date MaxDate => OptionDates[^1];

    /// <summary>Gets the largest swap tenor for which the structure returns volatilities: the last swap tenor.</summary>
    public override Period MaxSwapTenor => SwapTenors[^1];

    /// <inheritdoc/>
    public override VolatilityType VolatilityType => volatilityType;

    /// <summary>
    /// A volatility matrix whose reference date is the evaluation date, adjusted on the calendar: QuantLib's floating
    /// forms, on settlement days 0.
    /// </summary>
    /// <param name="calendar">The calendar the reference date is adjusted on and the option dates are computed on.</param>
    /// <param name="bdc">The convention adjusting the option dates.</param>
    /// <param name="optionTenors">The option tenors, positive and increasing.</param>
    /// <param name="swapTenors">The swap tenors, positive, increasing, and in months or years.</param>
    /// <param name="volatilities">The volatilities: a row per option tenor and a column per swap tenor.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="flatExtrapolation">Whether the volatilities extrapolate flat outside the grid, rather than
    /// linearly.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shifts">The shifts, laid out as the volatilities; zero when <see langword="null"/> or empty, QuantLib's
    /// default <c>Matrix()</c>.</param>
    /// <returns>The structure, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="calendar"/>, a collection,
    /// the volatilities or the day counter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The tenors are empty, not positive or not increasing, a swap tenor isn't in months or years, a matrix's size
    /// isn't the grid's, or there are fewer than two option or swap tenors.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> or <paramref name="type"/> isn't a member
    /// of its type.</exception>
    /// <exception cref="NanoQLException">Two tenors can't be compared.</exception>
    public static SwaptionVolatilityMatrix Create(
        Calendar calendar,
        BusinessDayConvention bdc,
        IEnumerable<Period> optionTenors,
        IEnumerable<Period> swapTenors,
        Matrix volatilities,
        DayCounter dayCounter,
        EvaluationContext context,
        bool flatExtrapolation = false,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        Matrix? shifts = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(calendar);
        Date referenceDate = calendar.Advance(context.EvaluationDate, 0, TimeUnit.Days);
        return new SwaptionVolatilityMatrix(
            referenceDate, calendar, bdc, optionTenors, swapTenors, volatilities, dayCounter, flatExtrapolation, type, shifts, 0);
    }

    /// <summary>The lower indexes of the volatility matrix's corners surrounding an option date and a swap tenor.</summary>
    /// <param name="optionDate">The option date.</param>
    /// <param name="swapTenor">The swap tenor.</param>
    /// <returns>The option's (row's) index and the swap's (column's) index.</returns>
    /// <exception cref="ArgumentException">The swap tenor isn't positive, or isn't in months or years.</exception>
    public (int OptionIndex, int SwapIndex) Locate(Date optionDate, Period swapTenor) =>
        Locate(TimeFromReference(optionDate), SwapLength(swapTenor));

    /// <summary>The lower indexes of the volatility matrix's corners surrounding an option time and a swap length.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <returns>The option's (row's) index and the swap's (column's) index.</returns>
    public (int OptionIndex, int SwapIndex) Locate(double optionTime, double swapLength) =>
        (interpolation.LocateY(optionTime), interpolation.LocateX(swapLength));

    /// <inheritdoc/>
    public override double MinStrike() => -NumericLimits.QL_MAX_REAL;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <inheritdoc/>
    protected override SmileSection SmileSectionImpl(double optionTime, double swapLength)
    {
        // dummy strike
        double atmVol = VolatilityImpl(optionTime, swapLength, 0.05);
        return new FlatSmileSection(
            optionTime, atmVol, DayCounter, null, VolatilityType, Shift(optionTime, swapLength, true));
    }

    /// <inheritdoc/>
    protected override double VolatilityImpl(double optionTime, double swapLength, double strike) =>
        interpolation.Evaluate(swapLength, optionTime, true);

    /// <summary>The interpolated shift, for either volatility type: QuantLib's matrix makes no lognormal check.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <param name="swapLength">The swap length, in years.</param>
    /// <returns>The shift.</returns>
    protected override double ShiftImpl(double optionTime, double swapLength) =>
        interpolationShifts.Evaluate(swapLength, optionTime, true);

    // QuantLib's checkInputs and the two interpolations, for every form.
    private (VolatilityType, Interpolation2D, Interpolation2D) Initialize(
        Matrix volatilities, bool flatExtrapolation, VolatilityType type, Matrix? shifts)
    {
        ArgumentNullException.ThrowIfNull(volatilities);
        EnumArguments.Check(type, nameof(type));

        // QuantLib's default Matrix() is no shifts.
        bool noShifts = shifts is null || (shifts.Rows == 0 && shifts.Columns == 0);
        CheckInputs(
            volatilities.Rows, volatilities.Columns, noShifts ? 0 : shifts!.Rows, noShifts ? 0 : shifts!.Columns);
        Matrix shiftValues = noShifts ? new Matrix(volatilities.Rows, volatilities.Columns, 0.0) : shifts!;

        // The interpolations copy their matrices.
        Interpolation2D vols = new BilinearInterpolation(SwapLengths.AsSpan(), OptionTimes.AsSpan(), volatilities);
        Interpolation2D shiftsOf = new BilinearInterpolation(SwapLengths.AsSpan(), OptionTimes.AsSpan(), shiftValues);
        return flatExtrapolation
            ? (type, new FlatExtrapolator2D(vols), new FlatExtrapolator2D(shiftsOf))
            : (type, vols, shiftsOf);
    }

    private void CheckInputs(int volRows, int volsColumns, int shiftRows, int shiftsColumns)
    {
        if (!(NOptionTenors == volRows))
        {
            throw new ArgumentException(
                $"mismatch between number of option dates ({NOptionTenors}) and number of rows ({volRows}) in the vol "
                + "matrix",
                "volatilities");
        }

        if (!(NSwapTenors == volsColumns))
        {
            throw new ArgumentException(
                $"mismatch between number of swap tenors ({NSwapTenors}) and number of columns ({volsColumns}) in the "
                + "vol matrix",
                "volatilities");
        }

        if (shiftRows == 0 && shiftsColumns == 0)
        {
            shiftRows = volRows;
            shiftsColumns = volsColumns;
        }

        if (!(NOptionTenors == shiftRows))
        {
            throw new ArgumentException(
                $"mismatch between number of option dates ({NOptionTenors}) and number of rows ({shiftRows}) in the "
                + "shift matrix",
                "shifts");
        }

        if (!(NSwapTenors == shiftsColumns))
        {
            throw new ArgumentException(
                $"mismatch between number of swap tenors ({NSwapTenors}) and number of columns ({shiftsColumns}) in "
                + "the shift matrix",
                "shifts");
        }
    }
}
