// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The forward's calculation (the S-cms plan's CMD5): <see cref="BondForward.Calculate"/> computes under the context it
/// is given at every call, its spot value and income the results' by-products where QuantLib's
/// <c>underlyingSpotValue_</c> and <c>underlyingIncome_</c> are mutable members; and its spot value is the bond's dirty
/// price on the engine it is given, as QuantLib's <c>spotValue()</c> reads the bond's own engine
/// (<c>bond_-&gt;dirtyPrice()</c>).
/// </summary>
public class ForwardCalculateTests
{
    private static readonly Date Today = new(7, Month.March, 2022);
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.0004977, new Actual365Fixed());

    [Fact]
    public void ASecondCalculationComputesAgainUnderItsContext()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        BondForward forward = Forward(context);
        ForwardResults first = forward.Calculate(context, BondEngine(Curve));
        Assert.Equal(first, forward.Calculate(context, BondEngine(Curve)));

        // a day later the bond settles a day later
        EvaluationContext later = context with { EvaluationDate = new Date(8, Month.March, 2022) };
        ForwardResults again = forward.Calculate(later, BondEngine(Curve));
        Assert.NotEqual(first.SpotValue, again.SpotValue);
        Assert.Equal(first, forward.Calculate(context, BondEngine(Curve)));
    }

    [Fact]
    public void TheSpotValueIsTheBondsDirtyPriceOnTheEngineGiven()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        BondForward forward = Forward(context);
        YieldTermStructure other = new FlatForward(Today, 0.03, new Actual365Fixed());
        ForwardResults results = forward.Calculate(context, BondEngine(other));
        double dirty = forward.Bond.DirtyPrice(DiscountingBondEngine.Calculate(forward.Bond, context, other), context);
        Assert.Equal(dirty, results.SpotValue);
        Assert.Equal(dirty, forward.SpotValue(context, BondEngine(other)));
        Assert.NotEqual(results.SpotValue, forward.Calculate(context, BondEngine(Curve)).SpotValue);
    }

    private static Func<Bond, EvaluationContext, BondResults> BondEngine(YieldTermStructure curve) =>
        (bond, context) => DiscountingBondEngine.Calculate(bond, context, curve);

    // QuantLib's test-suite forward: the 2046 2.5% bond on 1e5, delivered on 10 March 2022.
    private static BondForward Forward(EvaluationContext context)
    {
        var schedule = new Schedule(
            new Date(15, Month.August, 2015), new Date(15, Month.August, 2046), new Period(Frequency.Annual), new TARGET(),
            BusinessDayConvention.Following, BusinessDayConvention.Following, DateGenerationRule.Backward, false, context);
        var bond = new FixedRateBond(2, 1e5, schedule, [0.025], new ActualActual(ActualActualConvention.ISDA));
        return new BondForward(
            Today, new Date(10, Month.March, 2022), PositionType.Long, 0.0, 2, new ActualActual(ActualActualConvention.ISDA),
            new TARGET(), BusinessDayConvention.Following, bond, Curve, Curve);
    }
}
