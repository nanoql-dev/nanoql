// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/swaption/swaptionvoldiscrete.hpp ql/termstructures/volatility/swaption/swaptionvoldiscrete.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.TermStructures;

/// <summary>
/// The base class of swaption volatility structures given on a grid of option dates and swap tenors: the grid's
/// dates, times and lengths, and the interpolation from an option time to its date.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib: the volatility matrix and the volatility cubes derive from it, and read the
/// grid through QuantLib's protected data, kept as protected read-only members. QuantLib's structure whose reference
/// date moves with the evaluation date recomputes its option dates, times and swap lengths when the date moves; a
/// built structure here has a fixed reference date (a derived structure's factory computes it from the context, as
/// <see cref="SwaptionVolatilityMatrix.Create"/> does), so its grid is computed once, in the constructor.
/// </para>
/// <para>
/// The option tenors and the swap tenors must be positive and increasing, the option dates after the reference date
/// and increasing; the dates two tenors give aren't checked, as in QuantLib. Empty tenors or dates, which QuantLib reads
/// at their first element, are refused there. The constructor from option dates keeps QuantLib's behaviour: its option
/// tenors are zero periods, and its date interpolation maps every time to serial 0, so
/// <see cref="OptionDateFromTime"/> always throws.
/// </para>
/// <para>
/// The constructor reads <see cref="TermStructure.DayCounter"/>, <see cref="TermStructure.Calendar"/> and
/// <see cref="VolatilityTermStructure.BusinessDayConvention"/> to compute the grid, so they are sealed here: in C#, a
/// call from a base constructor runs a derived override before the derived constructor has run, where C++ runs the
/// base's own. A derived structure that answers another structure's conventions, as QuantLib's cube answers its ATM
/// structure's, passes them to the constructor instead.
/// </para>
/// </remarks>
public abstract class SwaptionVolatilityDiscrete : SwaptionVolatilityStructure
{
    /// <inheritdoc/>
    /// <remarks>Sealed: the constructor reads it.</remarks>
    public sealed override DayCounter DayCounter => base.DayCounter;

    /// <inheritdoc/>
    /// <remarks>Sealed: the constructor reads it.</remarks>
    public sealed override Calendar? Calendar => base.Calendar;

    /// <inheritdoc/>
    /// <remarks>Sealed: the constructor reads it.</remarks>
    public sealed override BusinessDayConvention BusinessDayConvention => base.BusinessDayConvention;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwaptionVolatilityDiscrete"/> class from option tenors, each one's
    /// option date the reference date advanced by it on the calendar.
    /// </summary>
    /// <param name="optionTenors">The option tenors, positive and increasing.</param>
    /// <param name="swapTenors">The swap tenors, positive, increasing, and in months or years.</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="cal">The calendar of the option dates, or <see langword="null"/>, QuantLib's empty calendar, with
    /// which option dates can't be computed from tenors.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="settlementDays">The settlement days the reference date was calculated with, if any: QuantLib's
    /// moving form, whose date a derived structure's factory computes from the context.</param>
    /// <exception cref="ArgumentNullException">A collection or the day counter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The tenors are empty, not positive or not increasing, or a swap tenor isn't
    /// in months or years.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> isn't a member of its type, or
    /// <paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="NanoQLException">Two tenors can't be compared, or the calendar is <see langword="null"/>.</exception>
    protected SwaptionVolatilityDiscrete(
        IEnumerable<Period> optionTenors,
        IEnumerable<Period> swapTenors,
        Date referenceDate,
        Calendar? cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        int? settlementDays = null)
        : base(referenceDate, cal, bdc, dc, settlementDays)
    {
        ArgumentNullException.ThrowIfNull(optionTenors);
        ArgumentNullException.ThrowIfNull(swapTenors);
        OptionTenors = [.. optionTenors];
        SwapTenors = [.. swapTenors];
        NOptionTenors = OptionTenors.Length;
        NSwapTenors = SwapTenors.Length;

        CheckOptionTenors();
        (OptionDates, OptionDatesAsReal, OptionInterpolatorDatesAsReal) = InitializeOptionDates();
        (OptionTimes, OptionInterpolatorTimes) = InitializeOptionTimes();

        CheckSwapTenors();
        SwapLengths = InitializeSwapLengths();

        OptionInterpolator = OptionInterpolatorOf(OptionInterpolatorTimes, OptionInterpolatorDatesAsReal);
    }

    /// <summary>Initializes a new instance of the <see cref="SwaptionVolatilityDiscrete"/> class from option dates.</summary>
    /// <param name="optionDates">The option dates, after the reference date and increasing.</param>
    /// <param name="swapTenors">The swap tenors, positive, increasing, and in months or years.</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="cal">The calendar of option dates computed from tenors, or <see langword="null"/>, QuantLib's
    /// empty calendar.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="dc">The day counter.</param>
    /// <exception cref="ArgumentNullException">A collection or the day counter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The dates are empty, not after the reference date or not increasing, or the
    /// swap tenors are empty, not positive, not increasing, or not in months or years.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bdc"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">Two swap tenors can't be compared.</exception>
    protected SwaptionVolatilityDiscrete(
        IEnumerable<Date> optionDates,
        IEnumerable<Period> swapTenors,
        Date referenceDate,
        Calendar? cal,
        BusinessDayConvention bdc,
        DayCounter dc)
        : base(referenceDate, cal, bdc, dc)
    {
        ArgumentNullException.ThrowIfNull(optionDates);
        ArgumentNullException.ThrowIfNull(swapTenors);
        OptionDates = [.. optionDates];
        foreach (Date d in OptionDates)
        {
            Date.ThrowIfDefault(d, nameof(optionDates));
        }

        SwapTenors = [.. swapTenors];
        NOptionTenors = OptionDates.Length;
        NSwapTenors = SwapTenors.Length;

        // QuantLib sizes the tenors and the dates as reals with the dates' count and never fills them.
        OptionTenors = [.. Enumerable.Repeat(new Period(0, TimeUnit.Days), NOptionTenors)];
        OptionDatesAsReal = [.. Enumerable.Repeat(0.0, NOptionTenors)];
        OptionInterpolatorDatesAsReal = [.. Enumerable.Repeat(0.0, NOptionTenors + 1)];

        CheckOptionDates(referenceDate);
        (OptionTimes, OptionInterpolatorTimes) = InitializeOptionTimes();

        CheckSwapTenors();
        SwapLengths = InitializeSwapLengths();

        OptionInterpolator = OptionInterpolatorOf(OptionInterpolatorTimes, OptionInterpolatorDatesAsReal);
    }

    /// <summary>Gets the option tenors: zero periods for a structure built from option dates.</summary>
    public ImmutableArray<Period> OptionTenors { get; }

    /// <summary>Gets the option dates.</summary>
    public ImmutableArray<Date> OptionDates { get; }

    /// <summary>Gets the option times: the option dates' times from the reference date.</summary>
    public ImmutableArray<double> OptionTimes { get; }

    /// <summary>Gets the swap tenors.</summary>
    public ImmutableArray<Period> SwapTenors { get; }

    /// <summary>Gets the swap lengths: the swap tenors' lengths in years.</summary>
    public ImmutableArray<double> SwapLengths { get; }

    /// <summary>Gets the number of option tenors or dates: QuantLib's <c>nOptionTenors_</c>.</summary>
    protected int NOptionTenors { get; }

    /// <summary>Gets the number of swap tenors: QuantLib's <c>nSwapTenors_</c>.</summary>
    protected int NSwapTenors { get; }

    /// <summary>Gets the option dates' serial numbers, as reals: zeros for a structure built from option dates.</summary>
    protected ImmutableArray<double> OptionDatesAsReal { get; }

    /// <summary>Gets the interpolation's times: 0, then the option times.</summary>
    protected ImmutableArray<double> OptionInterpolatorTimes { get; }

    /// <summary>
    /// Gets the interpolation's values: the reference date's serial number, then the option dates' (zeros for a
    /// structure built from option dates).
    /// </summary>
    protected ImmutableArray<double> OptionInterpolatorDatesAsReal { get; }

    /// <summary>
    /// Gets the linear interpolation from an option time to its date's serial number, extrapolation enabled: QuantLib's
    /// <c>optionInterpolator_</c>.
    /// </summary>
    protected Interpolation OptionInterpolator { get; }

    /// <summary>The option date of an option time, interpolated linearly between the grid's option dates.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <returns>The date, its serial number the interpolated one truncated toward zero.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The serial number is outside <see cref="Date"/>'s range; always so
    /// for a structure built from option dates.</exception>
    /// <remarks>
    /// QuantLib converts the interpolated serial to an integer with <c>static_cast</c>, whose result is undefined for
    /// NaN and for a value outside the integers; MSVC's conversion, the platform the vectors are generated on, gives
    /// the smallest integer there, which <see cref="Date"/>'s range check refuses. That serial is taken here in those
    /// cases, so the refusal is QuantLib's.
    /// </remarks>
    public Date OptionDateFromTime(double optionTime)
    {
        double serial = OptionInterpolator.Evaluate(optionTime);
        int truncated = serial > -2147483649.0 && serial < 2147483648.0 ? (int)serial : int.MinValue;
        return new Date(truncated);
    }

    private static Interpolation OptionInterpolatorOf(ImmutableArray<double> times, ImmutableArray<double> datesAsReal) =>
        new Linear().Interpolate(times.AsSpan(), datesAsReal.AsSpan()).EnableExtrapolation();

    private void CheckOptionDates(Date reference)
    {
        if (OptionDates.IsEmpty)
        {
            // QuantLib reads the first date of an empty vector.
            throw new ArgumentException("no option dates given", "optionDates");
        }

        if (!(OptionDates[0] > reference))
        {
            throw new ArgumentException(
                $"first option date ({OptionDates[0]}) must be greater than reference date ({reference})",
                "optionDates");
        }

        for (int i = 1; i < NOptionTenors; ++i)
        {
            if (!(OptionDates[i] > OptionDates[i - 1]))
            {
                throw new ArgumentException(
                    $"non increasing option dates: {DataFormattersFunctions.Ordinal(i)} is {OptionDates[i - 1]}, "
                    + $"{DataFormattersFunctions.Ordinal(i + 1)} is {OptionDates[i]}",
                    "optionDates");
            }
        }
    }

    private void CheckOptionTenors()
    {
        if (OptionTenors.IsEmpty)
        {
            // QuantLib reads the first tenor of an empty vector.
            throw new ArgumentException("no option tenors given", "optionTenors");
        }

        if (!(OptionTenors[0] > new Period(0, TimeUnit.Days)))
        {
            throw new ArgumentException($"first option tenor is negative ({OptionTenors[0]})", "optionTenors");
        }

        for (int i = 1; i < NOptionTenors; ++i)
        {
            if (!(OptionTenors[i] > OptionTenors[i - 1]))
            {
                throw new ArgumentException(
                    $"non increasing option tenor: {DataFormattersFunctions.Ordinal(i)} is {OptionTenors[i - 1]}, "
                    + $"{DataFormattersFunctions.Ordinal(i + 1)} is {OptionTenors[i]}",
                    "optionTenors");
            }
        }
    }

    private void CheckSwapTenors()
    {
        if (SwapTenors.IsEmpty)
        {
            // QuantLib reads the first tenor of an empty vector.
            throw new ArgumentException("no swap tenors given", "swapTenors");
        }

        if (!(SwapTenors[0] > new Period(0, TimeUnit.Days)))
        {
            throw new ArgumentException($"first swap tenor is negative ({SwapTenors[0]})", "swapTenors");
        }

        for (int i = 1; i < NSwapTenors; ++i)
        {
            if (!(SwapTenors[i] > SwapTenors[i - 1]))
            {
                throw new ArgumentException(
                    $"non increasing swap tenor: {DataFormattersFunctions.Ordinal(i)} is {SwapTenors[i - 1]}, "
                    + $"{DataFormattersFunctions.Ordinal(i + 1)} is {SwapTenors[i]}",
                    "swapTenors");
            }
        }
    }

    // The option dates of the tenors, and their serial numbers as reals, after the reference date's.
    private (ImmutableArray<Date> Dates, ImmutableArray<double> AsReal, ImmutableArray<double> InterpolatorAsReal)
        InitializeOptionDates()
    {
        var dates = new Date[NOptionTenors];
        var asReal = new double[NOptionTenors];
        var interpolatorAsReal = new double[NOptionTenors + 1];
        interpolatorAsReal[0] = ReferenceDate.SerialNumber;
        for (int i = 0; i < NOptionTenors; ++i)
        {
            dates[i] = OptionDateFromTenor(OptionTenors[i]);
            asReal[i] = interpolatorAsReal[i + 1] = dates[i].SerialNumber;
        }

        return ([.. dates], [.. asReal], [.. interpolatorAsReal]);
    }

    // The option dates' times, and the same after a time 0 for the reference date.
    private (ImmutableArray<double> Times, ImmutableArray<double> InterpolatorTimes) InitializeOptionTimes()
    {
        var times = new double[NOptionTenors];
        var interpolatorTimes = new double[NOptionTenors + 1];
        interpolatorTimes[0] = 0.0;
        for (int i = 0; i < NOptionTenors; ++i)
        {
            times[i] = interpolatorTimes[i + 1] = TimeFromReference(OptionDates[i]);
        }

        return ([.. times], [.. interpolatorTimes]);
    }

    private ImmutableArray<double> InitializeSwapLengths()
    {
        var lengths = new double[NSwapTenors];
        for (int i = 0; i < NSwapTenors; ++i)
        {
            Period tenor = SwapTenors[i];

            // SwapLength's own refusal, naming this constructor's parameter: the tenors are positive by now.
            if (tenor.Units is not (TimeUnit.Months or TimeUnit.Years))
            {
                throw new ArgumentException($"invalid Time Unit ({tenor.Units}) for swap length", "swapTenors");
            }

            lengths[i] = SwapLength(tenor);
        }

        return [.. lengths];
    }
}
