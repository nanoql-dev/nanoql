// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the inflation volatility surfaces' refusals, which the vectors record only as throws: the
/// range checks of a date before the base date, past the maximum date, or of a strike outside the domain are
/// <see cref="NanoQLException"/>s with QuantLib's texts (the date check's double bracket among them), and so is a base
/// level never set; the year-on-year surface's displacement other than 0 or 1 is an <see cref="ArgumentException"/>
/// naming <c>displacement</c>.
/// </summary>
public class InflationVolatilityRefusalTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.June, 2024) };

    [Fact]
    public void TheRangeChecksAreExceptions()
    {
        ConstantCPIVolatility cpi = ConstantCPIVolatility.Create(
            0.01, 0, new TARGET(), BusinessDayConvention.Following, new Actual365Fixed(), new Period(3, TimeUnit.Months),
            Frequency.Monthly, false, Context);
        NanoQLException e = Assert.Throws<NanoQLException>(() => cpi.Volatility(new Date(1, Month.April, 2024), 0.02));
        Assert.Equal("date (January 1st, 2024) is before base date", e.Message);
        e = Assert.Throws<NanoQLException>(() => cpi.BaseLevel());
        Assert.Equal("Base volatility, for baseDate(), not set.", e.Message);

        ConstantYoYOptionletVolatility yoy = ConstantYoYOptionletVolatility.Create(
            0.01, 0, new TARGET(), BusinessDayConvention.Following, new Actual365Fixed(), new Period(3, TimeUnit.Months),
            Frequency.Monthly, false, Context, -0.02, 0.08);
        e = Assert.Throws<NanoQLException>(() => yoy.Volatility(new Date(1, Month.January, 2026), 0.1));
        Assert.Equal("strike (0.1) is outside the curve domain [-0.02,0.08]] at date = October 1st, 2025", e.Message);
        e = Assert.Throws<NanoQLException>(() => yoy.Volatility(new Date(1, Month.January, 2026), 0.02, new Period(10, TimeUnit.Years)));
        Assert.Equal("date (January 1st, 2016) is before base date", e.Message);
        Assert.Equal(0.01, yoy.Volatility(new Date(1, Month.January, 2026), 0.1, null, true));
    }

    [Fact]
    public void ADisplacementOtherThanZeroOrOneIsAnArgumentException()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => ConstantYoYOptionletVolatility.Create(
                0.01, 0, new TARGET(), BusinessDayConvention.Following, new Actual365Fixed(), new Period(3, TimeUnit.Months),
                Frequency.Monthly, false, Context, displacement: 0.5));
        Assert.Equal("displacement", e.ParamName);
        Assert.StartsWith("YoYOptionletVolatilitySurface: displacement (0.5) must be 0 or 1", e.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ConstantYoYOptionletVolatility.Create(
                0.01, 0, new TARGET(), BusinessDayConvention.Following, new Actual365Fixed(), new Period(3, TimeUnit.Months),
                Frequency.Monthly, false, Context, volType: (VolatilityType)7));
    }
}
