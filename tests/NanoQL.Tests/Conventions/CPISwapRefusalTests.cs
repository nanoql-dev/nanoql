// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="CPISwap"/>'s schedule refusals (<c>cpiswap.cpp:66-67</c>): an empty floating or fixed schedule is an
/// <see cref="ArgumentException"/> naming it, with QuantLib's "empty float schedule" and "empty fixed schedule".
/// </summary>
public class CPISwapRefusalTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.June, 2024) };

    private static readonly Schedule OneDate = new([new Date(15, Month.June, 2029)], new UnitedKingdom());

    private static readonly Schedule Empty = new([]);

    [Fact]
    public void AnEmptyFloatScheduleIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => Swap(Empty, OneDate));
        Assert.Equal("floatSchedule", e.ParamName);
        Assert.StartsWith("empty float schedule", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyFixedScheduleIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => Swap(OneDate, Empty));
        Assert.Equal("fixedSchedule", e.ParamName);
        Assert.StartsWith("empty fixed schedule", e.Message, StringComparison.Ordinal);
    }

    private static CPISwap Swap(Schedule floatSchedule, Schedule fixedSchedule) =>
        new(
            SwapType.Payer,
            1e6,
            true,
            0.0,
            new Actual365Fixed(),
            floatSchedule,
            BusinessDayConvention.ModifiedFollowing,
            0,
            null,
            0.02,
            305.0,
            new Actual365Fixed(),
            fixedSchedule,
            BusinessDayConvention.ModifiedFollowing,
            new Period(3, TimeUnit.Months),
            new UKRPI(),
            Context);
}
