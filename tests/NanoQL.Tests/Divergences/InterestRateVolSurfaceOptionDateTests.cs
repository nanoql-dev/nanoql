// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The interest-rate volatility surface's option dates. QuantLib's <c>InterestRateVolSurface::optionDateFromTenor</c>,
/// the index's fixing date, hides its base's non-virtual member, so <c>atmVol(Period)</c> and
/// <c>smileSection(Period)</c>, defined on the bases, date the option on the calendar while the surface's own option
/// dates and volatility spreads use the index's (<c>interestratevolsurface.hpp:69</c>,
/// <c>blackatmvolcurve.cpp:40-44</c>); nanoql dates every tenor on the surface by the index. Checks: a tenor's
/// at-the-money volatility and smile section are those at the index's date; at one month, where the calendar's date (15
/// June 2026) and the index's (17 June) differ, the calendar date's volatility differs.
/// </summary>
public class InterestRateVolSurfaceOptionDateTests
{
    [Fact]
    public void ATenorIsDatedByTheIndex()
    {
        (SabrVolSurface surface, _) = SabrVolSurfaceFixingTests.Surface();
        foreach (Period p in new Period[] { new(3, TimeUnit.Months), new(6, TimeUnit.Months), new(1, TimeUnit.Years) })
        {
            Date indexDate = surface.OptionDateFromTenor(p);
            Assert.Equal(surface.AtmVol(indexDate), surface.AtmVol(p));
            Assert.Equal(surface.SmileSection(indexDate, true).Volatility(0.03), surface.SmileSection(p, true).Volatility(0.03));
        }
    }

    [Fact]
    public void WhereTheCalendarsDateDiffersTheIndexsStands()
    {
        (SabrVolSurface surface, _) = SabrVolSurfaceFixingTests.Surface();
        var oneMonth = new Period(1, TimeUnit.Months);
        Date indexDate = surface.OptionDateFromTenor(oneMonth);
        Date calendarDate = ((VolatilityTermStructure)surface).OptionDateFromTenor(oneMonth);
        Assert.Equal(new Date(17, Month.June, 2026), indexDate);
        Assert.Equal(new Date(15, Month.June, 2026), calendarDate);
        Assert.Equal(surface.AtmVol(indexDate), surface.AtmVol(oneMonth));
        Assert.NotEqual(surface.AtmVol(calendarDate), surface.AtmVol(oneMonth));
    }
}
