// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the inflation indexes' refusals, which the vectors record only as throws: a forecast with no
/// curve is QuantLib's empty handle's <see cref="NanoQLException"/>, "empty Handle cannot be dereferenced", and a
/// missing past fixing or base fixing a <see cref="NanoQLException"/> with QuantLib's text; an interpolation type or a
/// frequency a member never handles is an <see cref="ArgumentOutOfRangeException"/> naming the parameter, QuantLib's
/// deprecated <c>CPI::AsIndex</c> (0) among the types, refused with QuantLib's "unknown CPI interpolation type: 0".
/// </summary>
public class InflationIndexRefusalTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.June, 2024) };

    [Fact]
    public void AMissingFixingOrCurveIsAnException()
    {
        var index = new UKRPI();
        EvaluationContext context = Context with
        {
            Fixings = Context.Fixings.AddFixing(index, new Date(1, Month.April, 2024), 350.0),
        };

        NanoQLException e = Assert.Throws<NanoQLException>(() => index.Fixing(new Date(1, Month.August, 2024), context));
        Assert.Equal("empty Handle cannot be dereferenced", e.Message);
        e = Assert.Throws<NanoQLException>(() => index.Fixing(new Date(1, Month.March, 2024), context));
        Assert.Equal("Missing UK RPI fixing for March 1st, 2024", e.Message);
        e = Assert.Throws<NanoQLException>(() => new YYUKRPI().Fixing(new Date(1, Month.August, 2024), context));
        Assert.Equal("empty Handle cannot be dereferenced", e.Message);
        e = Assert.Throws<NanoQLException>(() => new YYUKRPI().PastFixing(new Date(1, Month.March, 2024), context));
        Assert.Equal("Missing UK YY_RPI fixing for March 1st, 2024", e.Message);
        e = Assert.Throws<NanoQLException>(() => new USCPI().LastFixingDate(context));
        Assert.Equal("no fixings stored for USA CPI", e.Message);

        // a curve whose base fixing isn't stored
        var curve = new ZeroInflationCurve(
            Context.EvaluationDate,
            [new Date(1, Month.May, 2024), new Date(1, Month.May, 2025)],
            [0.03, 0.03],
            Frequency.Monthly,
            new Actual365Fixed());
        e = Assert.Throws<NanoQLException>(() => index.Clone(curve).Fixing(new Date(1, Month.August, 2024), context));
        Assert.Equal("UK RPI index fixing at base date May 1st, 2024 is not available", e.Message);
    }

    [Fact]
    public void AnUnhandledInterpolationTypeIsOutOfRange()
    {
        var index = new UKRPI();
        foreach (int type in new[] { 0, 3, -1 })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
                () => CPI.LaggedFixing(index, new Date(1, Month.March, 2024), new Period(3, TimeUnit.Months), (CPIInterpolationType)type, Context));
            Assert.Equal("interpolationType", e.ParamName);
            Assert.StartsWith($"unknown CPI interpolation type: {type}", e.Message, StringComparison.Ordinal);
            e = Assert.Throws<ArgumentOutOfRangeException>(
                () => CPI.LaggedYoYRate(new YYUKRPI(), new Date(1, Month.March, 2024), new Period(3, TimeUnit.Months), (CPIInterpolationType)type, Context));
            Assert.Equal("interpolationType", e.ParamName);
        }
    }

    [Fact]
    public void AnUnhandledFrequencyIsOutOfRange()
    {
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => InflationTermStructureFunctions.InflationPeriod(new Date(1, Month.March, 2024), Frequency.Weekly));
        Assert.Equal("frequency", e.ParamName);
        Assert.StartsWith("Frequency not handled: Weekly", e.Message, StringComparison.Ordinal);

        // an index of an unhandled frequency refuses where it computes a period, as the function raises it
        var weekly = new ZeroInflationIndex(
            "W", new CustomRegion("Testland", "XX"), false, Frequency.Weekly, new Period(1, TimeUnit.Weeks), new Currencies.EURCurrency());
        e = Assert.Throws<ArgumentOutOfRangeException>(() => Context.Fixings.AddFixing(weekly, new Date(1, Month.March, 2024), 1.0));
        Assert.Equal("frequency", e.ParamName);

        // a value outside the type is refused on entry
        e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ZeroInflationIndex(
                "X", new UKRegion(), false, (Frequency)5, new Period(1, TimeUnit.Months), new Currencies.GBPCurrency()));
        Assert.Equal("frequency", e.ParamName);
    }
}
