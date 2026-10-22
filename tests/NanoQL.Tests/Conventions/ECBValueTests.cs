// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="ECB"/>'s known dates are a value: <see cref="ECB.AddDate"/> and <see cref="ECB.RemoveDate"/> return a new
/// value and leave the receiver unchanged, where QuantLib's <c>addDate</c> and <c>removeDate</c> change one static set
/// (<c>ecb.cpp:164-170</c>). Two values are equal, and hash alike, when their dates are. A member whose date defaults
/// to QuantLib's <c>Date()</c> reads the context's evaluation date where the date is <see langword="null"/>.
/// </summary>
public class ECBValueTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2014) };

    [Fact]
    public void AddDateAndRemoveDateReturnCopies()
    {
        ECB ecb = ECB.Default;
        Date first = ecb.KnownDates.Min;
        Date extra = new(14, Month.January, 2026);

        ECB removed = ecb.RemoveDate(first);
        ECB added = ecb.AddDate(extra);

        Assert.True(ecb.IsECBdate(first));
        Assert.Equal(200, ecb.KnownDates.Count);
        Assert.False(removed.IsECBdate(first));
        Assert.Equal(199, removed.KnownDates.Count);
        Assert.Equal(201, added.KnownDates.Count);
        Assert.True(added.IsECBdate(extra));
        Assert.Equal(extra, added.NextDate(Context, new Date(18, Month.December, 2024)));
        Assert.Throws<NanoQLException>(() => ecb.NextDate(Context, new Date(18, Month.December, 2024)));
        Assert.Same(ECB.Default, ecb);
    }

    [Fact]
    public void ValuesAreEqualAndHashAlikeByTheirDates()
    {
        Date first = ECB.Default.KnownDates.Min;
        ECB roundTrip = ECB.Default.RemoveDate(first).AddDate(first);

        Assert.True(ECB.Default.Equals(roundTrip));
        Assert.True(ECB.Default.Equals((object)roundTrip));
        Assert.Equal(ECB.Default.GetHashCode(), roundTrip.GetHashCode());
        Assert.False(ECB.Default.Equals(ECB.Default.RemoveDate(first)));
        Assert.False(ECB.Default.Equals(null));

        // adding a known date, or removing an unknown one, changes nothing
        Assert.True(ECB.Default.Equals(ECB.Default.AddDate(first)));
        Assert.True(ECB.Default.Equals(ECB.Default.RemoveDate(new Date(2, Month.January, 2005))));
    }

    [Fact]
    public void ANullDateReadsTheContextsEvaluationDate()
    {
        ECB ecb = ECB.Default;
        Date today = Context.EvaluationDate;

        Assert.Equal(ecb.NextDate(Context, today), ecb.NextDate(Context));
        Assert.Equal(ecb.NextDates(Context, today), ecb.NextDates(Context));
        Assert.Equal(ecb.NextCode(Context, today), ecb.NextCode(Context));

        // a code's century is the evaluation date's
        Assert.Equal(ecb.Date("MAR13", Context, today), ecb.Date("MAR13", Context));
        Assert.Equal(new Date(13, Month.March, 2013), ecb.Date("MAR13", Context));
        Assert.Equal(ecb.NextDate("MAR13", Context, today), ecb.NextDate("MAR13", Context));
        Assert.Equal(ecb.NextDates("MAR13", Context, today), ecb.NextDates("MAR13", Context));

        // a date given is read instead
        var other = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2010) };
        Assert.Equal(ecb.NextDate(Context, today), ecb.NextDate(other, today));
    }
}
