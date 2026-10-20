// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A credit default swap on an empty schedule with no protection start. QuantLib's constructors initialize the
/// protection start to <c>schedule[0]</c> before <c>init</c> checks the schedule (<c>creditdefaultswap.cpp:56, 81</c>),
/// and <c>Schedule::operator[]</c> reads <c>dates_[i]</c> unchecked unless <c>QL_EXTRA_SAFETY_CHECKS</c> is defined
/// (<c>schedule.hpp:169-173</c>): an empty vector's first element, which is undefined. On the wheel, both constructors
/// end the process with an access violation (exit code 0xC0000005); given a protection start, both refuse with
/// <c>init</c>'s "CreditDefaultSwap needs a non-empty schedule.". nanoql checks the schedule first, and refuses with
/// that text, an <see cref="ArgumentException"/> naming <c>schedule</c>, whether or not a protection start is given.
/// </summary>
public class CdsEmptyScheduleTests
{
    [Fact]
    public void AnEmptyScheduleIsRefusedBeforeItsFirstDateIsRead()
    {
        var empty = new Schedule([], new TARGET());
        Action[] builds =
        [
            // QuantLib's: an access violation, from reading the empty schedule's first date
            () => _ = new CreditDefaultSwap(ProtectionSide.Buyer, 1e6, 0.01, empty, BusinessDayConvention.Following, new Actual360()),
            () => _ = new CreditDefaultSwap(ProtectionSide.Buyer, 1e6, 0.02, 0.01, empty, BusinessDayConvention.Following, new Actual360()),
        ];

        foreach (Action build in builds)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(build);
            Assert.Equal("schedule", e.ParamName);
            Assert.StartsWith("CreditDefaultSwap needs a non-empty schedule.", e.Message, StringComparison.Ordinal);
        }
    }
}
