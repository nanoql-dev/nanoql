// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/swingoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using RsgType = NanoQL.Numerics.InverseCumulativeRsg<
    NanoQL.Numerics.RandomSequenceGenerator<NanoQL.Numerics.MersenneTwisterUniformRng>,
    NanoQL.Numerics.InverseCumulativeNormal>;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class SwingOptionTests
{
    [Fact]
    public void TestExtendedOrnsteinUhlenbeckProcess()
    {
        // Testing extended Ornstein-Uhlenbeck process...
        const Real speed = 2.5;
        const Real vol = 0.70;
        const Real level = 1.43;

        ExtendedOrnsteinUhlenbeckProcessDiscretization[] discr =
        [
            ExtendedOrnsteinUhlenbeckProcessDiscretization.MidPoint,
            ExtendedOrnsteinUhlenbeckProcessDiscretization.Trapezodial,
            ExtendedOrnsteinUhlenbeckProcessDiscretization.GaussLobatto,
        ];

        Func<Real, Real>[] f = [x => level, x => x + 1.0, Math.Sin];

        for (Size n = 0; n < f.Length; ++n)
        {
            var refProcess = new ExtendedOrnsteinUhlenbeckProcess(
                speed, vol, 0.0, f[n], ExtendedOrnsteinUhlenbeckProcessDiscretization.GaussLobatto, 1e-6);

            for (Size i = 0; i < discr.Length - 1; ++i)
            {
                var eouProcess = new ExtendedOrnsteinUhlenbeckProcess(speed, vol, 0.0, f[n], discr[i]);

                const Real T = 10;
                const Size nTimeSteps = 10000;

                const Real dt = T / nTimeSteps;
                Real t = 0.0;
                Real q = 0.0;
                Real p = 0.0;

                var rng = new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(
                    new MersenneTwisterUniformRng(1234U));

                for (Size j = 0; j < nTimeSteps; ++j)
                {
                    Real dw = rng.Next().Value;
                    q = eouProcess.Evolve(t, q, dt, dw);
                    p = refProcess.Evolve(t, p, dt, dw);

                    Assert.False(Math.Abs(q - p) > 1e-6, $"invalid process evaluation {n} {i} {j} {q - p}");
                    t += dt;
                }
            }
        }
    }

    [Fact]
    public void TestFdmExponentialJump1dMesher()
    {
        // Testing finite difference mesher for the Kluge model...
        var x = new Vector(2, 1.0);
        const Real beta = 100.0;
        const Real eta = 1.0 / 0.4;
        const Real jumpIntensity = 4.0;
        const Size dummySteps = 2;

        var mesher = new ExponentialJump1dMesher(dummySteps, beta, jumpIntensity, eta);

        var ouProcess = new ExtendedOrnsteinUhlenbeckProcess(1.0, 1.0, x[0], ConstantB(1.0));
        var jumpProcess = new ExtOUWithJumpsProcess(ouProcess, x[1], beta, jumpIntensity, eta);

        const Real dt = 1.0 / (10.0 * beta);
        const Size n = 1000000;

        var path = new Real[n];
        var mt = new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(
            new MersenneTwisterUniformRng(123));
        var dw = new Vector(3);
        for (Size i = 0; i < n; ++i)
        {
            dw[0] = mt.Next().Value;
            dw[1] = mt.Next().Value;
            dw[2] = mt.Next().Value;
            path[i] = (x = jumpProcess.Evolve(0.0, x, dt, dw))[1];
        }

        Array.Sort(path);

        const Real relTol1 = 2e-3;
        const Real relTol2 = 2e-2;
        const Real threshold = 0.9;

        for (Real xx = 1e-12; xx < 1.0; xx *= 10)
        {
            Real v = mesher.JumpSizeDistribution(xx);

            // std::lower_bound: the first element not less than xx
            int iter = LowerBound(path, xx);
            Real q = iter / (Real)n;
            Assert.True(
                Math.Abs(q - v) < relTol1 || (v < threshold && Math.Abs(q - v) < relTol2),
                "can not reproduce jump distribution");
        }
    }

    [Fact]
    public void TestExtOUJumpVanillaEngine()
    {
        // Testing finite difference pricer for the Kluge model...
        ExtOUWithJumpsProcess jumpProcess = CreateKlugeProcess();

        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        Date maturityDate = today + new Period(12, TimeUnit.Months);
        Real maturity = dc.YearFraction(today, maturityDate);

        const Rate irRate = 0.1;
        YieldTermStructure rTS = new FlatForward(today, irRate, dc);
        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, 30);
        var exercise = new EuropeanExercise(maturityDate);

        var option = new VanillaOption(payoff, exercise);
        Real fdNPV = FdExtOUJumpVanillaEngine.Calculate(option, context, jumpProcess, rTS, 25, 200, 50).NPV!.Value;

        const Size steps = 100;
        const Size nrTrails = 200000;
        var grid = new TimeGrid(maturity, steps);

        RsgType rsg = PseudoRandom.MakeSequenceGenerator(jumpProcess.Factors * (grid.Size - 1), 421);

        var npv = new GeneralStatistics();
        var generator = new MultiPathGenerator<RsgType>(jumpProcess, grid, rsg, false);

        for (Size n = 0; n < nrTrails; ++n)
        {
            MultiPath path = generator.Next().Value;

            Real x = path[0].Back();
            Real y = path[1].Back();

            Real cashflow = payoff.Evaluate(Math.Exp(x + y));
            npv.Add(cashflow * rTS.Discount(maturity));
        }

        Real mcNPV = npv.Mean();
        Real mcError = npv.ErrorEstimate();

        Assert.False(
            Math.Abs(fdNPV - mcNPV) > 3.0 * mcError,
            "Failed to reproduce FD and MC prices"
            + $"\n    FD NPV: {fdNPV}"
            + $"\n    MC NPV: {mcNPV} +/- {mcError}");
    }

    [Fact]
    public void TestFdBSSwingOption()
    {
        // Testing Black-Scholes vanilla swing option pricing...
        Date settlementDate = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = settlementDate };
        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        Date maturityDate = settlementDate + new Period(12, TimeUnit.Months);

        const Real strike = 30;
        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Put, strike);
        StrikedTypePayoff forward = new VanillaForwardPayoff(OptionType.Put, strike);

        List<Date> exerciseDates = [settlementDate + new Period(1, TimeUnit.Months)];
        while (exerciseDates[^1] < maturityDate)
        {
            exerciseDates.Add(exerciseDates[^1] + new Period(1, TimeUnit.Months));
        }

        var swingExercise = new SwingExercise(exerciseDates);

        YieldTermStructure riskFreeTS = FlatForward.Create(0, new NullCalendar(), 0.14, dayCounter, context);
        YieldTermStructure dividendTS = FlatForward.Create(0, new NullCalendar(), 0.02, dayCounter, context);
        BlackVolTermStructure volTS = new BlackConstantVol(settlementDate, new NullCalendar(), 0.4, dayCounter);

        const Real s0 = 30.0;

        var process = new BlackScholesMertonProcess(s0, dividendTS, riskFreeTS, volTS);

        var bermudanOption = new VanillaOption(payoff, swingExercise);
        Real bermudanOptionPrices = FdBlackScholesVanillaEngine.Calculate(
            bermudanOption, context, process, tGrid: 50, xGrid: 200).NPV!.Value;

        for (Size i = 0; i < exerciseDates.Count; ++i)
        {
            Size exerciseRights = i + 1;

            var swingOption = new VanillaSwingOption(forward, swingExercise, 0, exerciseRights);
            Real swingOptionPrice = FdSimpleBSSwingEngine.Calculate(swingOption, context, process, 50, 200).NPV!.Value;

            Real upperBound = exerciseRights * bermudanOptionPrices;

            Assert.False(
                swingOptionPrice - upperBound > 0.01,
                "Failed to reproduce upper bounds"
                + $"\n    upper Bound: {upperBound}"
                + $"\n    Price:       {swingOptionPrice}"
                + $"\n    diff:        {swingOptionPrice - upperBound}");

            Real lowerBound = 0.0;
            for (Size j = exerciseDates.Count - i - 1; j < exerciseDates.Count; ++j)
            {
                var europeanOption = new VanillaOption(payoff, new EuropeanExercise(exerciseDates[j]));
                lowerBound += AnalyticEuropeanEngine.Calculate(europeanOption, context, process).NPV!.Value;
            }

            Assert.False(
                lowerBound - swingOptionPrice > 4e-2,
                "Failed to reproduce lower bounds"
                + $"\n    lower Bound: {lowerBound}"
                + $"\n    Price:       {swingOptionPrice}"
                + $"\n    diff:        {lowerBound - swingOptionPrice}");
        }
    }

    [Fact]
    public void TestExtOUJumpSwingOption()
    {
        // Testing simple swing option pricing for Kluge model...
        Date settlementDate = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = settlementDate };
        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);
        Date maturityDate = settlementDate + new Period(12, TimeUnit.Months);

        const Real strike = 30;
        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Put, strike);
        StrikedTypePayoff forward = new VanillaForwardPayoff(OptionType.Put, strike);

        List<Date> exerciseDates = [settlementDate + new Period(1, TimeUnit.Months)];
        while (exerciseDates[^1] < maturityDate)
        {
            exerciseDates.Add(exerciseDates[^1] + new Period(1, TimeUnit.Months));
        }

        var swingExercise = new SwingExercise(exerciseDates);

        var exerciseTimes = new Real[exerciseDates.Count];
        for (Size i = 0; i < exerciseTimes.Length; ++i)
        {
            exerciseTimes[i] = dayCounter.YearFraction(settlementDate, exerciseDates[i]);
        }

        var grid = new TimeGrid(exerciseTimes, 60);
        var exerciseIndex = new Size[exerciseDates.Count];
        for (Size i = 0; i < exerciseIndex.Length; ++i)
        {
            exerciseIndex[i] = grid.ClosestIndex(exerciseTimes[i]);
        }

        ExtOUWithJumpsProcess jumpProcess = CreateKlugeProcess();

        const Rate irRate = 0.1;
        YieldTermStructure rTS = new FlatForward(settlementDate, irRate, dayCounter);

        var bermudanOption = new VanillaOption(payoff, swingExercise);
        Real bermudanOptionPrices =
            FdExtOUJumpVanillaEngine.Calculate(bermudanOption, context, jumpProcess, rTS, 25, 50, 25).NPV!.Value;

        const Size nrTrails = 16000;
        RsgType rsg = PseudoRandom.MakeSequenceGenerator(jumpProcess.Factors * (grid.Size - 1), 421);

        var generator = new MultiPathGenerator<RsgType>(jumpProcess, grid, rsg, false);

        for (Size i = 0; i < exerciseDates.Count; ++i)
        {
            Size exerciseRights = i + 1;

            var swingOption = new VanillaSwingOption(forward, swingExercise, 0, exerciseRights);
            Real swingOptionPrice =
                FdSimpleExtOUJumpSwingEngine.Calculate(swingOption, context, jumpProcess, rTS, 25, 50, 25).NPV!.Value;

            Real upperBound = exerciseRights * bermudanOptionPrices;

            Assert.False(
                swingOptionPrice - upperBound > 2e-2,
                "Failed to reproduce upper bounds"
                + $"\n    upper Bound: {upperBound}"
                + $"\n    Price:       {swingOptionPrice}");

            Real lowerBound = 0.0;
            for (Size j = exerciseDates.Count - i - 1; j < exerciseDates.Count; ++j)
            {
                var europeanOption = new VanillaOption(payoff, new EuropeanExercise(exerciseDates[j]));
                lowerBound += FdExtOUJumpVanillaEngine.Calculate(europeanOption, context, jumpProcess, rTS, 25, 50, 25).NPV!.Value;
            }

            Assert.False(
                lowerBound - swingOptionPrice > 2e-2,
                "Failed to reproduce lower bounds"
                + $"\n    lower Bound: {lowerBound}"
                + $"\n    Price:       {swingOptionPrice}");

            // use MC plus perfect forecast to find an upper bound
            var npv = new GeneralStatistics();
            for (Size n = 0; n < nrTrails; ++n)
            {
                MultiPath path = generator.Next().Value;

                var exerciseValues = new Real[exerciseTimes.Length];
                for (Size k = 0; k < exerciseTimes.Length; ++k)
                {
                    Real x = path[0][exerciseIndex[k]];
                    Real y = path[1][exerciseIndex[k]];
                    Real s = Math.Exp(x + y);

                    exerciseValues[k] = payoff.Evaluate(s) * rTS.Discount(exerciseDates[k]);
                }

                Array.Sort(exerciseValues, (a, c) => c.CompareTo(a));

                Real npCashFlows = 0.0;
                for (Size k = 0; k < exerciseRights; ++k)
                {
                    npCashFlows += exerciseValues[k];
                }

                npv.Add(npCashFlows);
            }

            Real mcUpperBound = npv.Mean();
            Real mcErrorUpperBound = npv.ErrorEstimate();
            Assert.False(
                swingOptionPrice - mcUpperBound > 2.36 * mcErrorUpperBound,
                "Failed to reproduce mc upper bounds"
                + $"\n    mc upper Bound: {mcUpperBound}"
                + $"\n    Price:          {swingOptionPrice}");
        }
    }

    [Fact]
    public void TestKlugeChFVanillaPricing()
    {
        // Testing Kluge PDE Vanilla Pricing in comparison to moment matching...
        var settlementDate = new Date(22, Month.November, 2019);
        var context = new EvaluationContext { EvaluationDate = settlementDate };
        DayCounter dayCounter = new Actual365Fixed();
        Date maturityDate = settlementDate + new Period(6, TimeUnit.Months);

        Real t = dayCounter.YearFraction(settlementDate, maturityDate);

        const Real f0 = 30;

        const Real x0 = 0.0;
        const Real y0 = 0.0;

        const Real beta = 5.0;
        const Real eta = 5.0;
        const Real lambda = 4.0;
        const Real alpha = 4.0;
        const Real sig = 1.0;

        var klugeProcess = new ExtOUWithJumpsProcess(
            new ExtendedOrnsteinUhlenbeckProcess(alpha, sig, x0, ConstantB(0.0)), y0, beta, lambda, eta);

        const Real strike = f0;

        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturityDate));

        Real ps = Math.Log(f0)
            - (sig * sig / (4 * alpha) * (1 - Math.Exp(-2 * alpha * t)))
            - (lambda / beta * Math.Log((eta - Math.Exp(-beta * t)) / (eta - 1.0)));
        (Real Time, Real Value)[] shape = [(t, ps)];

        Real expected = new RichardsonExtrapolation(x => SwingPdePricing(klugeProcess, option, shape, context, x), 4.0).Evaluate(2.0, 1.5);

        Real stdDev = Math.Sqrt(
            ((((2 - (2 * Math.Exp(-2 * beta * t))) * lambda) / (beta * eta * eta))
                + (((1 - Math.Exp(-2 * alpha * t)) * sig * sig) / alpha)) / 2.0);

        Real bsNPV = BlackFormulaFunctions.BlackFormula(OptionType.Call, strike, f0, stdDev);

        Real g1 = ((2 - (2 * Math.Exp(-3 * beta * t))) * lambda) / (beta * eta * eta * eta) / (stdDev * stdDev * stdDev);

        Real g2 = (3 * ((Math.Exp((alpha + beta) * t)
                    * Squared((2 * alpha * Math.Exp(2 * alpha * t) * (-1 + Math.Exp(2 * beta * t)) * lambda)
                        + (beta * Math.Exp(2 * beta * t) * (-1 + Math.Exp(2 * alpha * t)) * eta * eta * sig * sig)))
                + (16 * alpha * alpha * beta * Math.Exp(((5 * alpha) + (3 * beta)) * t) * lambda * Math.Sinh(2 * beta * t)))
            / (4.0 * alpha * alpha * beta * beta * Math.Exp(5 * (alpha + beta) * t) * eta * eta * eta * eta)
            / (stdDev * stdDev * stdDev * stdDev)) - 3.0;

        Real d = (Math.Log(f0 / strike) + (0.5 * stdDev * stdDev)) / stdDev;

        // Jurczenko E., Maillet B. and Negrea B.,
        // Multi-Moment Approximate Option Pricing Models:
        // A General Comparison (Part 1)
        // https://papers.ssrn.com/sol3/papers.cfm?abstract_id=300922
        var nd = new NormalDistribution();
        Real q3 = 1 / Factorial.Get(3) * f0 * stdDev * ((2 * stdDev) - d) * nd.Evaluate(d);
        Real q4 = 1 / Factorial.Get(4) * f0 * stdDev * ((d * d) - (3 * d * stdDev) - 1) * nd.Evaluate(d);
        Real q5 = 10 / Factorial.Get(6) * f0 * stdDev
            * ((d * d * d * d) - (5 * d * d * d * stdDev) - (6 * d * d) + (15 * d * stdDev) + 3) * nd.Evaluate(d);

        // Corrado C. and T. Su, (1996-b),
        // "Skewness and Kurtosis in S&P 500 IndexReturns Implied by Option Prices",
        // Journal of Financial Research 19 (2), 175-192.
        Real ccs3 = bsNPV + (g1 * q3);
        Real ccs4 = ccs3 + (g2 * q4);

        // Rubinstein M., (1998), "Edgeworth Binomial Trees",
        // Journal of Derivatives 5 (3), 20-27.
        Real cr = ccs4 + (g1 * g1 * q5);

        Real expectedImplVol = BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(OptionType.Call, strike, f0, expected, 1.0) / Math.Sqrt(t);
        Real bsImplVol = BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(OptionType.Call, strike, f0, bsNPV, 1.0) / Math.Sqrt(t);
        Real ccs3ImplVol = BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(OptionType.Call, strike, f0, ccs3, 1.0) / Math.Sqrt(t);
        Real ccs4ImplVol = BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(OptionType.Call, strike, f0, ccs4, 1.0) / Math.Sqrt(t);
        Real crImplVol = BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(OptionType.Call, strike, f0, cr, 1.0) / Math.Sqrt(t);

        Real[] tol = [0.01, 0.0075, 0.005, 0.004];
        string[] methods = ["Second Order", "Third Order", "Fourth Order", "Rubinstein"];
        Real[] calculated = [bsImplVol, ccs3ImplVol, ccs4ImplVol, crImplVol];

        for (Size i = 0; i < 4; ++i)
        {
            Real diff = Math.Abs(calculated[i] - expectedImplVol);
            Assert.False(
                diff > tol[i],
                "failed to reproduce vanilla option implied volatility with moment matching"
                + $"\n    calculated: {calculated[i]}"
                + $"\n    expected:   {expectedImplVol}"
                + $"\n    difference: {diff}"
                + $"\n    tolerance:  {tol[i]}"
                + $"\n    method:     {methods[i]}");
        }
    }

    private static Func<Real, Real> ConstantB(Real b) => x => b;

    private static ExtOUWithJumpsProcess CreateKlugeProcess()
    {
        Real[] x0 = [3.0, 0.0];

        const Real beta = 5.0;
        const Real eta = 2.0;
        const Real jumpIntensity = 1.0;
        const Real speed = 1.0;
        const Real volatility = 2.0;

        var ouProcess = new ExtendedOrnsteinUhlenbeckProcess(speed, volatility, x0[0], ConstantB(x0[0]));
        return new ExtOUWithJumpsProcess(ouProcess, x0[1], beta, jumpIntensity, eta);
    }

    // QuantLib's SwingPdePricing: the vanilla engine on a zero rate at grids scaled by 1/x
    private static Real SwingPdePricing(
        ExtOUWithJumpsProcess process, VanillaOption option, (Real Time, Real Value)[] shape, EvaluationContext context, Real x)
    {
        YieldTermStructure rTS = FlatForward.Create(0, new NullCalendar(), 0.0, new Actual365Fixed(), context);
        const Size gridX = 200;
        const Size gridY = 100;
        const Size gridT = 100;

        return FdExtOUJumpVanillaEngine.Calculate(
            option, context, process, rTS, (Size)(gridT / x), (Size)(gridX / x), (Size)(gridY / x), shape).NPV!.Value;
    }

    private static Real Squared(Real x) => x * x;

    private static int LowerBound(Real[] sorted, Real value)
    {
        int lo = 0, hi = sorted.Length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) / 2);
            if (sorted[mid] < value)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}
