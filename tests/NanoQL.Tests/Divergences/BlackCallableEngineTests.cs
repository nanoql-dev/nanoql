// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Black engine on a bond whose one callability has occurred, and the option-adjusted spread through the Black
/// engine. QuantLib's engine checks that the full schedule holds one callability, then reads the first of the live
/// ones, those after the settlement date (<c>blackcallablebondengine.cpp:127-133</c>): when the one callability has
/// occurred, that list is empty, and QuantLib reads past its end, which is undefined (the wheel read indeterminate
/// memory, and happened to refuse "must have exercise Date >= settlement Date"). nanoql refuses with that text. The
/// Black engine reads no spread (<c>blackcallablebondengine.cpp:125-175</c>), so the spread for a clean price can't be
/// bracketed: a defined path, kept as QuantLib's, where the solver refuses "unable to bracket root".
/// </summary>
public class BlackCallableEngineTests
{
    private static readonly Date Today = new(16, Month.September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly Calendar Calendar = new TARGET();
    private static readonly YieldTermStructure Curve =
        new FlatForward(Calendar.Advance(Today, 2, TimeUnit.Days), 0.03, new Actual365Fixed());

    [Fact]
    public void AnOccurredCallabilityIsRefusedWithQuantLibsText()
    {
        // on the settlement date, the call has occurred
        Date settlement = Calendar.Advance(Today, 3, TimeUnit.Days);
        CallableFixedRateBond bond = Bond(settlement);

        NanoQLException e = Assert.Throws<NanoQLException>(
            () => BlackCallableFixedRateBondEngine.Calculate(bond, Context, 0.2, Curve));
        Assert.Equal("must have exercise Date >= settlement Date", e.Message);

        // a day later, it's live and priced
        Assert.NotNull(BlackCallableFixedRateBondEngine.Calculate(Bond(settlement + 1), Context, 0.2, Curve).NPV);
    }

    [Fact]
    public void TheOASThroughTheBlackEngineCantBeBracketed()
    {
        CallableFixedRateBond bond = Bond(Calendar.Advance(Today, 4, TimeUnit.Years));
        CallableBondResults Engine(CallableBond b, EvaluationContext x, double s) =>
            BlackCallableFixedRateBondEngine.Calculate(b, x, 0.2, Curve);
        double cleanPrice = bond.CleanPrice(Engine(bond, Context, 0.0), Context);

        NanoQLException e = Assert.Throws<NanoQLException>(() => bond.OAS(
            cleanPrice - 2.0, Curve, new Actual365Fixed(), Compounding.Compounded, Frequency.Semiannual, Engine, Context));
        Assert.StartsWith("unable to bracket root in 100 function evaluations", e.Message, StringComparison.Ordinal);

        // the clean price is the same at any spread: the engine never reads it
        Assert.Equal(
            bond.CleanPriceOAS(0.0, Curve, new Actual365Fixed(), Compounding.Compounded, Frequency.Semiannual, Engine, Context),
            bond.CleanPriceOAS(0.05, Curve, new Actual365Fixed(), Compounding.Compounded, Frequency.Semiannual, Engine, Context));
    }

    private static CallableFixedRateBond Bond(Date callDate)
    {
        Date issue = Calendar.Adjust(Today - 100);
        Schedule schedule = new MakeSchedule().From(issue).To(Calendar.Advance(issue, 10, TimeUnit.Years))
            .WithCalendar(Calendar).WithFrequency(Frequency.Semiannual)
            .WithConvention(BusinessDayConvention.ModifiedFollowing).WithRule(DateGenerationRule.Backward).ToSchedule();
        return new CallableFixedRateBond(
            3, 100.0, schedule, [0.05], new Thirty360(Thirty360Convention.BondBasis),
            BusinessDayConvention.ModifiedFollowing, 100.0, issue,
            [new Callability(new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call, callDate)]);
    }
}
