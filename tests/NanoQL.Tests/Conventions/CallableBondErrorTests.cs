// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The callable bonds' refusals by type, message and parameter: those of QuantLib's undefined paths (playbook §2), a
/// value already stored refused with <see cref="NanoQLException"/> and an argument with
/// <see cref="ArgumentException"/>, as is a callability after the maturity date; a callability type outside its enum
/// refused on entry with the tree's text; the tree engine's refusals of a missing term structure and of a spread on a
/// lattice that takes none; an expired bond's implied volatility, refused by QuantLib's own check before the Black
/// engine's would refuse it, and its spread family, refused with the same text; and the plain bonds' engine's refusal
/// of a callable bond, with QuantLib's "no arguments given", which names no experimental type. Then QuantLib's refusals
/// the vectors record only as throws: the Black engine's single callability, the arguments' negative redemption, the
/// Black engine's empty day counter, the volatility structure's range checks, on a structure of bounded bond tenors and
/// strikes, and its smile section's negative tenor; and QuantLib's empty calendar, refused where an option date or a
/// zero-coupon bond's redemption date is computed on it.
/// </summary>
public class CallableBondErrorTests
{
    private static readonly Date Today = new(16, Month.September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly Calendar Calendar = new TARGET();
    private static readonly YieldTermStructure Curve =
        new FlatForward(Calendar.Advance(Today, 2, TimeUnit.Days), 0.03, new Actual365Fixed());

    [Fact]
    public void TheCallableBondsRefusals()
    {
        // The schedule's own refusal of an empty schedule
        NanoQLException empty = Assert.Throws<NanoQLException>(() => new CallableFixedRateBond(
            2, 100.0, new Schedule([], Calendar), [0.05], new Thirty360(Thirty360Convention.BondBasis)));
        Assert.Equal(typeof(NanoQLException), empty.GetType());
        Assert.Equal("empty Schedule: no end date", empty.Message);

        // A null callability, an argument
        ArgumentException nullCall = Assert.Throws<ArgumentException>(() => new CallableZeroCouponBond(
            2, 100.0, Calendar, new Date(15, Month.June, 2030), new Actual365Fixed(), putCallSchedule: [null!]));
        Assert.Equal(typeof(ArgumentException), nullCall.GetType());
        Assert.Equal("putCallSchedule", nullCall.ParamName);
        Assert.StartsWith("null callability in schedule", nullCall.Message, StringComparison.Ordinal);

        // The Black engine's one callability occurred, a value already stored
        Date settlement = Calendar.Advance(Today, 3, TimeUnit.Days);
        NanoQLException occurred = Assert.Throws<NanoQLException>(
            () => BlackCallableFixedRateBondEngine.Calculate(Bond(settlement), Context, 0.2, Curve));
        Assert.Equal(typeof(NanoQLException), occurred.GetType());
        Assert.Equal("must have exercise Date >= settlement Date", occurred.Message);

        // a callability after the maturity date, an argument
        var lateCall = new Callability(
            new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call, new Date(16, Month.June, 2030));
        ArgumentException late = Assert.Throws<ArgumentException>(() => new CallableZeroCouponBond(
            2, 100.0, Calendar, new Date(15, Month.June, 2030), new Actual365Fixed(), putCallSchedule: [lateCall]));
        Assert.Equal(typeof(ArgumentException), late.GetType());
        Assert.Equal("putCallSchedule", late.ParamName);
        Assert.StartsWith("Bond cannot mature before last call/put date", late.Message, StringComparison.Ordinal);

        // the tree's "unknown callability type", on entry
        ArgumentOutOfRangeException type = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Callability(new BondPrice(100.0, BondPriceType.Clean), (CallabilityType)2, settlement));
        Assert.Equal("type", type.ParamName);
        Assert.StartsWith("unknown callability type", type.Message, StringComparison.Ordinal);

        // a model that isn't term-structure consistent needs the engine's curve: QuantLib's empty handle, refused as
        // the swaption engines refuse it
        CallableFixedRateBond bond = Bond(Calendar.Advance(Today, 4, TimeUnit.Years));
        NanoQLException noCurve = Assert.Throws<NanoQLException>(
            () => TreeCallableFixedRateBondEngine.Calculate(bond, Context, new Vasicek(), 40));
        Assert.Equal(typeof(NanoQLException), noCurve.GetType());
        Assert.Equal("empty Handle cannot be dereferenced", noCurve.Message);

        // a spread on a lattice other than a one-factor model's tree
        var model = new HullWhite(Curve);
        NanoQLException spread = Assert.Throws<NanoQLException>(() => TreeCallableFixedRateBondEngine.Calculate(
            bond, Context, model, new OtherLattice(new TimeGrid(10.0, 10)), null, 0.01));
        Assert.Equal(typeof(NanoQLException), spread.GetType());
        Assert.Equal("Spread is not supported for trees other than OneFactorModel", spread.Message);

        // an expired bond's implied volatility: QuantLib's own check, ahead of the Black engine's refusal of an
        // occurred callability, which the bond's occurred call would meet
        var expired = new EvaluationContext { EvaluationDate = new Date(16, Month.September, 2030) };
        NanoQLException implied = Assert.Throws<NanoQLException>(() => bond.ImpliedVolatility(
            new BondPrice(100.0, BondPriceType.Clean), Curve, expired, 1e-8, 200, 0.0, 1.0));
        Assert.Equal(typeof(NanoQLException), implied.GetType());
        Assert.Equal("instrument expired", implied.Message);

        // and its spread family, with the same text, where QuantLib's prices the expired bond's arguments
        CallableBondResults Tree(CallableBond b, EvaluationContext x, double s) =>
            TreeCallableFixedRateBondEngine.Calculate(b, x, model, 40, null, s);
        var dc = new Actual365Fixed();
        Func<double>[] spreadFamily =
        [
            () => bond.OAS(100.0, Curve, dc, Compounding.Continuous, Frequency.NoFrequency, Tree, expired),
            () => bond.CleanPriceOAS(0.01, Curve, dc, Compounding.Continuous, Frequency.NoFrequency, Tree, expired),
            () => bond.EffectiveDuration(0.01, Curve, dc, Compounding.Continuous, Frequency.NoFrequency, Tree, expired),
            () => bond.EffectiveConvexity(
                0.01, Curve, dc, Compounding.Continuous, Frequency.NoFrequency, Tree, expired),
        ];
        foreach (Func<double> member in spreadFamily)
        {
            NanoQLException e = Assert.Throws<NanoQLException>(() => member());
            Assert.Equal(typeof(NanoQLException), e.GetType());
            Assert.Equal("instrument expired", e.Message);
        }

        // the arguments are checked first, as the engines check theirs before the expiry
        ArgumentOutOfRangeException iterations = Assert.Throws<ArgumentOutOfRangeException>(() => bond.OAS(
            100.0, Curve, dc, Compounding.Continuous, Frequency.NoFrequency, Tree, expired, maxIterations: -1));
        Assert.Equal("maxIterations", iterations.ParamName);
        ArgumentException settlementDate = Assert.Throws<ArgumentException>(() => bond.CleanPriceOAS(
            0.01, Curve, dc, Compounding.Continuous, Frequency.NoFrequency, Tree, expired, default(Date)));
        Assert.Equal(typeof(ArgumentException), settlementDate.GetType());
        Assert.Equal("settlementDate", settlementDate.ParamName);
        ArgumentOutOfRangeException evaluations = Assert.Throws<ArgumentOutOfRangeException>(() =>
            bond.ImpliedVolatility(new BondPrice(100.0, BondPriceType.Clean), Curve, expired, 1e-8, -1, 0.0, 1.0));
        Assert.Equal("maxEvaluations", evaluations.ParamName);
    }

    [Fact]
    public void TheDiscountingBondEngineRefusesACallableBond()
    {
        CallableFixedRateBond bond = Bond(Calendar.Advance(Today, 4, TimeUnit.Years));

        NanoQLException e = Assert.Throws<NanoQLException>(() => DiscountingBondEngine.Calculate(bond, Context, Curve));
        Assert.Equal(typeof(NanoQLException), e.GetType());
        Assert.Equal("no arguments given", e.Message);

        // QuantLib's refusal comes after the expiry check: an expired callable bond gets the expired results
        var later = new EvaluationContext { EvaluationDate = new Date(16, Month.September, 2030) };
        Assert.Equal(0.0, DiscountingBondEngine.Calculate(bond, later, Curve).NPV);

        // and the copy a BondHelper prices is a plain bond, which the engine prices, as QuantLib's BondHelper slices it
        var bondHelper = new BondHelper(100.0, bond, Context);
        Assert.False(bondHelper.Bond is CallableBond);
        Assert.NotNull(DiscountingBondEngine.Calculate(bondHelper.Bond, Context, Curve).NPV);
    }

    [Fact]
    public void TheBlackEngineAndArgumentsRefusals()
    {
        Date first = Calendar.Advance(Today, 4, TimeUnit.Years);
        Date second = Calendar.Advance(Today, 6, TimeUnit.Years);

        // the Black engine prices a single callability: two, and none, are refused
        NanoQLException two = Assert.Throws<NanoQLException>(() => BlackCallableFixedRateBondEngine.Calculate(
            Bond([Call(first), Call(second)]), Context, 0.2, Curve));
        Assert.Equal(typeof(NanoQLException), two.GetType());
        Assert.Equal("Must have exactly one call/put date to use Black Engine", two.Message);
        NanoQLException none = Assert.Throws<NanoQLException>(
            () => BlackCallableFixedRateBondEngine.Calculate(Bond([]), Context, 0.2, Curve));
        Assert.Equal(typeof(NanoQLException), none.GetType());
        Assert.Equal("Must have exactly one call/put date to use Black Engine", none.Message);

        // a negative redemption, refused by the arguments through either engine
        CallableFixedRateBond negative = Bond([Call(first)], -5.0);
        NanoQLException tree = Assert.Throws<NanoQLException>(
            () => TreeCallableFixedRateBondEngine.Calculate(negative, Context, new HullWhite(Curve), 40));
        Assert.Equal(typeof(NanoQLException), tree.GetType());
        Assert.Equal("positive redemption required: -5 not allowed", tree.Message);
        NanoQLException black = Assert.Throws<NanoQLException>(
            () => BlackCallableFixedRateBondEngine.Calculate(negative, Context, 0.2, Curve));
        Assert.Equal(typeof(NanoQLException), black.GetType());
        Assert.Equal("positive redemption required: -5 not allowed", black.Message);

        // a zero-coupon bond with QuantLib's empty day counter: the Black engine's forward yield can't count its time
        var zero = new CallableZeroCouponBond(
            2, 100.0, Calendar, new Date(15, Month.June, 2030), null, putCallSchedule: [Call(first)]);
        NanoQLException dayCounter = Assert.Throws<NanoQLException>(
            () => BlackCallableZeroCouponBondEngine.Calculate(zero, Context, 0.2, Curve));
        Assert.Equal(typeof(NanoQLException), dayCounter.GetType());
        Assert.Equal("no day counter implementation provided", dayCounter.Message);
    }

    [Fact]
    public void TheVolatilityStructuresRangeRefusals()
    {
        // bond tenors up to 10 years and strikes in [0, 0.1], on 30/360: the longest bond length is 10
        var vol = new BoundedVolatility(Today);
        Date optionDate = Calendar.Advance(Today, 1, TimeUnit.Years);
        Assert.Equal(0.2, vol.Volatility(1.0, 5.0, 0.05));
        Assert.Equal(0.2, vol.Volatility(optionDate, new Period(5, TimeUnit.Years), 0.05));

        // by time and length
        NanoQLException negativeLength = Assert.Throws<NanoQLException>(() => vol.Volatility(1.0, -0.5, 0.05));
        Assert.Equal(typeof(NanoQLException), negativeLength.GetType());
        Assert.Equal("negative bondLength (-0.5) given", negativeLength.Message);
        NanoQLException longLength = Assert.Throws<NanoQLException>(() => vol.Volatility(1.0, 12.5, 0.05));
        Assert.Equal(typeof(NanoQLException), longLength.GetType());
        Assert.Equal("bondLength (12.5) is past max curve bondLength (10)", longLength.Message);

        // by date and tenor
        NanoQLException longTenor = Assert.Throws<NanoQLException>(
            () => vol.Volatility(optionDate, new Period(20, TimeUnit.Years), 0.05));
        Assert.Equal(typeof(NanoQLException), longTenor.GetType());
        Assert.Equal("bond tenor (20Y) is past max tenor (10Y)", longTenor.Message);
        NanoQLException negativeTenor = Assert.Throws<NanoQLException>(
            () => vol.Volatility(optionDate, new Period(-1, TimeUnit.Years), 0.05));
        Assert.Equal(typeof(NanoQLException), negativeTenor.GetType());
        Assert.Equal("negative bond tenor (-1Y) given", negativeTenor.Message);
        NanoQLException zeroTenor = Assert.Throws<NanoQLException>(
            () => vol.Volatility(optionDate, new Period(0, TimeUnit.Days), 0.05));
        Assert.Equal(typeof(NanoQLException), zeroTenor.GetType());
        Assert.Equal("negative bond tenor (0D) given", zeroTenor.Message);

        // the conversion of a tenor ending on or before its date, an argument
        ArgumentException convert = Assert.Throws<ArgumentException>(
            () => vol.ConvertDates(optionDate, new Period(0, TimeUnit.Days)));
        Assert.Equal(typeof(ArgumentException), convert.GetType());
        Assert.Equal("bondTenor", convert.ParamName);
        Assert.StartsWith("negative bond tenor (0D) given", convert.Message, StringComparison.Ordinal);
        ArgumentException convertBack = Assert.Throws<ArgumentException>(
            () => vol.ConvertDates(optionDate, new Period(-1, TimeUnit.Years)));
        Assert.Equal(typeof(ArgumentException), convertBack.GetType());
        Assert.Equal("bondTenor", convertBack.ParamName);
        Assert.StartsWith("negative bond tenor (-1Y) given", convertBack.Message, StringComparison.Ordinal);

        // the smile section converts its dates, with the conversion's refusal
        ArgumentException smile = Assert.Throws<ArgumentException>(
            () => vol.SmileSection(optionDate, new Period(0, TimeUnit.Years)));
        Assert.Equal(typeof(ArgumentException), smile.GetType());
        Assert.Equal("bondTenor", smile.ParamName);
        Assert.StartsWith("negative bond tenor (0Y) given", smile.Message, StringComparison.Ordinal);
        ArgumentException smileBack = Assert.Throws<ArgumentException>(
            () => vol.SmileSection(optionDate, new Period(-6, TimeUnit.Months)));
        Assert.Equal(typeof(ArgumentException), smileBack.GetType());
        Assert.Equal("bondTenor", smileBack.ParamName);
        Assert.StartsWith("negative bond tenor (-6M) given", smileBack.Message, StringComparison.Ordinal);

        // the strike, by either form
        NanoQLException strike = Assert.Throws<NanoQLException>(() => vol.Volatility(1.0, 5.0, 0.25));
        Assert.Equal(typeof(NanoQLException), strike.GetType());
        Assert.Equal("strike (0.25) is outside the curve domain [0,0.1]", strike.Message);
        NanoQLException negativeStrike = Assert.Throws<NanoQLException>(
            () => vol.Volatility(optionDate, new Period(5, TimeUnit.Years), -0.01));
        Assert.Equal(typeof(NanoQLException), negativeStrike.GetType());
        Assert.Equal("strike (-0.01) is outside the curve domain [0,0.1]", negativeStrike.Message);
    }

    [Fact]
    public void TheEmptyCalendarsRefusals()
    {
        // the constant volatility by reference date is on QuantLib's empty calendar: an option date in days refused
        var vol = new CallableBondConstantVolatility(Today, 0.2, new Actual365Fixed());
        NanoQLException optionDate = Assert.Throws<NanoQLException>(
            () => vol.OptionDateFromTenor(new Period(10, TimeUnit.Days)));
        Assert.Equal(typeof(NanoQLException), optionDate.GetType());
        Assert.Equal("no calendar implementation provided", optionDate.Message);

        // a zero-coupon bond on the empty calendar whose redemption date is adjusted
        NanoQLException zero = Assert.Throws<NanoQLException>(() => new CallableZeroCouponBond(
            2, 100.0, null, new Date(15, Month.June, 2030), new Actual365Fixed(), BusinessDayConvention.Following));
        Assert.Equal(typeof(NanoQLException), zero.GetType());
        Assert.Equal("no calendar implementation provided", zero.Message);
    }

    private static CallableFixedRateBond Bond(Date callDate) => Bond([Call(callDate)]);

    private static CallableFixedRateBond Bond(Callability[] putCallSchedule, double redemption = 100.0)
    {
        Date issue = Calendar.Adjust(Today - 100);
        Schedule schedule = new MakeSchedule().From(issue).To(Calendar.Advance(issue, 10, TimeUnit.Years))
            .WithCalendar(Calendar).WithFrequency(Frequency.Semiannual)
            .WithConvention(BusinessDayConvention.ModifiedFollowing).WithRule(DateGenerationRule.Backward).ToSchedule();
        return new CallableFixedRateBond(
            3, 100.0, schedule, [0.05], new Thirty360(Thirty360Convention.BondBasis),
            BusinessDayConvention.ModifiedFollowing, redemption, issue, putCallSchedule);
    }

    private static Callability Call(Date date) =>
        new(new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call, date);

    // A structure of bounded bond tenors and strikes, as the harness's own subclass is, whose smile no check reaches.
    private sealed class BoundedVolatility(Date referenceDate)
        : CallableBondVolatilityStructure(referenceDate, null, new Thirty360(Thirty360Convention.BondBasis))
    {
        public override Date MaxDate => Date.MaxDate;

        public override Period MaxBondTenor => new(10, TimeUnit.Years);

        public override double MinStrike() => 0.0;

        public override double MaxStrike() => 0.1;

        protected override TermStructures.SmileSection SmileSectionImpl(double optionTime, double bondLength) =>
            throw new NotSupportedException();

        protected override double VolatilityImpl(double optionTime, double bondLength, double strike) => 0.2;
    }

    // A lattice that isn't a one-factor model's short-rate tree, whose members the engine never reaches.
    private sealed class OtherLattice(TimeGrid grid) : Lattice(grid)
    {
        public override void Initialize(DiscretizedAsset asset, double time) => throw new NotSupportedException();

        public override void Rollback(DiscretizedAsset asset, double to) => throw new NotSupportedException();

        public override void PartialRollback(DiscretizedAsset asset, double to) => throw new NotSupportedException();

        public override double PresentValue(DiscretizedAsset asset) => throw new NotSupportedException();

        public override Vector Grid(double t) => throw new NotSupportedException();
    }
}
