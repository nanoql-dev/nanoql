// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="Business252"/> built without a calendar reads it through <see cref="ConventionProvider"/>: QuantLib's
/// Brazilian settlement calendar with nothing set, built in the provider's scope, or the provider's own; and
/// <see cref="Business252.Calendar"/> returns the calendar counted.
/// </summary>
public class Business252ProviderTests
{
    private static readonly Date Start = new(2, Month.March, 2026);
    private static readonly Date End = new(2, Month.March, 2027);

    [Fact]
    public void WithNothingSetTheCalendarIsBrazilsSettlementOne()
    {
        var dayCounter = new Business252();

        Assert.Equal(new Brazil(BrazilMarket.Settlement), dayCounter.Calendar);
        Assert.Empty(dayCounter.Calendar.AddedHolidays);
        Assert.Equal("Business/252(Brazil)", dayCounter.Name);
        Assert.Equal(new Business252(new Brazil()), dayCounter);
    }

    [Fact]
    public void TheDefaultCalendarIsBuiltInTheProvidersScope()
    {
        Date holiday = new(10, Month.March, 2026);
        var provider = TestConventions.AddingHoliday("p", "Brazil.Settlement", holiday);
        Business252 dayCounter;
        using (ConventionProvider.Use(provider))
        {
            dayCounter = new Business252();
        }

        Assert.Equal([holiday], dayCounter.Calendar.AddedHolidays);
        Assert.Equal(new Business252().DayCount(Start, End) - 1, dayCounter.DayCount(Start, End));
        Assert.Equal(["business252", "calendar:Brazil.Settlement"], provider.Resolved);
    }

    [Fact]
    public void AProvidersOwnCalendarIsCounted()
    {
        var provider = new TestConventions("p") { Business252Calendar = () => new TARGET() };
        Business252 dayCounter;
        using (ConventionProvider.Use(provider))
        {
            dayCounter = new Business252();
        }

        Assert.Equal(new TARGET(), dayCounter.Calendar);
        Assert.Equal("Business/252(TARGET)", dayCounter.Name);
        Assert.Equal(new Business252(new TARGET()).DayCount(Start, End), dayCounter.DayCount(Start, End));
    }

    [Fact]
    public void AGivenCalendarReadsNoProvider()
    {
        var provider = new TestConventions("p") { Business252Calendar = () => new TARGET() };
        var calendar = new UnitedKingdom();
        Business252 dayCounter;
        using (ConventionProvider.Use(provider))
        {
            dayCounter = new Business252(calendar);
        }

        Assert.Same(calendar, dayCounter.Calendar);
        Assert.DoesNotContain("business252", provider.Resolved);
    }
}
