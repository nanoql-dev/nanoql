// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The integral CDS engine's, the composite instrument's and the convex-monotone interpolation's conventions: the
/// engine refuses a negative step on a live swap, where QuantLib's loop walks back until a curve refuses its date, and
/// QuantLib's null step with its text; the composite is immutable, its engines delegates; and the interpolation's
/// checks and its zero value are QuantLib's.
/// </summary>
public class CoreInstrumentConventionTests
{
    private static readonly Date Today = new(9, Month.June, 2006);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    private static CreditDefaultSwap Swap(Date start, int years)
    {
        var schedule = new Schedule(
            start, start + new Period(years, TimeUnit.Years), new Period(Frequency.Semiannual), new TARGET(),
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward,
            false, Context);
        return new CreditDefaultSwap(
            ProtectionSide.Seller, 10000.0, 0.012, schedule, BusinessDayConvention.ModifiedFollowing, new Actual360());
    }

    [Fact]
    public void TheIntegralEngineRefusesANegativeOrNullStep()
    {
        var probability = new FlatHazardRate(Today, 0.01234, new Actual360());
        var discount = new FlatForward(Today, 0.06, new Actual360());
        CreditDefaultSwap live = Swap(Today - 30, 5);

        ArgumentException negative = Assert.Throws<ArgumentException>(
            () => IntegralCdsEngine.Calculate(live, Context, new Period(-1, TimeUnit.Days), probability, 0.4, discount));
        Assert.Equal("integrationStep", negative.ParamName);

        NanoQLException none = Assert.Throws<NanoQLException>(
            () => IntegralCdsEngine.Calculate(live, Context, new Period(0, TimeUnit.Weeks), probability, 0.4, discount));
        Assert.Equal("null period set", none.Message);

        Assert.Equal(
            "no discount term structure set",
            Assert.Throws<NanoQLException>(
                () => IntegralCdsEngine.Calculate(live, Context, new Period(1, TimeUnit.Days), probability, 0.4, null)).Message);
        Assert.Equal(
            "no probability term structure set",
            Assert.Throws<NanoQLException>(
                () => IntegralCdsEngine.Calculate(live, Context, new Period(1, TimeUnit.Days), null, 0.4, discount)).Message);

        // an expired swap's results are QuantLib's, the engine's checks not reached, as QuantLib's instrument gives them
        CreditDefaultSwap expired = Swap(Today - 800, 2);
        Assert.Equal(
            0.0,
            IntegralCdsEngine.Calculate(expired, Context, new Period(-1, TimeUnit.Days), probability, 0.4, discount).NPV);
    }

    [Fact]
    public void ACompositeIsImmutableAndSumsItsComponents()
    {
        var empty = new CompositeInstrument();
        Assert.True(empty.IsExpired(Context));
        Assert.Equal(0.0, empty.Calculate(Context).NPV);

        var stock = new Stock(42.0);
        CompositeInstrument one = empty.Add(stock, stock.Calculate, 2.0);
        CompositeInstrument two = one.Subtract(stock, stock.Calculate, 0.5);
        Assert.Empty(empty.Components);
        Assert.Single(one.Components);
        Assert.Equal(-0.5, two.Components[1].Multiplier);
        Assert.Equal((2.0 * 42.0) + (-0.5 * 42.0), two.Calculate(Context).NPV);
        Assert.Null(two.Calculate(Context).ErrorEstimate);

        Assert.Equal("instrument", Assert.Throws<ArgumentNullException>(() => one.Add(null!, stock.Calculate)).ParamName);
        Assert.Equal("engine", Assert.Throws<ArgumentNullException>(() => one.Add(stock, null!)).ParamName);

        // a component's engine that gives no value: QuantLib's NPV() refuses it
        CompositeInstrument noValue = empty.Add(stock, _ => new InstrumentResults());
        Assert.Equal("NPV not provided", Assert.Throws<NanoQLException>(() => noValue.Calculate(Context)).Message);
    }

    [Fact]
    public void AnExpiredComponentOfALiveCompositeIsWorthNothing()
    {
        // QuantLib's composite reads each component's NPV(), which an expired instrument gives as setupExpired's 0
        // without calling its engine: a delegate that would price the expired swap isn't called
        EvaluationContext context = CreditFixture.Context();
        CreditDefaultSwap expired = CreditFixture.Swap(expired: true, spread: 0.01);
        Assert.True(expired.IsExpired(context));
        var stock = new Stock(42.0);
        CompositeInstrument composite = new CompositeInstrument()
            .Add(stock, stock.Calculate, 2.0)
            .Add(expired, _ => throw new InvalidOperationException("an expired component's engine is called"), 3.0);

        Assert.False(composite.IsExpired(context));
        Assert.Equal(2.0 * 42.0, composite.Calculate(context).NPV);
    }

    [Fact]
    public void TheConvexMonotoneChecksAndDefaultsAreQuantLibs()
    {
        double[] x = [0.0, 1.0, 2.0], y = [0.0, 0.02, 0.03];
        Assert.Equal(
            "monotonicity",
            Assert.Throws<ArgumentException>(() => new ConvexMonotoneInterpolation(x, y, 0.3, 1.5, true)).ParamName);
        Assert.Equal(
            "quadraticity",
            Assert.Throws<ArgumentException>(() => new ConvexMonotoneInterpolation(x, y, -0.1, 0.7, true)).ParamName);
        Assert.Throws<ArgumentException>(() => new ConvexMonotoneInterpolation([0.0], [0.0], 0.3, 0.7, true));
        Assert.Throws<ArgumentException>(() => default(ConvexMonotone).CheckPoints(1));
        Assert.Throws<ArgumentException>(() => new ConvexMonotone(2.0).CheckPoints(5));

        var f = new ConvexMonotoneInterpolation(x, y, 0.3, 0.7, true);
        Assert.Equal(
            "Convex-monotone spline derivative not implemented", Assert.Throws<NanoQLException>(() => f.Derivative(0.5)).Message);
        Assert.Equal(
            "Convex-monotone spline second derivative not implemented",
            Assert.Throws<NanoQLException>(() => f.SecondDerivative(0.5)).Message);

        // the zero value is QuantLib's default construction, and builds what it builds
        ConvexMonotone d = default;
        Assert.Equal((0.3, 0.7, true, 1), (d.Quadraticity, d.Monotonicity, d.ForcePositive, d.DataSizeAdjustment));
        Assert.Equal(new ConvexMonotone().Interpolate(x, y).Evaluate(1.5), new ConvexMonotone(0.3, 0.7, true).Interpolate(x, y).Evaluate(1.5));

        // a single period is flat at its average
        var single = new ConvexMonotoneInterpolation([0.0, 2.0], [0.0, 0.04], 0.3, 0.7, true);
        Assert.Equal(0.04, single.Evaluate(1.0));
        Assert.Equal(0.08, single.Primitive(2.0), 15);
    }
}
