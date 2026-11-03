// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A constant inflation volatility's quote is its value: QuantLib's <c>testConstantVolatilityQuoteIsObserved</c> builds
/// a constant CPI and a constant year-on-year volatility on a quote of 1%, moves the quote to 2%, checks that both
/// notify their observers, and reads 2% from each. Here a moved quote is a volatility built from the new value, which
/// reads it; the first volatility still reads 1%.
/// </summary>
public class ConstantInflationVolatilityQuoteTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.June, 2026) };

    [Fact]
    public void AConstantCPIVolatilityBuiltOnTheNewValueReadsIt()
    {
        ConstantCPIVolatility quoted = Cpi(0.01);
        ConstantCPIVolatility moved = Cpi(0.02);
        Assert.Equal(0.02, moved.Volatility(1.0, 0.02), 1e-10 * 0.02);
        Assert.Equal(0.01, quoted.Volatility(1.0, 0.02));
    }

    [Fact]
    public void AConstantYoYVolatilityBuiltOnTheNewValueReadsIt()
    {
        ConstantYoYOptionletVolatility quoted = YoY(0.01);
        ConstantYoYOptionletVolatility moved = YoY(0.02);
        Assert.Equal(0.02, moved.Volatility(1.0, 0.02), 1e-10 * 0.02);
        Assert.Equal(0.01, quoted.Volatility(1.0, 0.02));
    }

    private static ConstantCPIVolatility Cpi(double v) =>
        ConstantCPIVolatility.Create(
            v, 0, new TARGET(), BusinessDayConvention.Following, new Actual365Fixed(), new Period(3, TimeUnit.Months),
            Frequency.Monthly, false, Context);

    private static ConstantYoYOptionletVolatility YoY(double v) =>
        ConstantYoYOptionletVolatility.Create(
            v, 0, new TARGET(), BusinessDayConvention.Following, new Actual365Fixed(), new Period(3, TimeUnit.Months),
            Frequency.Monthly, false, Context);
}
