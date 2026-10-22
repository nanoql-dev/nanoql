// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
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
        AssertParam("market", () => new Brazil((BrazilMarket)99));
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
        AssertParam("w", () => new BespokeCalendar().AddWeekend((Weekday)9));
        AssertParam("w", () => new BespokeCalendar().AddWeekend((Weekday)0));
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

        ImmutableArray<CashFlow> cashFlows = leg.WithNotionals(100.0).WithCouponRates(0.03, dc).ToLeg();
        EvaluationContext context = Context();
        AssertParam("compounding", () => CashFlows.Npv(cashFlows, 0.03, dc, (Compounding)99, Frequency.Annual, context));
        AssertParam("frequency", () => CashFlows.Npv(cashFlows, 0.03, dc, Compounding.Compounded, (Frequency)99, context));
        AssertParam("type", () => CashFlows.Duration(cashFlows, rate, (DurationType)99, context));
        AssertParam("compounding", () => CashFlows.Yield(cashFlows, 0.0, dc, (Compounding)99, Frequency.Annual, context));
        AssertParam("frequency", () => CashFlows.Yield(cashFlows, 0.0, dc, Compounding.Compounded, (Frequency)99, context));
        AssertParam("compounding", () => CashFlows.ZSpread(cashFlows, 0.0, curve, (Compounding)99, Frequency.Annual, context));
        AssertParam("frequency", () => CashFlows.Npv(cashFlows, curve, 0.01, Compounding.Continuous, (Frequency)99, context));
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new DigitalReplication((ReplicationType)99));
        Assert.Equal("t", e.ParamName);
        Assert.StartsWith("unknown Replication Type (99)", e.Message, StringComparison.Ordinal);

        // The bonds: a price type is stored or switched on, and the conventions passed to the leg builders and the
        // calendar under other names are checked under the bonds' own.
        ArgumentOutOfRangeException price = Assert.Throws<ArgumentOutOfRangeException>(
            () => new BondPrice(98.0, (BondPriceType)7));
        Assert.Equal("type", price.ParamName);
        Assert.StartsWith("unknown price type", price.Message, StringComparison.Ordinal);
        var bond = new FixedRateBond(2, 100.0, schedule, [0.03], dc);
        AssertParam("priceType", () => new BondHelper(100.0, bond, context, (BondPriceType)7));
        AssertParam("priceType", () => new FixedRateBondHelper(
            100.0, 2, 100.0, schedule, [0.03], dc, context, priceType: (BondPriceType)7));
        AssertParam("paymentConv", () => new FixedRateBondHelper(
            100.0, 2, 100.0, schedule, [0.03], dc, context, (BusinessDayConvention)99));
        AssertParam("priceType", () => bond.Yield(
            DiscountingBondEngine.Calculate(bond, context, curve), dc, Compounding.Compounded, Frequency.Annual, context,
            priceType: (BondPriceType)7));
        AssertParam("paymentConvention", () => new FixedRateBond(2, 100.0, schedule, [0.03], dc, (BusinessDayConvention)99));
        AssertParam("exCouponConvention", () => new FixedRateBond(
            2, 100.0, schedule, [0.03], dc, exCouponConvention: (BusinessDayConvention)99));
        AssertParam("paymentConvention", () => new ZeroCouponBond(
            2, new TARGET(), 100.0, Today + 730, (BusinessDayConvention)99));
        AssertParam("paymentConvention", () => new AmortizingFixedRateBond(
            2, [100.0], schedule, [0.03], dc, (BusinessDayConvention)99));
        AssertParam("type", () => BondFunctions.Duration(bond, rate, context, (DurationType)99));
        AssertParam("frequency", () => AmortizingFixedRateBondFunctions.SinkingSchedule(
            Today, new Period(5, TimeUnit.Years), (Frequency)99, new TARGET()));
        AssertParam("frequency", () => AmortizingFixedRateBondFunctions.SinkingNotionals(
            new Period(5, TimeUnit.Years), Frequency.OtherFrequency, 0.03, 100.0));
        AssertParam("bondLength", () => AmortizingFixedRateBondFunctions.SinkingNotionals(
            new Period(5, TimeUnit.Hours), Frequency.Annual, 0.03, 100.0));
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

    [Fact]
    public void TheTqrDecompositionRefusesAnUnknownCalculationOrStrategy()
    {
        // QuantLib reads an unknown calculation as OnlyFirstRowEigenVector and an unknown strategy as a shift
        var diag = new Vector([1.0, 2.0]);
        var sub = new Vector([0.5]);
        AssertParam("calc", () => new TqrEigenDecomposition(
            diag, sub, (TqrEigenDecompositionEigenVectorCalculation)3, TqrEigenDecompositionShiftStrategy.NoShift));
        AssertParam("strategy", () => new TqrEigenDecomposition(
            diag, sub, TqrEigenDecompositionEigenVectorCalculation.WithEigenVector, (TqrEigenDecompositionShiftStrategy)(-1)));
    }

    private static EvaluationContext Context() => new() { EvaluationDate = Today };

    private static void AssertParam(string paramName, Action action) =>
        Assert.Equal(paramName, Assert.Throws<ArgumentOutOfRangeException>(action).ParamName);
}
