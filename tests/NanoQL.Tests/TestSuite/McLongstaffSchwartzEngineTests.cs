// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/mclongstaffschwartzengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class McLongstaffSchwartzEngineTests
{
    // The calibration seed QuantLib's cases run on: its default, the narrowed Null<Size>() (the S-van plan's VR4 (v)),
    // which nanoql's default replaces by the seed plus 1768237423; the cases keep their seeds (VD19).
    private const long QuantLibsCalibrationSeed = 4294967295;

    [Fact]
    public void TestAmericanOption()
    {
        // Testing Monte-Carlo pricing of American options...

        // most of the example taken from the EquityOption.cpp
        OptionType type = OptionType.Put;
        Real underlying = 36;
        Spread dividendYield = 0.00;
        Rate riskFreeRate = 0.06;
        Volatility volatility = 0.20;

        var todaysDate = new Date(15, May, 1998);
        var settlementDate = new Date(17, May, 1998);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        var maturity = new Date(17, May, 1999);
        DayCounter dayCounter = new Actual365Fixed();

        Exercise americanExercise = new AmericanExercise(settlementDate, maturity);

        // bootstrap the yield/dividend/vol curves
        YieldTermStructure flatTermStructure = new FlatForward(settlementDate, riskFreeRate, dayCounter);
        YieldTermStructure flatDividendTS = new FlatForward(settlementDate, dividendYield, dayCounter);

        // expected results for exercise probability, evaluated with third-party
        // product (using Cox-Rubinstein binomial tree)
        var expectedExProb = new Matrix(2, 3);
        expectedExProb[0, 0] = 0.48013; // (price: 2.105)
        expectedExProb[0, 1] = 0.51678; // (price: 3.451)
        expectedExProb[0, 2] = 0.54598; // (price: 4.807)
        expectedExProb[1, 0] = 0.75549; // (price: 4.505)
        expectedExProb[1, 1] = 0.67569; // (price: 5.764)
        expectedExProb[1, 2] = 0.65562; // (price: 7.138)

        LsmBasisSystemPolynomialType[] polynomialTypes =
        [
            LsmBasisSystemPolynomialType.Monomial, LsmBasisSystemPolynomialType.Laguerre,
            LsmBasisSystemPolynomialType.Hermite, LsmBasisSystemPolynomialType.Hyperbolic,
            LsmBasisSystemPolynomialType.Chebyshev2nd,
        ];

        for (Integer i = 0; i < 2; ++i)
        {
            for (Integer j = 0; j < 3; ++j)
            {
                BlackVolTermStructure flatVolTS =
                    new BlackConstantVol(settlementDate, new NullCalendar(), volatility + (0.1 * j), dayCounter);

                StrikedTypePayoff payoff = new PlainVanillaPayoff(type, underlying + (4 * i));

                var stochasticProcess =
                    new GeneralizedBlackScholesProcess(underlying, flatDividendTS, flatTermStructure, flatVolTS);

                var americanOption = new VanillaOption(payoff, americanExercise);

                Func<VanillaOption, EvaluationContext, OneAssetOptionResults> mcengine =
                    new MakeMCAmericanEngine<PseudoRandom, GeneralStatistics, PseudoRandom>(stochasticProcess, 42)
                        .WithSteps(75)
                        .WithAntitheticVariate()
                        .WithAbsoluteTolerance(0.02)
                        .WithPolynomialOrder(3)
                        .WithBasisSystem(polynomialTypes[0 * ((i * 3) + j) % polynomialTypes.Length])
                        .WithSeedCalibration(QuantLibsCalibrationSeed)
                        .ToEngine();

                OneAssetOptionResults results = mcengine(americanOption, context);
                Real calculated = results.NPV!.Value;
                Real errorEstimate = results.ErrorEstimate!.Value;
                Real exerciseProbability = results.Result<double>("exerciseProbability");

                Real expected = FdBlackScholesVanillaEngine.Calculate(
                    americanOption, context, stochasticProcess, tGrid: 401, xGrid: 200).NPV!.Value;

                // Check price
                if (Math.Abs(calculated - expected) > 2.34 * errorEstimate)
                {
                    Assert.Fail(
                        $"Failed to reproduce american option prices\n    expected: {expected}\n    calculated:   "
                        + $"{calculated} +/- {errorEstimate}");
                }

                // Check exercise probability (tolerance 1.5%)
                if (Math.Abs(exerciseProbability - expectedExProb[i, j]) > 0.015)
                {
                    Assert.Fail(
                        $"Failed to reproduce american option exercise probability\n    expected: {expectedExProb[i, j]}"
                        + $"\n    calculated:   {exerciseProbability}");
                }
            }
        }
    }

    [Fact]
    public void TestAmericanMaxOption()
    {
        // reference values taken from
        // "Monte Carlo Methods in Financial Engineering",
        // by Paul Glasserman, 2004 Springer Verlag, p. 462

        // Testing Monte-Carlo pricing of American max options...

        // most of the example taken from the EquityOption.cpp
        OptionType type = OptionType.Call;
        Real strike = 100;
        Spread dividendYield = 0.10;
        Rate riskFreeRate = 0.05;
        Volatility volatility = 0.20;

        var todaysDate = new Date(15, May, 1998);
        var settlementDate = new Date(17, May, 1998);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        var maturity = new Date(16, May, 2001);
        DayCounter dayCounter = new Actual365Fixed();

        Exercise americanExercise = new AmericanExercise(settlementDate, maturity);

        // bootstrap the yield/dividend/vol curves
        YieldTermStructure flatTermStructure = new FlatForward(settlementDate, riskFreeRate, dayCounter);
        YieldTermStructure flatDividendTS = new FlatForward(settlementDate, dividendYield, dayCounter);

        BlackVolTermStructure flatVolTS = new BlackConstantVol(settlementDate, new NullCalendar(), volatility, dayCounter);

        StrikedTypePayoff payoff = new PlainVanillaPayoff(type, strike);

        Size numberAssets = 2;
        var corr = new Matrix(numberAssets, numberAssets, 0.0);
        for (Size i = 0; i < numberAssets; ++i)
        {
            corr[i, i] = 1.0;
        }

        var americanMaxOption = new VanillaOption(payoff, americanExercise);

        Real[] expected = [8.08, 13.90, 21.34];
        for (Size i = 0; i < 3; ++i)
        {
            // QuantLib relinks the underlying's quote; here the processes are built for it
            Real underlying = 90.0 + (i * 10.0);
            var stochasticProcess =
                new GeneralizedBlackScholesProcess(underlying, flatDividendTS, flatTermStructure, flatVolTS);
            var process = new StochasticProcessArray(
                [.. Enumerable.Repeat<StochasticProcess1D>(stochasticProcess, numberAssets)], corr);

            var mcengine = new MCAmericanMaxEngine<PseudoRandom>(
                americanMaxOption, context, process, 25, null, false, true, false, 4096, null, null, 42, 1024, QuantLibsCalibrationSeed);

            (Real calculated, Real errorEstimate) = mcengine.Price();
            if (Math.Abs(calculated - expected[i]) > 2.34 * errorEstimate)
            {
                Assert.Fail(
                    $"Failed to reproduce american option prices\n    expected: {expected[i]}\n    calculated:   "
                    + $"{calculated} +/- {errorEstimate}");
            }
        }
    }

    private sealed class AmericanMaxPathPricer(Payoff payoff) : IEarlyExercisePathPricer<MultiPath, Vector>
    {
        public Vector State(MultiPath path, Size t)
        {
            var tmp = new Vector(path.AssetNumber);
            for (Size i = 0; i < path.AssetNumber; ++i)
            {
                tmp[i] = path[i][t];
            }

            return tmp;
        }

        public Real Evaluate(MultiPath path, Size t)
        {
            Vector tmp = State(path, t);
            return payoff.Evaluate(tmp.AsSpan().ToArray().Max());
        }

        public ImmutableArray<Func<Vector, Real>> BasisSystem() =>
            LsmBasisSystem.MultiPathBasisSystem(2, 2, LsmBasisSystemPolynomialType.Monomial);
    }

    private sealed class MCAmericanMaxEngine<TRng>(
        VanillaOption option,
        EvaluationContext context,
        StochasticProcessArray processes,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianbridge,
        bool antitheticVariate,
        bool controlVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed,
        int? nCalibrationSamples = null,
        long? seedCalibration = null)
        : MCLongstaffSchwartzEngine<VanillaOption, MultiVariate<TRng>, MultiVariate<TRng>, MultiPath, Vector, GeneralStatistics>(
            option,
            context,
            processes,
            timeSteps,
            timeStepsPerYear,
            brownianbridge,
            antitheticVariate,
            controlVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            seed,
            nCalibrationSamples,
            seedCalibration: seedCalibration)
        where TRng : IRngTraits
    {
        // the value and its error estimate, QuantLib's NPV() and errorEstimate()
        public (Real Value, Real ErrorEstimate) Price()
        {
            MonteCarloModel<MultiPath, GeneralStatistics> model = Run().Model;
            return (model.SampleAccumulator.Mean(), model.SampleAccumulator.ErrorEstimate());
        }

        protected override LongstaffSchwartzPathPricer<MultiPath, Vector> LsmPathPricer()
        {
            var processArray = Process as StochasticProcessArray;
            if (!(processArray is not null && processArray.Size > 0))
            {
                throw new NanoQLException("Stochastic process array required");
            }

            if (processArray.Process(0) is not GeneralizedBlackScholesProcess process)
            {
                throw new NanoQLException("generalized Black-Scholes proces required");
            }

            var earlyExercisePathPricer = new AmericanMaxPathPricer(Option.Payoff!);

            NanoQL.TimeGrid grid = TimeGrid();
            return new LongstaffSchwartzPathPricer<MultiPath, Vector>(
                grid, earlyExercisePathPricer, process.RiskFreeRate, ExerciseTimes(grid));
        }
    }
}
