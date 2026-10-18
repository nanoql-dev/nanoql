// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// C# lets any integer be cast to an enum. An enum argument a member always refuses, a value outside its type or a
/// member it never handles, throws <see cref="ArgumentOutOfRangeException"/> naming the parameter, checked on entry by
/// every public member that switches on it, stores it or computes with it; where QuantLib fails on such an argument
/// with <c>QL_FAIL</c>, nanoql's check takes its place. A value outside its type can't be stored, so the
/// <see cref="NanoQLException"/> branches on stored values fail only for members an operation doesn't handle.
/// </summary>
public class UnknownEnumValueTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void CalendarsAndDayCountersRefuseAnUnknownMarketRuleOrConvention()
    {
        AssertParam("market", () => new UnitedStates((UnitedStatesMarket)99));
        AssertParam("market", () => new UnitedKingdom((UnitedKingdomMarket)99));
        AssertParam("market", () => new Canada((CanadaMarket)99));
        AssertParam("r", () => new JointCalendar(new TARGET(), new TARGET(), (JointCalendarRule)99));
        AssertParam("r", () => new JointCalendar([new TARGET()], (JointCalendarRule)99));
        AssertParam("c", () => new Actual365Fixed((Actual365FixedConvention)99));
        AssertParam("c", () => new Thirty360((Thirty360Convention)99));
        AssertParam("c", () => new ActualActual((ActualActualConvention)99));
    }

    [Fact]
    public void DatesPeriodsAndCalendarMovesRefuseAnUnknownValue()
    {
        Calendar target = new TARGET();

        AssertParam("units", () => new Period(1, (TimeUnit)99));
        AssertParam("units", () => _ = (TimeUnit)99 * 1);
        AssertParam("f", () => new Period((Frequency)99));
        AssertParam("f", () => new Period(Frequency.OtherFrequency));
        AssertParam("c", () => target.Adjust(Today, (BusinessDayConvention)99));
        AssertParam("unit", () => target.Advance(Today, 1, (TimeUnit)99));
        AssertParam("c", () => target.Advance(Today, 1, TimeUnit.Days, (BusinessDayConvention)99));
        AssertParam("c", () => target.Advance(Today, new Period(1, TimeUnit.Months), (BusinessDayConvention)99));
        AssertParam("m", () => DateFunctions.ToString((Month)13));
        AssertParam("f", () => FrequencyFunctions.ToString((Frequency)99));
        AssertParam("w", () => Date.NextWeekday(Today, (Weekday)9));
        AssertParam("w", () => Date.NthWeekday(1, (Weekday)0, Month.January, 2026));
    }

    [Fact]
    public void SchedulesAndTheirBuilderRefuseAnUnknownConventionRuleOrFrequency()
    {
        Date end = Today + new Period(2, TimeUnit.Years);
        var tenor = new Period(6, TimeUnit.Months);
        Calendar target = new TARGET();
        const BusinessDayConvention following = BusinessDayConvention.Following;
        const DateGenerationRule backward = DateGenerationRule.Backward;
        var unknownConvention = (BusinessDayConvention)99;

        AssertParam("convention", () => new Schedule(
            Today, end, tenor, target, unknownConvention, following, backward, false, Context()));
        AssertParam("terminationDateConvention", () => new Schedule(
            Today, end, tenor, target, following, unknownConvention, backward, false, Context()));
        AssertParam("rule", () => new Schedule(
            Today, end, tenor, target, following, following, (DateGenerationRule)99, false, Context()));

        // With a null effective date, the rule decides where it's placed, so it's checked before that.
        AssertParam("rule", () => new Schedule(
            null, end, tenor, target, following, following, (DateGenerationRule)99, false, Context()));
        AssertParam("convention", () => new Schedule([Today, end], target, unknownConvention));
        AssertParam("terminationDateConvention", () => new Schedule([Today, end], target, following, unknownConvention));
        AssertParam("rule", () => new Schedule([Today, end], target, following, null, null, (DateGenerationRule)99));

        var builder = new MakeSchedule().From(Today).To(end);
        AssertParam("frequency", () => builder.WithFrequency((Frequency)99));
        AssertParam("frequency", () => builder.WithFrequency(Frequency.OtherFrequency));
        AssertParam("conv", () => builder.WithConvention(unknownConvention));
        AssertParam("conv", () => builder.WithTerminationDateConvention(unknownConvention));
        AssertParam("r", () => builder.WithRule((DateGenerationRule)99));
    }

    [Fact]
    public void RatesCurvesAndLegsRefuseAnUnknownCompoundingFrequencyOrConvention()
    {
        DayCounter dc = new Actual360();
        var rate = new InterestRate(0.03, dc, Compounding.Simple, Frequency.Annual);
        var curve = new FlatForward(Today, 0.03, new Actual365Fixed());

        AssertParam("comp", () => new InterestRate(0.03, dc, (Compounding)99, Frequency.Annual));
        AssertParam("freq", () => new InterestRate(0.03, dc, Compounding.Simple, (Frequency)99));
        AssertParam("comp", () => InterestRate.ImpliedRate(1.05, dc, (Compounding)99, Frequency.Annual, 1.0));
        AssertParam("freq", () => InterestRate.ImpliedRate(1.05, dc, Compounding.Compounded, (Frequency)99, 1.0));
        AssertParam("comp", () => rate.EquivalentRate((Compounding)99, Frequency.Annual, 1.0));
        AssertParam("comp", () => curve.ZeroRate(1.0, (Compounding)99));
        AssertParam("freq", () => curve.ForwardRate(Today + 30, Today + 365, dc, Compounding.Compounded, (Frequency)99));
        AssertParam("compounding", () => new FlatForward(Today, 0.03, dc, (Compounding)99));
        AssertParam("frequency", () => new FlatForward(Today, 0.03, dc, Compounding.Compounded, (Frequency)99));
        AssertParam("compounding", () => FlatForward.Create(2, new TARGET(), 0.03, dc, Context(), (Compounding)99));

        Schedule schedule = new MakeSchedule().From(Today).To(Today + 730).WithFrequency(Frequency.Annual).ToSchedule();
        var leg = new FixedRateLeg(schedule);
        AssertParam("convention", () => leg.WithPaymentAdjustment((BusinessDayConvention)99));
        AssertParam("convention", () => leg.WithExCouponPeriod(
            new Period(5, TimeUnit.Days), new TARGET(), (BusinessDayConvention)99));
        AssertParam("comp", () => leg.WithCouponRates(0.03, dc, (Compounding)99));
    }

    [Fact]
    public void WeekdayAndConventionTextRefusesAnUnknownValue()
    {
        AssertParam("d", () => WeekdayFunctions.LongWeekday((Weekday)8));
        AssertParam("d", () => WeekdayFunctions.ShortWeekday((Weekday)0));
        AssertParam("d", () => WeekdayFunctions.ShortestWeekday((Weekday)8));
        AssertParam("w", () => WeekdayFunctions.ToString((Weekday)0));
        AssertParam("b", () => BusinessDayConventionFunctions.ToString((BusinessDayConvention)7));
        AssertParam("b", () => BusinessDayConventionFunctions.ToString((BusinessDayConvention)(-1)));
    }

    [Fact]
    public void CubicInterpolationsAndTheirFactoriesRefuseAnUnknownSchemeOrCondition()
    {
        double[] x = [0.0, 1.0, 2.0], y = [1.0, 2.0, 1.5];
        const CubicInterpolationDerivativeApprox spline = CubicInterpolationDerivativeApprox.Spline;
        const CubicInterpolationBoundaryCondition second = CubicInterpolationBoundaryCondition.SecondDerivative;
        var unknownScheme = (CubicInterpolationDerivativeApprox)99;
        var unknownCondition = (CubicInterpolationBoundaryCondition)(-1);

        // QuantLib reads the values only where it uses them (a local scheme on two points never switches on its
        // scheme); here each constructor checks them on entry, since it stores them.
        AssertParam("da", () => new CubicInterpolation(x[..2], y[..2], unknownScheme, false, second, 0.0, second, 0.0));
        AssertParam("leftCond", () => new CubicInterpolation(x, y, spline, false, unknownCondition, 0.0, second, 0.0));
        AssertParam("rightCond", () => new CubicInterpolation(x, y, spline, false, second, 0.0, unknownCondition, 0.0));
        AssertParam("da", () => new LogCubicInterpolation(x, y, unknownScheme, false, second, 0.0, second, 0.0));
        AssertParam("leftC", () => new LogCubicInterpolation(x, y, spline, false, unknownCondition, 0.0, second, 0.0));
        AssertParam("rightC", () => new LogCubicInterpolation(x, y, spline, false, second, 0.0, unknownCondition, 0.0));
        AssertParam("da", () => new Cubic(unknownScheme));
        AssertParam("leftCondition", () => new Cubic(spline, false, unknownCondition));
        AssertParam("rightCondition", () => new Cubic(spline, false, second, 0.0, unknownCondition));
        AssertParam("da", () => new LogCubic(unknownScheme));
        AssertParam("leftCondition", () => new LogCubic(spline, true, unknownCondition));
        AssertParam("rightCondition", () => new LogCubic(spline, true, second, 0.0, unknownCondition));
    }

    private static EvaluationContext Context() => new() { EvaluationDate = Today };

    private static void AssertParam(string paramName, Action action) =>
        Assert.Equal(paramName, Assert.Throws<ArgumentOutOfRangeException>(action).ParamName);
}
