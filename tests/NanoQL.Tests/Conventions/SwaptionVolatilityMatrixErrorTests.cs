// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The swaption volatility matrix's refusals, whose type, message and <see cref="ArgumentException.ParamName"/> a
/// vector's <c>{"throws": true}</c> can't assert (playbook §9.4): QuantLib's checks of the constructors' arguments (the
/// grid's tenors and dates in the discrete base, the matrices' sizes), <see cref="ArgumentException"/> naming the
/// argument; the 2-D interpolation's refusal of fewer than two swap tenors or option dates, raised by the callee on the
/// lengths and times the base computes (<c>x</c>, <c>y</c>); the undefined paths' refusals (SV1's empty grid,
/// <see cref="ArgumentException"/>; SV4's serials, <see cref="NanoQL.Time.Date"/>'s
/// <see cref="ArgumentOutOfRangeException"/>); and the volatility type, stored, refused on entry. And the matrix copies
/// its volatility and shift matrices (playbook §2.5): changing them afterwards changes nothing.
/// </summary>
public class SwaptionVolatilityMatrixErrorTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly Period[] Options = [new(1, TimeUnit.Years), new(2, TimeUnit.Years)];
    private static readonly Period[] Swaps = [new(2, TimeUnit.Years), new(5, TimeUnit.Years)];
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Fact]
    public void TheMatrixsRefusals()
    {
        var vols = new Matrix(2, 2, 0.2);

        Check("optionTenors", "no option tenors given", () => Tenors(Array.Empty<Period>(), Swaps, new Matrix(0, 2)));
        Check("swapTenors", "no swap tenors given", () => Tenors(Options, [], new Matrix(2, 0)));
        Check(
            "optionDates",
            "no option dates given",
            () => new SwaptionVolatilityMatrix(Today, new TARGET(), BusinessDayConvention.Following, Array.Empty<Date>(), Swaps, vols, Dc));
        Check(
            "optionTenors",
            "first option tenor is negative (0D)",
            () => Tenors([new Period(0, TimeUnit.Days), new Period(1, TimeUnit.Years)], Swaps, vols));
        Check(
            "optionTenors",
            "non increasing option tenor: 1st is 2Y, 2nd is 1Y",
            () => Tenors([new Period(2, TimeUnit.Years), new Period(1, TimeUnit.Years)], Swaps, vols));
        Check(
            "swapTenors",
            "first swap tenor is negative (0D)",
            () => Tenors(Options, [new Period(0, TimeUnit.Days), new Period(2, TimeUnit.Years)], vols));
        Check(
            "swapTenors",
            "non increasing swap tenor: 1st is 5Y, 2nd is 2Y",
            () => Tenors(Options, [new Period(5, TimeUnit.Years), new Period(2, TimeUnit.Years)], vols));
        Check(
            "swapTenors",
            "invalid Time Unit (Days) for swap length",
            () => Tenors(Options, [new Period(30, TimeUnit.Days), new Period(2, TimeUnit.Years)], vols));
        Check(
            "optionDates",
            "first option date (January 15th, 2025) must be greater than reference date (January 15th, 2025)",
            () => new SwaptionVolatilityMatrix(
                Today, new TARGET(), BusinessDayConvention.Following, [Today, new Date(15, Month.January, 2026)], Swaps, vols, Dc));
        Check(
            "optionDates",
            "non increasing option dates: 1st is January 15th, 2027, 2nd is January 15th, 2026",
            () => new SwaptionVolatilityMatrix(
                Today,
                new TARGET(),
                BusinessDayConvention.Following,
                [new Date(15, Month.January, 2027), new Date(15, Month.January, 2026)],
                Swaps,
                vols,
                Dc));
        Check(
            "volatilities",
            "mismatch between number of option dates (2) and number of rows (1) in the vol matrix",
            () => Tenors(Options, Swaps, new Matrix(1, 2, 0.2)));
        Check(
            "volatilities",
            "mismatch between number of swap tenors (2) and number of columns (3) in the vol matrix",
            () => Tenors(Options, Swaps, new Matrix(2, 3, 0.2)));
        Check(
            "shifts",
            "mismatch between number of option dates (2) and number of rows (1) in the shift matrix",
            () => Tenors(Options, Swaps, vols, new Matrix(1, 2, 0.01)));
        Check(
            "shifts",
            "mismatch between number of swap tenors (2) and number of columns (1) in the shift matrix",
            () => Tenors(Options, Swaps, vols, new Matrix(2, 1, 0.01)));

        // the 2-D interpolation's own refusal, on the lengths and times the base computes
        Check(
            "x",
            "not enough x points to interpolate: at least 2 required, 1 provided",
            () => Tenors(Options, [new Period(2, TimeUnit.Years)], new Matrix(2, 1, 0.2)));
        Check(
            "y",
            "not enough y points to interpolate: at least 2 required, 1 provided",
            () => Tenors([new Period(1, TimeUnit.Years)], Swaps, new Matrix(1, 2, 0.2)));

        // the volatility type, stored; the arguments, .NET's text for a null one
        ArgumentOutOfRangeException r = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SwaptionVolatilityMatrix(
                Today, new TARGET(), BusinessDayConvention.Following, Options, Swaps, vols, Dc, false, (VolatilityType)3));
        Assert.Equal("type", r.ParamName);
        Assert.StartsWith("Unknown volatility type (3)", r.Message, StringComparison.Ordinal);
        Assert.Equal("volatilities", Assert.Throws<ArgumentNullException>(() => Tenors(Options, Swaps, null!)).ParamName);
        Assert.Equal(
            "context",
            Assert.Throws<ArgumentNullException>(() => SwaptionVolatilityMatrix.Create(
                new TARGET(), BusinessDayConvention.Following, Options, Swaps, vols, Dc, null!)).ParamName);
        Assert.Equal(
            "calendar",
            Assert.Throws<ArgumentNullException>(() => SwaptionVolatilityMatrix.Create(
                null!, BusinessDayConvention.Following, Options, Swaps, vols, Dc, new EvaluationContext { EvaluationDate = Today }))
            .ParamName);

        // SV4: a serial outside the integers, MSVC's smallest integer, refused by Date
        r = Assert.Throws<ArgumentOutOfRangeException>(() => Tenors(Options, Swaps, vols).OptionDateFromTime(double.NaN));
        Assert.Equal("serialNumber", r.ParamName);
        Assert.StartsWith("Date's serial number (-2147483648) outside allowed range", r.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMatrixCopiesItsMatrices()
    {
        var vols = new Matrix(new[,] { { 0.20, 0.21 }, { 0.22, 0.23 } });
        var shifts = new Matrix(new[,] { { 0.01, 0.011 }, { 0.012, 0.013 } });
        SwaptionVolatilityMatrix matrix = Tenors(Options, Swaps, vols, shifts);
        double vol = matrix.Volatility(1.5, 3.0, 0.03), shift = matrix.Shift(1.5, 3.0);

        vols[0, 0] = 10.0;
        vols[1, 1] = 10.0;
        shifts[0, 0] = 1.0;
        shifts[1, 1] = 1.0;

        Assert.Equal(vol, matrix.Volatility(1.5, 3.0, 0.03));
        Assert.Equal(shift, matrix.Shift(1.5, 3.0));
    }

    private static SwaptionVolatilityMatrix Tenors(Period[] options, Period[] swaps, Matrix vols, Matrix? shifts = null) =>
        new(Today, new TARGET(), BusinessDayConvention.Following, options, swaps, vols, Dc, false, VolatilityType.ShiftedLognormal, shifts);

    private static void Check(string paramName, string message, Func<object> f)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(f);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
