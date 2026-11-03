// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="Schedule"/>'s indexer, QuantLib's unchecked <c>operator[]</c> beside the checked <c>at</c>: an index past
/// the dates, or a negative one, is refused with Array::at's text, as <see cref="Schedule.At"/> refuses it.
/// </summary>
public class ScheduleIndexerTests
{
    [Fact]
    public void AnIndexOutsideTheDatesIsRefused()
    {
        var schedule = new Schedule([new Date(15, Month.October, 2026), new Date(15, Month.April, 2027), new Date(15, Month.October, 2027)]);
        foreach (int i in new[] { 3, -1 })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => schedule[i]);
            Assert.Equal("i", e.ParamName);
            Assert.StartsWith($"index ({i}) must be less than 3: array access out of range", e.Message, StringComparison.Ordinal);
            Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => schedule.At(i)).ParamName);
        }

        Assert.Equal(schedule.At(2), schedule[2]);
    }
}
