// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Every public member that takes the evaluation context throws <see cref="ArgumentNullException"/> for a null one,
/// before anything else. The context sits after QuantLib's parameters without defaults and before those with one.
/// </summary>
public class ContextChecksTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2026) };

    private static readonly Schedule Annual = new(
        new Date(15, Month.January, 2026),
        new Date(15, Month.January, 2030),
        new Period(1, TimeUnit.Years),
        new TARGET(),
        BusinessDayConvention.Following,
        BusinessDayConvention.Following,
        DateGenerationRule.Backward,
        false,
        Context);

    [Fact]
    public void ScheduleMembersRefuseANullContext()
    {
        // With an effective date given, the constructor never reads the context, and still refuses a null one.
        AssertContext(() => new Schedule(
            new Date(15, Month.January, 2026),
            new Date(15, Month.January, 2030),
            new Period(1, TimeUnit.Years),
            new TARGET(),
            BusinessDayConvention.Following,
            BusinessDayConvention.Following,
            DateGenerationRule.Backward,
            false,
            null!));
        AssertContext(() => Annual.LowerBound(null!, new Date(1, Month.March, 2027)));
        AssertContext(() => Annual.NextDate(new Date(1, Month.March, 2027), null!));
        AssertContext(() => Annual.PreviousDate(new Date(1, Month.March, 2027), null!));
    }

    [Fact]
    public void AMissingReferenceDateIsTheEvaluationDate()
    {
        // QuantLib's lower_bound(const Date& d = Date()) defaults its date, so the context comes first and the date
        // keeps its default; nextDate(const Date& refDate) and previousDate(const Date& refDate) have none, so the
        // date comes first. Each reads the evaluation date for a null date.
        Assert.Equal(Annual.LowerBound(Context, Context.EvaluationDate), Annual.LowerBound(Context));
        Assert.Equal(new Date(15, Month.January, 2027), Annual.NextDate(null, Context));
        Assert.Equal(new Date(15, Month.January, 2026), Annual.PreviousDate(null, Context));
    }

    private static void AssertContext(Action action) =>
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(action).ParamName);
}
