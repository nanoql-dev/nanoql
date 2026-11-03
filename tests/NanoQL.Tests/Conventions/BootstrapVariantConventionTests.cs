// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The bootstrap variants' conventions: the global bootstrap's variable helpers follow the curve's helpers and the
/// instrument weights cover both, and a later build that moves a pillar is refused, as QuantLib's pillars are fixed
/// when its bootstrap starts; the local bootstrap refuses an interpolator that isn't a local one, where QuantLib's
/// template fails to compile, and a localisation below 1 or below the interpolator's data adjustment, where QuantLib's
/// steps read out of their arrays; the additional variables' guesses, bounds and transforms, QuantLib's; and the
/// variables through a multicurve's member as through the curve, and on every piecewise curve.
/// </summary>
public class BootstrapVariantConventionTests
{
    [Fact]
    public void TheWeightsCoverTheCurvesHelpersThenTheVariableHelpers()
    {
        // the curve's 20 helpers and the 3 futures: 20 weights are QuantLib's count check over the curve's instruments
        // alone, which the futures' place among them makes 23
        int curveHelpers = GlobalBootstrapVariablesFixture.CurveHelpers().Count - 1;
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => GlobalBootstrapVariablesFixture.Curve(weights: Enumerable.Repeat(1.0, curveHelpers)));
        Assert.Equal(
            $"GlobalBootstrap: number of instrument weights ({curveHelpers}) must match number of instruments "
            + $"({curveHelpers + 3})",
            e.Message);

        // 23 unit weights are QuantLib's default, bit for bit
        PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap> weighted =
            GlobalBootstrapVariablesFixture.Curve(weights: Enumerable.Repeat(1.0, curveHelpers + 3));
        PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap> plain = GlobalBootstrapVariablesFixture.Curve();
        Assert.Equal(plain.Data(), weighted.Data());
        Assert.Equal(plain.AdditionalVariables, weighted.AdditionalVariables);

        // the futures' pillars are among the curve's nodes
        foreach (IBootstrapHelper h in GlobalBootstrapVariablesFixture.Futures([0.01], GlobalBootstrapVariablesFixture.Context))
        {
            Assert.Contains(((BootstrapHelper<YieldTermStructure>)h).PillarDate!.Value, plain.Dates());
        }
    }

    [Fact]
    public void TheVariableHelpersWeightsReadTheirOwnRows()
    {
        // an over-determined set: the 6- and 9-month deposits out of the curve and penalised at weight 1, so they
        // compete with the futures over the same months and the futures keep errors at the optimum; the futures
        // weighted 5. At the optimum's volatility the nodes solve the same weighted least squares with the futures as
        // the curve's own instruments, weighted 5 in the same rows: the two routes agree, where a variable helper's
        // weight dropped or read from another row moves the optimum by the weights' effect. Vectors can't hold this
        // set: a one-ulp move of an input moves its fit by up to 6e-11, past the tolerance's margin, in QuantLib and
        // nanoql alike.
        List<RateHelper> all = GlobalBootstrapVariablesFixture.CurveHelpers();
        RateHelper[] penalised = [all[4], all[5]];
        List<RateHelper> helpers = [.. all.Take(4), .. all.Skip(6)];
        double[] weighted = [.. Enumerable.Repeat(1.0, helpers.Count), 5.0, 5.0, 5.0];

        static Vector Penalties(IBootstrapState state, EvaluationContext context) =>
            new([state.AdditionalHelpers[0].QuoteError(context), state.AdditionalHelpers[1].QuoteError(context)]);

        PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap> WithVariables(double[]? weights) => new(
            GlobalBootstrapVariablesFixture.Settlement,
            helpers,
            new Actual365Fixed(),
            GlobalBootstrapVariablesFixture.Context,
            i: default(LogLinear),
            bootstrap: new GlobalBootstrap(
                accuracy: 1e-12,
                instrumentWeights: weights,
                additionalHelpers: penalised,
                additionalPenalties: Penalties,
                additionalVariables: new SimpleQuoteVariables(1, [1.0], [0.0]),
                variableHelpers: GlobalBootstrapVariablesFixture.Futures));

        PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap> curve = WithVariables(weighted);
        ImmutableArray<IBootstrapHelper> futures =
            GlobalBootstrapVariablesFixture.Futures(curve.AdditionalVariables, GlobalBootstrapVariablesFixture.Context);
        var fixedVolatility = new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(
            GlobalBootstrapVariablesFixture.Settlement,
            [.. helpers, .. futures.Cast<RateHelper>()],
            new Actual365Fixed(),
            GlobalBootstrapVariablesFixture.Context,
            i: default(LogLinear),
            bootstrap: new GlobalBootstrap(
                accuracy: 1e-12, instrumentWeights: weighted, additionalHelpers: penalised, additionalPenalties: Penalties));

        // the futures' quote errors on each curve, where the weights act
        double[] Errors(YieldTermStructure c) =>
        [
            .. futures.Select(h => ((BootstrapHelper<YieldTermStructure>)h).SetTermStructure(c)
                .QuoteError(GlobalBootstrapVariablesFixture.Context)),
        ];

        Assert.Equal(curve.Dates(), fixedVolatility.Dates());
        double routes = Errors(curve).Zip(Errors(fixedVolatility), (a, b) => Math.Abs(a - b)).Max();
        PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap> unit = WithVariables(null);
        double effect = Errors(curve).Zip(Errors(unit), (a, b) => Math.Abs(a - b)).Max();
        Assert.True(routes < 1e-10, $"the two routes' futures errors differ by {routes}");

        // the weights' effect, which the two routes resolve: unit weights move the futures' errors by about 1.6e-6
        Assert.True(effect > 1e-7, $"the weights move the futures' errors by {effect}");
    }

    [Fact]
    public void AVariableThatMovesAPillarIsRefused()
    {
        // futures whose first contract moves to the next IMM date after the third once the volatility leaves its guess:
        // QuantLib's pillars are fixed at the bootstrap's start, its helpers' dates never reading a quote
        static ImmutableArray<IBootstrapHelper> Moving(ImmutableArray<double> vol, EvaluationContext context)
        {
            ImmutableArray<IBootstrapHelper> futures = GlobalBootstrapVariablesFixture.Futures(vol, context);
            if (vol[0] == 1.0)
            {
                return futures;
            }

            var helper = (FuturesRateHelper)futures[0];
            return futures.SetItem(
                0,
                new FuturesRateHelper(
                    helper.Quote,
                    IMM.NextDate(context, GlobalBootstrapVariablesFixture.ImmDates[2]),
                    GlobalBootstrapVariablesFixture.Euribor3M));
        }

        NanoQLException e = Assert.Throws<NanoQLException>(() => GlobalBootstrapVariablesFixture.Curve(Moving));
        Assert.Equal("the additional variables moved a pillar", e.Message);
    }

    [Fact]
    public void TheGlobalBootstrapHoldsItsVariablesAndTheirHelpers()
    {
        // QuantLib's default construction holds none
        GlobalBootstrap none = default;
        Assert.Null(none.AdditionalVariables);
        Assert.Null(none.VariableHelpers);

        var variables = new SimpleQuoteVariables(2, [0.1]);
        Func<ImmutableArray<double>, EvaluationContext, ImmutableArray<IBootstrapHelper>> helpers =
            GlobalBootstrapVariablesFixture.Futures;
        var b = new GlobalBootstrap(additionalVariables: variables, variableHelpers: helpers);
        Assert.Same(variables, b.AdditionalVariables);
        Assert.Same(helpers, b.VariableHelpers);
        Assert.Equal(2, variables.Count);
    }

    [Fact]
    public void TheVariablesRunThroughAMultiCurveAsThroughTheCurve()
    {
        // the fixture's curve as a multicurve's only member, on the same bootstrap: the multicurve's slices carry the
        // variables' guesses, so the member is the single curve bit for bit, its volatility included
        PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap> single = GlobalBootstrapVariablesFixture.Curve();
        List<RateHelper> helpers = GlobalBootstrapVariablesFixture.CurveHelpers();
        RateHelper firstSwap = helpers[6];
        helpers.RemoveAt(6);

        static Vector Penalties(IBootstrapState state, EvaluationContext context) =>
            new(1, 1e4 * state.AdditionalHelpers[0].QuoteError(context));

        var bootstrap = new GlobalBootstrap(
            accuracy: 1e-12,
            additionalHelpers: [firstSwap],
            additionalPenalties: Penalties,
            additionalVariables: new SimpleQuoteVariables(1, [1.0], [0.0]),
            variableHelpers: GlobalBootstrapVariablesFixture.Futures);
        var member = new MultiCurveMember(GlobalBootstrapVariablesFixture.Settlement, new Actual365Fixed());
        ImmutableArray<YieldTermStructure> built = new MultiCurve(1e-12)
            .AddBootstrappedCurve<Discount, LogLinear>(
                member, GlobalBootstrapVariablesFixture.Settlement, helpers, new Actual365Fixed(), bootstrap: bootstrap)
            .Build(GlobalBootstrapVariablesFixture.Context);

        var curve = (PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>)built[0];
        Assert.Equal(single.Dates(), curve.Dates());
        Assert.Equal(single.Data(), curve.Data());
        Assert.Single(curve.AdditionalVariables);
        Assert.Equal(single.AdditionalVariables, curve.AdditionalVariables);
    }

    [Fact]
    public void EveryPiecewiseCurveHoldsItsBootstrapsVariables()
    {
        // the default and inflation curves read the variables as the yield curve does: none on a bootstrap without them
        EvaluationContext context = InflationFixture.Context();
        var lag = new Period(3, TimeUnit.Months);
        var rpi = new UKRPI();
        ZeroCouponInflationSwapHelper[] zeroHelpers =
        [
            .. new[] { 1, 2, 3 }.Select(y => new ZeroCouponInflationSwapHelper(
                0.03, lag, InflationFixture.Today + new Period(y, TimeUnit.Years), new UnitedKingdom(),
                BusinessDayConvention.ModifiedFollowing, new Actual365Fixed(), rpi, CPIInterpolationType.Flat, context)),
        ];
        var zero = new PiecewiseZeroInflation(
            InflationFixture.Today, ctx => rpi.LastFixingDate(ctx), Frequency.Monthly, new Actual365Fixed(), zeroHelpers,
            context);
        Assert.Empty(zero.AdditionalVariables);

        var yoyIndex = new YoYInflationIndex(rpi);
        YearOnYearInflationSwapHelper[] yoyHelpers =
        [
            .. new[] { 1, 2, 3 }.Select(y => new YearOnYearInflationSwapHelper(
                0.03, lag, InflationFixture.Today + new Period(y, TimeUnit.Years), new UnitedKingdom(),
                BusinessDayConvention.ModifiedFollowing, new Actual365Fixed(), yoyIndex, CPIInterpolationType.Flat,
                InflationFixture.Nominal(), context)),
        ];
        var yoy = new PiecewiseYoYInflationCurve<Linear, GlobalBootstrap, YoYInflationTraits>(
            InflationFixture.Today, new Date(1, Month.April, 2024), 0.03, Frequency.Monthly, new Actual365Fixed(),
            yoyHelpers, context);
        Assert.Empty(yoy.AdditionalVariables);

        var creditContext = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2024) };
        BootstrapHelper<DefaultProbabilityTermStructure>[] cdsHelpers =
        [
            new SpreadCdsHelper(
                0.005, new Period(2, TimeUnit.Years), 0, new TARGET(), Frequency.Quarterly, BusinessDayConvention.Following,
                DateGenerationRule.TwentiethIMM, new Thirty360(Thirty360Convention.BondBasis), 0.4,
                new FlatForward(creditContext.EvaluationDate, 0.06, new Actual360()), creditContext),
        ];
        var hazard = new PiecewiseDefaultCurve<HazardRate, BackwardFlat, IterativeBootstrap>(
            creditContext.EvaluationDate, cdsHelpers, new Thirty360(Thirty360Convention.BondBasis), creditContext);
        Assert.Empty(hazard.AdditionalVariables);
    }

    [Fact]
    public void TheVariablesTakeQuantLibsGuessesBoundsAndTransforms()
    {
        // past the guesses and bounds given, the last (QuantLib's detail::get); log(v - lb) and exp(x) + lb with a bound
        var variables = new SimpleQuoteVariables(3, [0.2], [null, 0.1]);
        Vector guess = variables.Initialize(false);
        Assert.Equal(0.2, guess[0]);
        Assert.Equal(Math.Log(0.2 - 0.1), guess[1]);
        Assert.Equal(Math.Log(0.2 - 0.1), guess[2]);
        Assert.Equal([0.5, Math.Exp(0.5) + 0.1, Math.Exp(-1.0) + 0.1], variables.Values([0.5, 0.5, -1.0]));
        Assert.Equal([0.0], new SimpleQuoteVariables(1).Initialize(true).ToArray());

        Assert.Equal(
            "initialGuesses",
            Assert.Throws<ArgumentException>(() => new SimpleQuoteVariables(1, [1.0, 2.0])).ParamName);
        Assert.Equal(
            "lowerBounds", Assert.Throws<ArgumentException>(() => new SimpleQuoteVariables(1, null, [0.0, 0.0])).ParamName);
        Assert.Throws<ArgumentException>(() => variables.Values([0.1, 0.2, 0.3, 0.4]));
    }

    [Fact]
    public void ALocalBootstrapNeedsALocalInterpolation()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        var settlement = new Date(17, Month.January, 2025);
        List<RateHelper> helpers = Deposits(context, 4);

        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new PiecewiseYieldCurve<ForwardRate, Linear, LocalBootstrap>(settlement, helpers, new Actual360(), context));
        Assert.Equal("interpolator", e.ParamName);
        Assert.StartsWith("LocalBootstrap needs a local interpolation", e.Message);

        // a localisation below 1, where QuantLib's first step reads out of its arrays
        Assert.Equal(
            "interpolator",
            Assert.Throws<ArgumentException>(() => new PiecewiseYieldCurve<ForwardRate, ConvexMonotone, LocalBootstrap>(
                settlement, helpers, new Actual360(), context, bootstrap: new LocalBootstrap(0))).ParamName);
        Assert.Equal("localisation", Assert.Throws<ArgumentOutOfRangeException>(() => new LocalBootstrap(-1)).ParamName);

        // QuantLib's setup checks: more helpers than the localisation
        NanoQLException few = Assert.Throws<NanoQLException>(
            () => new PiecewiseYieldCurve<ForwardRate, ConvexMonotone, LocalBootstrap>(
                settlement, Deposits(context, 2), new Actual360(), context));
        Assert.Equal("not enough instruments: 2 provided, 2 required.", few.Message);

        // and distinct pillars
        List<RateHelper> twice = Deposits(context, 4);
        twice.Add(Deposits(context, 1)[0]);
        NanoQLException duplicate = Assert.Throws<NanoQLException>(
            () => new PiecewiseYieldCurve<ForwardRate, ConvexMonotone, LocalBootstrap>(settlement, twice, new Actual360(), context));
        Assert.StartsWith("two instruments have the same pillar date", duplicate.Message);

        // the zero value is QuantLib's default construction
        LocalBootstrap b = default;
        Assert.Equal((2, true, (double?)null), (b.Localisation, b.ForcePositive, b.Accuracy));
    }

    [Fact]
    public void ALocalInterpolationExtendsOnlyAConvexMonotoneOne()
    {
        double[] x = [0.0, 1.0, 2.0, 3.0], y = [0.0, 0.02, 0.025, 0.03];
        Interpolation linear = default(Linear).Interpolate(x.AsSpan(0, 3), y.AsSpan(0, 3));
        Assert.Equal(
            "previous",
            Assert.Throws<ArgumentException>(() => default(ConvexMonotone).LocalInterpolate(x, y, 2, linear, 4)).ParamName);
    }

    private static List<RateHelper> Deposits(EvaluationContext context, int count) =>
    [
        .. Enumerable.Range(1, count).Select(m => (RateHelper)new DepositRateHelper(
            0.02 + (0.001 * m), new Period(m, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing,
            false, new Actual360(), context)),
    ];
}
