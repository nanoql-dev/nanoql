// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of the swaps and their builders, each with its type, message and parameter, which a vector's
/// <c>{"throws": true}</c> can't tell apart: a QuantLib check of a stored value throws <see cref="NanoQLException"/>,
/// and a check of an argument an <see cref="ArgumentException"/> naming it, an
/// <see cref="ArgumentOutOfRangeException"/> for a value outside its range and an <see cref="ArgumentNullException"/>
/// for a null one.
/// </summary>
public class SwapErrorTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly FlatForward Curve = new(Today, 0.03, new Actual365Fixed());

    private static Schedule Annual(Date start, int years, Calendar calendar) => new(
        start, start + new Period(years, TimeUnit.Years), new Period(1, TimeUnit.Years), calendar,
        BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward,
        false, Context);

    [Fact]
    public void TheSwapsRefusals()
    {
        var estr = new Estr(Curve);
        Schedule schedule = Annual(new Date(17, Month.January, 2025), 2, new TARGET());

        // The nominal and the nominals, stored values QuantLib checks.
        var amortizing = new OvernightIndexedSwap(SwapType.Payer, [1e6, 5e5], schedule, 0.03, new Actual360(), estr);
        NanoQLException e = Assert.Throws<NanoQLException>(() => amortizing.Nominal());
        Assert.Equal("nominal is not constant", e.Message);
        var different = new OvernightIndexedSwap(
            SwapType.Payer, [1e6], schedule, 0.03, new Actual360(), [2e6], schedule, estr);
        e = Assert.Throws<NanoQLException>(() => different.Nominals());
        Assert.Equal("different nominals on fixed and floating leg", e.Message);
        e = Assert.Throws<NanoQLException>(() => different.Nominal());
        Assert.Equal("nominal is not constant", e.Message);

        // The rounding check, an argument outside its range (overnightindexedswap.cpp:177-181).
        ArgumentOutOfRangeException r = Assert.Throws<ArgumentOutOfRangeException>(() => new OvernightIndexedSwap(
            SwapType.Payer, 1e6, schedule, 0.03, new Actual360(), estr, roundingPrecision: 17));
        Assert.Equal("roundingPrecision", r.ParamName);
        Assert.StartsWith("rounding precision (17) must be between 0 and 16", r.Message, StringComparison.Ordinal);

        // An empty schedule, refused by the conversion of the leg built on it, as QuantLib's legs reach it: the
        // undefined-path refusal of a value the leg builder stores (EmptyScheduleTests).
        var empty = new Schedule(Array.Empty<Date>(), new TARGET());
        var euribor = new Euribor6M(Curve);
        Func<object>[] emptySchedules =
        [
            () => new OvernightIndexedSwap(SwapType.Payer, 1e6, empty, 0.03, null, estr),
            () => new OvernightIndexedSwap(SwapType.Payer, [1e6], empty, 0.03, null, estr),
            () => new OvernightIndexedSwap(SwapType.Payer, 1e6, empty, 0.03, null, schedule, estr),
            () => new OvernightIndexedSwap(SwapType.Payer, 1e6, schedule, 0.03, null, empty, estr),
            () => new OvernightIndexedSwap(SwapType.Payer, [1e6], empty, 0.03, null, [1e6], schedule, estr),
            () => new OvernightIndexedSwap(SwapType.Payer, [1e6], schedule, 0.03, null, [1e6], empty, estr),
            () => new VanillaSwap(SwapType.Payer, 1e6, empty, 0.03, null, schedule, euribor, 0.0, null, Context),
            () => new VanillaSwap(SwapType.Payer, 1e6, schedule, 0.03, null, empty, euribor, 0.0, null, Context),
            () => new BuiltLegSwap(empty, schedule, euribor, () => []),
        ];
        foreach (Func<object> build in emptySchedules)
        {
            Assert.Equal("empty schedule", Assert.Throws<NanoQLException>(build).Message);
        }

        // A null index, with QuantLib's text, and a type outside the enum.
        ArgumentNullException n = Assert.Throws<ArgumentNullException>(() => new OvernightIndexedSwap(
            SwapType.Payer, 1e6, schedule, 0.03, new Actual360(), null!));
        Assert.Equal("overnightIndex", n.ParamName);
        Assert.StartsWith("null floating index provided", n.Message, StringComparison.Ordinal);
        n = Assert.Throws<ArgumentNullException>(() => new VanillaSwap(
            SwapType.Payer, 1e6, schedule, 0.03, null, schedule, null!, 0.0, null, Context));
        Assert.Equal("iborIndex", n.ParamName);
        Assert.StartsWith("null floating index provided", n.Message, StringComparison.Ordinal);
        r = Assert.Throws<ArgumentOutOfRangeException>(() => new VanillaSwap(
            (SwapType)0, 1e6, schedule, 0.03, null, schedule, new Euribor6M(Curve), 0.0, null, Context));
        Assert.Equal("type", r.ParamName);
        Assert.StartsWith("Unknown vanilla-swap type", r.Message, StringComparison.Ordinal);

        // The context, which the vanilla swap's floating leg reads.
        n = Assert.Throws<ArgumentNullException>(() => new VanillaSwap(
            SwapType.Payer, 1e6, schedule, 0.03, null, schedule, new Euribor6M(Curve), 0.0, null, null!));
        Assert.Equal("context", n.ParamName);
    }

    [Fact]
    public void TheBaseRefusesBeforeTheFloatingLegIsBuilt()
    {
        // QuantLib's base constructor checks its terms and builds the fixed leg (fixedvsfloatingswap.cpp:53-84) before
        // the derived constructor's body builds the floating leg (vanillaswap.cpp:41-50,
        // overnightindexedswap.cpp:153-183): with a type outside the enum and a floating leg that can't be built, the
        // type is refused.
        Schedule schedule = Annual(new Date(17, Month.January, 2025), 2, new TARGET());

        // A one-date floating schedule, which only the Ibor leg's build refuses.
        var oneDate = new Schedule([new Date(17, Month.January, 2025)], new TARGET());
        NanoQLException e = Assert.Throws<NanoQLException>(() => new VanillaSwap(
            SwapType.Payer, 1e6, schedule, 0.03, null, oneDate, new Euribor6M(Curve), 0.0, null, Context));
        Assert.Equal("too many nominals (1), only 0 required", e.Message);
        ArgumentOutOfRangeException r = Assert.Throws<ArgumentOutOfRangeException>(() => new VanillaSwap(
            (SwapType)0, 1e6, schedule, 0.03, null, oneDate, new Euribor6M(Curve), 0.0, null, Context));
        Assert.Equal("type", r.ParamName);
        Assert.StartsWith("Unknown vanilla-swap type", r.Message, StringComparison.Ordinal);

        // A rounding precision outside [0, 16], which only the overnight leg's build refuses.
        r = Assert.Throws<ArgumentOutOfRangeException>(() => new OvernightIndexedSwap(
            (SwapType)0, 1e6, schedule, 0.03, new Actual360(), new Estr(Curve), roundingPrecision: 17));
        Assert.Equal("type", r.ParamName);
        Assert.StartsWith("Unknown vanilla-swap type", r.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EachRefusalNamesTheConstructorsOwnParameter()
    {
        var estr = new Estr(Curve);
        var euribor = new Euribor6M(Curve);
        Schedule schedule = Annual(new Date(17, Month.January, 2025), 2, new TARGET());
        const BusinessDayConvention Unknown = (BusinessDayConvention)99;

        // The payment adjustment, which the base checks as its payment convention, in each constructor.
        const string UnknownConvention = "unknown business-day convention";
        AssertRefused<ArgumentOutOfRangeException>(
            () => _ = new OvernightIndexedSwap(
                SwapType.Payer, 1e6, schedule, 0.03, null, estr, paymentAdjustment: Unknown),
            "paymentAdjustment",
            UnknownConvention);
        AssertRefused<ArgumentOutOfRangeException>(
            () => _ = new OvernightIndexedSwap(
                SwapType.Payer, [1e6], schedule, 0.03, null, estr, paymentAdjustment: Unknown),
            "paymentAdjustment",
            UnknownConvention);
        AssertRefused<ArgumentOutOfRangeException>(
            () => _ = new OvernightIndexedSwap(
                SwapType.Payer, 1e6, schedule, 0.03, null, schedule, estr, paymentAdjustment: Unknown),
            "paymentAdjustment",
            UnknownConvention);
        AssertRefused<ArgumentOutOfRangeException>(
            () => _ = new OvernightIndexedSwap(
                SwapType.Payer, [1e6], schedule, 0.03, null, [1e6], schedule, estr, paymentAdjustment: Unknown),
            "paymentAdjustment",
            UnknownConvention);
        AssertRefused<ArgumentOutOfRangeException>(
            () => _ = new VanillaSwap(
                SwapType.Payer, 1e6, schedule, 0.03, null, schedule, euribor, 0.0, null, Context, Unknown),
            "paymentConvention",
            UnknownConvention);

        // A null schedule in the one-schedule constructors.
        AssertRefused<ArgumentNullException>(
            () => _ = new OvernightIndexedSwap(SwapType.Payer, 1e6, null!, 0.03, null, estr),
            "schedule",
            "Value cannot be null.");
        AssertRefused<ArgumentNullException>(
            () => _ = new OvernightIndexedSwap(SwapType.Payer, [1e6], null!, 0.03, null, estr),
            "schedule",
            "Value cannot be null.");

        // The base's own constructor: a floating leg the swap base refuses.
        AssertRefused<ArgumentException>(
            () => _ = new BuiltLegSwap(schedule, schedule, euribor, () => default),
            "floatingLeg",
            "default(ImmutableArray) is not a leg; an empty leg is []");
        AssertRefused<ArgumentException>(
            () => _ = new BuiltLegSwap(schedule, schedule, euribor, () => [null!]),
            "floatingLeg",
            "null cash flow in leg");
    }

    [Fact]
    public void ACalleesRefusalOfAComputedValueReachesTheCallerAsRaised()
    {
        // A value the swap or its builder computes from its own arguments or settings, refused by the callee it is
        // given to, with QuantLib's text and the callee's exception type and parameter.
        var estr = new Estr(Curve);
        Schedule schedule = Annual(new Date(17, Month.January, 2025), 2, new TARGET());

        // A payment date before the accrual end, from a negative payment lag: the overnight coupon's refusal
        // (overnightindexedcoupon.cpp:87-88), from the swap and from its builder.
        const string EarlyPayment = "Payment date cannot be earlier than accrual end date";
        AssertRefused<ArgumentException>(
            () => _ = new OvernightIndexedSwap(SwapType.Payer, 1e6, schedule, 0.03, null, estr, paymentLag: -5),
            "paymentDate",
            EarlyPayment);
        AssertRefused<ArgumentException>(
            () => _ = new MakeOIS(new Period(2, TimeUnit.Years), estr)
                .WithPaymentLag(-5)
                .ToOvernightIndexedSwap(Context),
            "paymentDate",
            EarlyPayment);

        // The fixed schedule the builder builds on a negative tenor: the schedule's refusal (schedule.cpp:119-120).
        AssertRefused<ArgumentException>(
            () => _ = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(Curve))
                .WithFixedLegTenor(new Period(-1, TimeUnit.Years))
                .ToVanillaSwap(Context),
            "tenor",
            "non positive tenor (-1Y) not allowed");

        // An overnight schedule whose dates are out of order: the coupon's refusal of the accrual dates it is given
        // (overnightindexedcoupon.cpp:86).
        var outOfOrder = new Schedule(
            [new Date(19, Month.January, 2026), new Date(17, Month.January, 2025)], new TARGET());
        AssertRefused<ArgumentException>(
            () => _ = new OvernightIndexedSwap(SwapType.Payer, 1e6, schedule, 0.03, null, outOfOrder, estr),
            "startDate",
            "startDate must be less than endDate");
    }

    [Fact]
    public void TheBuildersRefusals()
    {
        var euribor = new Euribor6M(Curve);
        var estr = new Estr(Curve);
        var fiveYears = new Period(5, TimeUnit.Years);

        // An effective date with settlement days, stored values QuantLib's conversion checks
        // (makevanillaswap.cpp:65-67, makeois.cpp:56-58).
        const string Both = "cannot set both an explicit effective date and settlement days; use one or the other";
        NanoQLException e = Assert.Throws<NanoQLException>(() => new MakeVanillaSwap(fiveYears, euribor)
            .WithSettlementDays(2).WithEffectiveDate(new Date(20, Month.January, 2025)).ToVanillaSwap(Context));
        Assert.Equal(Both, e.Message);
        e = Assert.Throws<NanoQLException>(() => new MakeOIS(fiveYears, estr)
            .WithEffectiveDate(new Date(20, Month.January, 2025))
            .WithSettlementDays(2)
            .ToOvernightIndexedSwap(Context));
        Assert.Equal(Both, e.Message);

        // The fixed leg's defaults for the index's currency, a null one written as QuantLib writes it.
        var cad = new IborIndex(
            "CadIbor", new Period(3, TimeUnit.Months), 0, new CADCurrency(), new TARGET(),
            BusinessDayConvention.ModifiedFollowing, false, new Actual365Fixed(), Curve);
        e = Assert.Throws<NanoQLException>(() => new MakeVanillaSwap(fiveYears, cad).ToVanillaSwap(Context));
        Assert.Equal("unknown fixed leg default tenor for CAD", e.Message);
        var noCurrency = new IborIndex(
            "NulIbor", new Period(3, TimeUnit.Months), 0, null, new TARGET(), BusinessDayConvention.ModifiedFollowing,
            false, new Actual365Fixed(), Curve);
        e = Assert.Throws<NanoQLException>(() => new MakeVanillaSwap(fiveYears, noCurrency)
            .WithFixedLegTenor(new Period(1, TimeUnit.Years)).ToVanillaSwap(Context));
        Assert.Equal("unknown fixed leg day counter for null currency", e.Message);

        // No fixed rate with neither an engine nor a curve, and a par rate QuantLib leaves null.
        e = Assert.Throws<NanoQLException>(() =>
            new MakeVanillaSwap(fiveYears, new Euribor6M()).ToVanillaSwap(Context));
        Assert.Equal("null term structure set to this instance of Euribor6M Actual/360", e.Message);
        e = Assert.Throws<NanoQLException>(() => new MakeOIS(fiveYears, new Estr()).ToOvernightIndexedSwap(Context));
        Assert.Equal("null term structure set to this instance of ESTRON Actual/360", e.Message);
        e = Assert.Throws<NanoQLException>(() => new MakeOIS(fiveYears, estr).WithNominal(0.0)
            .ToOvernightIndexedSwap(Context));
        Assert.Equal("result not available", e.Message);

        // No fixed rate, and an engine delegate that returns no results to take the par rate from.
        const string NoResults = "the pricing engine returned no results";
        e = Assert.Throws<NanoQLException>(() => new MakeVanillaSwap(fiveYears, euribor)
            .WithPricingEngine((_, _) => null!)
            .ToVanillaSwap(Context));
        Assert.Equal(NoResults, e.Message);
        e = Assert.Throws<NanoQLException>(() => new MakeOIS(fiveYears, estr)
            .WithPricingEngine((_, _) => null!)
            .ToOvernightIndexedSwap(Context));
        Assert.Equal(NoResults, e.Message);

        // A stored frequency with no period, which a zero-coupon rule would take.
        e = Assert.Throws<NanoQLException>(() => new MakeOIS(fiveYears, estr).WithFixedRate(0.03)
            .WithPaymentFrequency(Frequency.OtherFrequency).ToOvernightIndexedSwap(Context));
        Assert.Equal("unknown frequency", e.Message);
        Assert.NotNull(new MakeOIS(fiveYears, estr).WithFixedRate(0.03).WithPaymentFrequency(Frequency.OtherFrequency)
            .WithRule(DateGenerationRule.Zero).ToOvernightIndexedSwap(Context));

        // Arguments: null indexes and the rest, enums outside their types, negative counts, and MakeOIS's rounding.
        Assert.Equal(
            "index", Assert.Throws<ArgumentNullException>(() => new MakeVanillaSwap(fiveYears, null!)).ParamName);
        Assert.Equal(
            "overnightIndex", Assert.Throws<ArgumentNullException>(() => new MakeOIS(fiveYears, null!)).ParamName);
        Assert.Equal(
            "context",
            Assert.Throws<ArgumentNullException>(() => new MakeVanillaSwap(fiveYears, euribor).ToVanillaSwap(null!))
                .ParamName);
        Assert.Equal(
            "type",
            Assert.Throws<ArgumentOutOfRangeException>(() => new MakeOIS(fiveYears, estr).WithType((SwapType)2))
                .ParamName);
        Assert.Equal(
            "f",
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MakeOIS(fiveYears, estr).WithPaymentFrequency((Frequency)5)).ParamName);
        Assert.Equal(
            "settlementDays",
            Assert.Throws<ArgumentOutOfRangeException>(() => new MakeVanillaSwap(fiveYears, euribor)
                .WithSettlementDays(-2)).ParamName);
        Assert.Equal(
            "effectiveDate",
            Assert.Throws<ArgumentException>(() => new MakeOIS(fiveYears, estr).WithEffectiveDate(default(Date)))
                .ParamName);

        // No discounting curve, QuantLib's empty handle: the engine set on it refuses a par build with QuantLib's text
        // (discountingswapengine.cpp:42).
        const string EmptyHandle = "discounting term structure handle is empty";
        e = Assert.Throws<NanoQLException>(() =>
            new MakeVanillaSwap(fiveYears, euribor).WithDiscountingTermStructure(null).ToVanillaSwap(Context));
        Assert.Equal(EmptyHandle, e.Message);
        e = Assert.Throws<NanoQLException>(() =>
            new MakeOIS(fiveYears, estr).WithDiscountingTermStructure(null).ToOvernightIndexedSwap(Context));
        Assert.Equal(EmptyHandle, e.Message);
    }

    [Fact]
    public void MakeOISRefusesARoundingPrecisionOutsideItsRangeAtItsConversion()
    {
        // QuantLib's withRoundingPrecision stores any value (makeois.cpp:401-404), and the swap's constructor refuses
        // one outside [0, 16] when the builder converts (overnightindexedswap.cpp:177-181): an argument of the swap's,
        // with its type, name and text.
        var builder = new MakeOIS(new Period(1, TimeUnit.Years), new Estr(Curve)).WithFixedRate(0.03);
        foreach (int precision in new[] { 17, -1 })
        {
            MakeOIS stored = builder.WithRoundingPrecision(precision);
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() =>
                stored.ToOvernightIndexedSwap(Context));
            Assert.Equal("roundingPrecision", e.ParamName);
            Assert.StartsWith(
                $"rounding precision ({precision}) must be between 0 and 16", e.Message, StringComparison.Ordinal);
        }

        // A refusal QuantLib's conversion raises before it builds the swap comes first (wheel: "cannot set both...").
        NanoQLException both = Assert.Throws<NanoQLException>(() => builder.WithRoundingPrecision(17)
            .WithEffectiveDate(new Date(20, Month.January, 2025)).WithSettlementDays(2).ToOvernightIndexedSwap(Context));
        Assert.Equal("cannot set both an explicit effective date and settlement days; use one or the other", both.Message);

        // The range itself, and no rounding, stay QuantLib's.
        Assert.Equal(16, builder.WithRoundingPrecision(16).ToOvernightIndexedSwap(Context).RoundingPrecision);
        Assert.Null(builder.WithRoundingPrecision(6).WithRoundingPrecision(null).ToOvernightIndexedSwap(Context)
            .RoundingPrecision);
    }

    [Fact]
    public void TheBuildersTakeQuantLibsEmptyCalendarsAsNull()
    {
        // QuantLib's empty settlement and payment calendars, null: the leg's calendar (makeois.cpp:82,
        // makevanillaswap.cpp:88, overnightindexedswap.cpp:170-172). Valued on Thursday 17 April 2025, the spot date
        // two TARGET days later passes Good Friday and Easter Monday.
        var easter = new EvaluationContext { EvaluationDate = new Date(17, Month.April, 2025) };
        MakeOIS ois = new MakeOIS(new Period(1, TimeUnit.Years), new Estr(Curve)).WithFixedRate(0.03).WithPaymentLag(2);
        OvernightIndexedSwap named = ois.WithSettlementCalendar(new TARGET()).WithPaymentCalendar(new TARGET())
            .ToOvernightIndexedSwap(easter);
        OvernightIndexedSwap nulls = ois.WithSettlementCalendar(null).WithPaymentCalendar(null).ToOvernightIndexedSwap(easter);
        Assert.Equal(new Date(23, Month.April, 2025), nulls.StartDate());
        Assert.Equal(PaymentDates(named), PaymentDates(nulls));

        // MakeVanillaSwap reads its settlement calendar only with settlement days given (makevanillaswap.cpp:84-89).
        var vanilla = new MakeVanillaSwap(new Period(2, TimeUnit.Years), new Euribor6M(Curve)).WithFixedRate(0.03)
            .WithSettlementDays(2);
        Assert.Equal(new Date(23, Month.April, 2025), vanilla.WithSettlementCalendar(new TARGET()).ToVanillaSwap(easter).StartDate());
        Assert.Equal(new Date(23, Month.April, 2025), vanilla.WithSettlementCalendar(null).ToVanillaSwap(easter).StartDate());
    }

    private static Date[] PaymentDates(Swap swap) => [.. swap.Legs.SelectMany(leg => leg.Select(cf => cf.Date))];

    private static void AssertRefused<TException>(Action build, string paramName, string message)
        where TException : ArgumentException
    {
        TException e = Assert.Throws<TException>(build);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }

    // A swap built on the base's own constructor, with the floating leg it is given.
    private sealed class BuiltLegSwap(
        Schedule fixedSchedule, Schedule floatingSchedule, IborIndex index, Func<ImmutableArray<CashFlow>> floatingLeg)
        : FixedVsFloatingSwap(
            SwapType.Payer,
            [1e6],
            fixedSchedule,
            0.03,
            null,
            [1e6],
            floatingSchedule,
            index,
            0.0,
            null,
            null,
            0,
            null,
            floatingLeg);
}
