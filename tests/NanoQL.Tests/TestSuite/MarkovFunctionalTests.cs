// SPDX-FileCopyrightText: Copyright (C) 2012 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/markovfunctional.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testVanillaEngines (S-g1d's Gaussian1dCapFloorEngine)

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class MarkovFunctionalTests
{
    [Fact]
    public void TestMfStateProcess()
    {
        const Real tolerance = 1E-10;

        // Testing Markov functional state process...
        Real[] times1 = [], vols1 = [1.0];
        var sp1 = new MfStateProcess(0.00, times1, vols1);
        Real var11 = sp1.Variance(0.0, 0.0, 1.0);
        Real var12 = sp1.Variance(0.0, 0.0, 2.0);
        if (Math.Abs(var11 - 1.0) > tolerance)
        {
            Assert.Fail($"process 1 has not variance 1.0 for dt = 1.0 but {var11}");
        }

        if (Math.Abs(var12 - 2.0) > tolerance)
        {
            Assert.Fail($"process 1 has not variance 1.0 for dt = 1.0 but {var12}");
        }

        Real[] times2 = [1.0, 2.0], vols2 = [1.0, 2.0, 3.0];
        var sp2 = new MfStateProcess(0.00, times2, vols2);
        (Real T, Real Expected)[] diffusions = [(0.0, 1.0), (0.99, 1.0), (1.0, 2.0), (1.9, 2.0), (2.0, 3.0), (3.0, 3.0), (5.0, 3.0)];
        foreach ((Real t, Real expected) in diffusions)
        {
            Real dif = sp2.Diffusion(t, 0.0);
            if (Math.Abs(dif - expected) > tolerance)
            {
                Assert.Fail($"process 2 has wrong drift at {t}, should be {expected} but is {dif}");
            }
        }

        (Real T0, Real Dt, Real Expected)[] variances2 =
            [(0.0, 0.0, 0.0), (0.0, 0.5, 0.5), (0.0, 1.0, 1.0), (0.0, 1.5, 3.0), (0.0, 3.0, 14.0), (0.0, 5.0, 32.0), (1.2, 1.0, 5.0)];
        foreach ((Real t0, Real dt, Real expected) in variances2)
        {
            Real v = sp2.Variance(t0, 0.0, dt);
            if (Math.Abs(v - expected) > tolerance)
            {
                Assert.Fail($"process 2 has wrong variance at {t0} + {dt}, should be {expected} but is {v}");
            }
        }

        var sp3 = new MfStateProcess(0.01, times2, vols2);
        (Real T0, Real Dt, Real Expected)[] variances3 =
        [
            (0.0, 0.0, 0.0), (0.0, 0.5, 0.502508354208), (0.0, 1.0, 1.01006700134), (0.0, 1.5, 3.06070578669),
            (0.0, 3.0, 14.5935513933), (0.0, 5.0, 34.0940185819), (1.2, 1.0, 5.18130257358),
        ];
        foreach ((Real t0, Real dt, Real expected) in variances3)
        {
            Real v = sp3.Variance(t0, 0.0, dt);
            if (Math.Abs(v - expected) > tolerance)
            {
                Assert.Fail($"process 3 has wrong variance at {t0} + {dt}, should be {expected} but it {v:G12}");
            }
        }
    }

    [Fact]
    public void TestKahaleSmileSection()
    {
        // Testing Kahale smile section...
        const Real tol = 1E-8;

        // arbitrage free sample smile data
        const Real atm = 0.05;
        const Real t = 1.0;

        Real[] strikes = [0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07, 0.08, 0.09, 0.10];
        var money = new List<Real>();
        var calls0 = new List<Real>();

        foreach (Real strike in strikes)
        {
            money.Add(strike / atm);
            calls0.Add(BlackFormula(OptionType.Call, strike, atm, 0.50 * Math.Sqrt(t), 1.0, 0.0));
        }

        Real[] stdDevs0 = ImpliedStdDevs(atm, strikes, [.. calls0]);
        SmileSection sec1 = new InterpolatedSmileSection<Linear>(t, strikes, stdDevs0, atm);

        // test arbitrage free smile reproduction
        var ksec11 = new KahaleSmileSection(sec1, atm, false, false, false, [.. money]);

        if (Math.Abs(ksec11.LeftCoreStrike() - 0.01) > tol)
        {
            Assert.Fail($"smile11 left af strike is {ksec11.LeftCoreStrike()} expected 0.01");
        }

        if (Math.Abs(ksec11.RightCoreStrike() - 0.10) > tol)
        {
            Assert.Fail($"smile11 right af strike is {ksec11.RightCoreStrike()} expected 0.10");
        }

        Real k = strikes[0];
        while (k <= strikes[^1] + tol)
        {
            Real pric0 = sec1.OptionPrice(k);
            Real pric1 = ksec11.OptionPrice(k);
            if (Math.Abs(pric0 - pric1) > tol)
            {
                Assert.Fail($"smile11 is not reprocduced at strike {k} input smile call price is  {pric0} kahale smile call price is {pric1}");
            }

            k += 0.0001;
        }

        // test interpolation
        var ksec12 = new KahaleSmileSection(sec1, atm, true, false, false, [.. money]);

        // sanity check for left point extrapolation may mark 0.01 as bad as well as
        // good depending
        // on platform and compiler due to numerical differences, so we have to
        // admit two possible results
        if (Math.Abs(ksec12.LeftCoreStrike() - 0.02) > tol && Math.Abs(ksec12.LeftCoreStrike() - 0.01) > tol)
        {
            Assert.Fail($"smile12 left af strike is {ksec12.LeftCoreStrike()}expected 0.01 or 0.02");
        }

        if (Math.Abs(ksec12.RightCoreStrike() - 0.10) > tol)
        {
            Assert.Fail($"smile12 right af strike is {ksec12.RightCoreStrike()}expected 0.10");
        }

        for (Size i = 1; i < strikes.Length; i++)
        {
            Real pric0 = sec1.OptionPrice(strikes[i]);
            Real pric1 = ksec12.OptionPrice(strikes[i]);
            if (Math.Abs(pric0 - pric1) > tol)
            {
                Assert.Fail($"smile12 is not reproduced on grid at strike {strikes[i]} input smile call price is {pric0} kahale smile call price is {pric1}");
            }
        }

        // test global no arbitrageability
        CheckDigitals(ksec11, ksec12, "11", "12", strikes);

        // test exponential extrapolation
        var ksec13 = new KahaleSmileSection(sec1, atm, false, true, false, [.. money]);

        k = strikes[^1];
        Real dig0 = ksec13.DigitalOptionPrice(k - 0.0010);
        while (k <= (10.0 * strikes[^1]) + tol)
        {
            Real dig = ksec13.DigitalOptionPrice(k);
            if (!(dig <= dig0 + tol && dig >= 0.0))
            {
                Assert.Fail($"arbitrage in digitals13 ({dig0},{dig}) at strike {k}");
            }

            k += 0.0001;
        }

        // test arbitrageable smile (leftmost point)
        Real[] calls1 = [.. calls0];
        calls1[0] = atm - strikes[0] + 0.0010; // introduce arbitrage by changing call price
        Real[] stdDevs1 = ImpliedStdDevs(atm, strikes, calls1);
        SmileSection sec2 = new InterpolatedSmileSection<Linear>(t, strikes, stdDevs1, atm);

        var ksec21 = new KahaleSmileSection(sec2, atm, false, false, false, [.. money]);
        var ksec22 = new KahaleSmileSection(sec2, atm, true, false, true, [.. money]);

        if (Math.Abs(ksec21.LeftCoreStrike() - 0.02) > tol)
        {
            Assert.Fail($"smile21 left af strike is {ksec21.LeftCoreStrike()} expected 0.02");
        }

        if (Math.Abs(ksec22.LeftCoreStrike() - 0.02) > tol)
        {
            Assert.Fail($"smile22 left af strike is {ksec22.LeftCoreStrike()} expected 0.02");
        }

        if (Math.Abs(ksec21.RightCoreStrike() - 0.10) > tol)
        {
            Assert.Fail($"smile21 right af strike is {ksec21.RightCoreStrike()} expected 0.10");
        }

        if (Math.Abs(ksec22.RightCoreStrike() - 0.10) > tol)
        {
            Assert.Fail($"smile22 right af strike is {ksec22.RightCoreStrike()} expected 0.10");
        }

        CheckDigitals(ksec21, ksec22, "21", "22", strikes);

        // test arbitrageable smile (second but rightmost point)
        Real[] calls2 = [.. calls0];
        calls2[8] = (0.9 * calls2[9]) + (0.1 * calls2[8]); // introduce arbitrage by changing call price
        Real[] stdDevs2 = ImpliedStdDevs(atm, strikes, calls2);
        SmileSection sec3 = new InterpolatedSmileSection<Linear>(t, strikes, stdDevs2, atm);

        var ksec31 = new KahaleSmileSection(sec3, atm, false, false, false, [.. money]);
        var ksec32 = new KahaleSmileSection(sec3, atm, true, false, true, [.. money]);

        if (Math.Abs(ksec31.LeftCoreStrike() - 0.01) > tol)
        {
            Assert.Fail($"smile31 left af strike is {ksec31.LeftCoreStrike()} expected 0.01");
        }

        // sanity check for left point extrapolation may mark 0.01 as bad as well as
        // good depending
        // on platform and compiler due to numerical differences, so we have to
        // admit two possible results
        if (Math.Abs(ksec32.LeftCoreStrike() - 0.02) > tol && Math.Abs(ksec32.LeftCoreStrike() - 0.01) > tol)
        {
            Assert.Fail($"smile32 left af strike is {ksec32.LeftCoreStrike()} expected 0.01 or 0.02");
        }

        if (Math.Abs(ksec31.RightCoreStrike() - 0.08) > tol)
        {
            Assert.Fail($"smile31 right af strike is {ksec31.RightCoreStrike()} expected 0.08");
        }

        if (Math.Abs(ksec32.RightCoreStrike() - 0.10) > tol)
        {
            Assert.Fail($"smile32 right af strike is {ksec32.RightCoreStrike()} expected 0.10");
        }

        CheckDigitals(ksec31, ksec32, "31", "32", strikes);
    }

    [Fact]
    public void TestSmileSectionUtilsWShapedSmile()
    {
        // Testing SmileSectionUtils with W-shaped smile...

        // A smile with arbitrage around the ATM point forces
        // SmileSectionUtils to push its central index toward the grid
        // boundary. Before the fix for #2184, the while loop at line 137
        // evaluated af() before the bounds check, causing an out-of-bounds
        // access in c_[]. The fix swaps the condition order so that the
        // bounds check short-circuits first.
        const Real atm = 0.05;
        const Real t = 1.0;

        Real[] strikes = [0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07, 0.08, 0.09, 0.10];
        Real[] vols0 = [0.35, 0.15, 0.40, 0.15, 0.35, 0.15, 0.40, 0.15, 0.35, 0.20];

        var money = new Real[strikes.Length];
        var calls = new Real[strikes.Length];
        for (Size i = 0; i < strikes.Length; i++)
        {
            money[i] = strikes[i] / atm;
            calls[i] = BlackFormula(OptionType.Call, strikes[i], atm, vols0[i] * Math.Sqrt(t), 1.0, 0.0);
        }

        Real[] stdDevs = ImpliedStdDevs(atm, strikes, calls);
        SmileSection sec = new InterpolatedSmileSection<Linear>(t, strikes, stdDevs, atm);

        // SmileSectionUtils must construct without crashing.
        // The central index will be pushed rightward through the
        // arbitrageable region; the fix ensures the loop terminates
        // at the boundary instead of reading past the end of c_[].
        var utils = new SmileSectionUtils(sec, money, atm);

        // The arbitrage-free region should be valid.
        (int left, int right) = utils.ArbitragefreeIndices();
        if (right <= left)
        {
            Assert.Fail($"arbitrage-free region is empty: left index {left}, right index {right}");
        }
    }

    // Adapted: the evaluation date (line 905) is the context's, under which the curves and volatility structures,
    // whose reference dates float in QuantLib, are built; each model calibrates when it is built, under the context,
    // and its outputs are computed for it.
    [Fact]
    public void TestCalibrationOneInstrumentSet()
    {
        const Real tol0 = 0.0001; // 1bp tolerance for model zero rates vs. market
                                  // zero rates (note that model zero rates are
                                  // implied by the calibration of the numeraire to
                                  // the smile)
        const Real tol1 = 0.0001; // 1bp tolerance for model call put premia vs. market premia

        // Testing Markov functional calibration to one instrument set...
        var referenceDate = new Date(14, November, 2012);
        var context = new EvaluationContext { EvaluationDate = referenceDate };

        YieldTermStructure flatYts = FlatYts(context);
        YieldTermStructure md0Yts = Md0Yts(context);
        SwaptionVolatilityStructure flatSwaptionVts = FlatSwaptionVts(context);
        SwaptionVolatilityStructure md0SwaptionVts = Md0SwaptionVts(md0Yts, context);
        OptionletVolatilityStructure flatOptionletVts = FlatOptionletVts(context);
        OptionletVolatilityStructure md0OptionletVts = Md0OptionletVts(md0Yts, context);

        SwapIndex swapIndexBase = new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years));
        IborIndex iborIndex = new Euribor(new Period(6, TimeUnit.Months));

        Date[] volStepDates = [];
        Real[] vols = [1.0];

        // use a grid with fewer points for smile arbitrage
        // testing and model outputs than the default grid
        // for the testing here
        Real[] money = [0.1, 0.25, 0.50, 0.75, 1.0, 1.25, 1.50, 2.0, 5.0];

        // Calibration Basket 1 / flat yts, vts
        var mf1 = new MarkovFunctional(
            flatYts,
            0.01,
            volStepDates,
            vols,
            flatSwaptionVts,
            ExpiriesCalBasket1(referenceDate),
            TenorsCalBasket1(),
            swapIndexBase,
            context,
            new MarkovFunctionalModelSettings()
                .WithYGridPoints(64) // we use the default values more or less, this
                                     // is just to demonstrate how to set the model
                                     // parameters
                .WithYStdDevs(7.0)
                .WithGaussHermitePoints(32)
                .WithDigitalGap(1e-5)
                .WithMarketRateAccuracy(1e-7)
                .WithLowerRateBound(0.0)
                .WithUpperRateBound(2.0)
                .WithAdjustments(MarkovFunctionalModelSettingsAdjustments.KahaleSmile | MarkovFunctionalModelSettingsAdjustments.SmileExponentialExtrapolation)
                .WithSmileMoneynessCheckpoints(money));

        MarkovFunctionalModelOutputs outputs1 = mf1.ModelOutputs(context); // this costs a lot of time, so only use it if you
                                                                           // want to check the calibration
        CheckOutputs(outputs1, "Basket 1 / flat termstructures", tol0, tol1);

        // Calibration Basket 2 / flat yts, vts
        var mf2 = new MarkovFunctional(
            flatYts,
            0.01,
            volStepDates,
            vols,
            flatOptionletVts,
            ExpiriesCalBasket2(referenceDate),
            iborIndex,
            context,
            new MarkovFunctionalModelSettings()
                .WithYGridPoints(64)
                .WithYStdDevs(7.0)
                .WithGaussHermitePoints(32)
                .WithDigitalGap(1e-5)
                .WithMarketRateAccuracy(1e-7)
                .WithLowerRateBound(0.0)
                .WithUpperRateBound(2.0)
                .WithAdjustments(MarkovFunctionalModelSettingsAdjustments.AdjustNone)
                .WithSmileMoneynessCheckpoints(money));

        MarkovFunctionalModelOutputs outputs2 = mf2.ModelOutputs(context);
        CheckOutputs(outputs2, "Basket 2 / flat termstructures", tol0, tol1);

        // Calibration Basket 1 / real yts, vts
        var mf3 = new MarkovFunctional(
            md0Yts,
            0.01,
            volStepDates,
            vols,
            md0SwaptionVts,
            ExpiriesCalBasket1(referenceDate),
            TenorsCalBasket1(),
            swapIndexBase,
            context,
            new MarkovFunctionalModelSettings()
                .WithYGridPoints(128) // use more points to increase accuracy
                .WithYStdDevs(7.0)
                .WithGaussHermitePoints(64)
                .WithDigitalGap(1e-5)
                .WithMarketRateAccuracy(1e-7)
                .WithLowerRateBound(0.0)
                .WithUpperRateBound(2.0)
                .WithSmileMoneynessCheckpoints(money));

        MarkovFunctionalModelOutputs outputs3 = mf3.ModelOutputs(context);
        CheckOutputs(outputs3, "Basket 1 / real termstructures", tol0, tol1);

        // Calibration Basket 2 / real yts, vts
        var mf4 = new MarkovFunctional(
            md0Yts,
            0.01,
            volStepDates,
            vols,
            md0OptionletVts,
            ExpiriesCalBasket2(referenceDate),
            iborIndex,
            context,
            new MarkovFunctionalModelSettings()
                .WithYGridPoints(64)
                .WithYStdDevs(7.0)
                .WithGaussHermitePoints(32)
                .WithDigitalGap(1e-5)
                .WithMarketRateAccuracy(1e-7)
                .WithLowerRateBound(0.0)
                .WithUpperRateBound(2.0)
                .WithSmileMoneynessCheckpoints(money));

        MarkovFunctionalModelOutputs outputs4 = mf4.ModelOutputs(context);
        CheckOutputs(outputs4, "Basket 2 / real termstructures", tol0, tol1);
    }

    // Adapted: the evaluation date (line 1408) is the context's, under which the curves, the volatility structures and
    // the helpers are built; each model calibrates its numeraire when it is built, and Calibrate returns the calibrated
    // model, which the secondary instruments are priced on, where QuantLib's engine holds the model calibrate changed.
    // As in QuantLib, the secondary set's mismatches are reported, not failed.
    [Fact]
    public void TestCalibrationTwoInstrumentSets()
    {
        const Real tol1 = 0.1; // 0.1 times vega tolerance for model vs. market in
                               // second instrument set

        // Testing Markov functional calibration to two instrument sets...
        var referenceDate = new Date(14, November, 2012);
        var context = new EvaluationContext { EvaluationDate = referenceDate };

        YieldTermStructure flatYts = FlatYts(context);
        YieldTermStructure md0Yts = Md0Yts(context);
        SwaptionVolatilityStructure flatSwaptionVts = FlatSwaptionVts(context);
        SwaptionVolatilityStructure md0SwaptionVts = Md0SwaptionVts(md0Yts, context);

        SwapIndex swapIndexBase = new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years));

        var target = new TARGET();
        Date[] volStepDates =
        [
            target.Advance(referenceDate, 1, TimeUnit.Years), target.Advance(referenceDate, 2, TimeUnit.Years),
            target.Advance(referenceDate, 3, TimeUnit.Years), target.Advance(referenceDate, 4, TimeUnit.Years),
        ];
        Real[] vols = [1.0, 1.0, 1.0, 1.0, 1.0];
        Real[] money = [0.1, 0.25, 0.50, 0.75, 1.0, 1.25, 1.50, 2.0, 5.0];

        var om = new LevenbergMarquardt();

        // ConjugateGradient om;
        var ec = new EndCriteria(1000, 500, 1e-2, 1e-2, 1e-2);

        // Calibration Basket 1 / flat yts, vts / Secondary calibration set consists
        // of coterminal swaptions
        IborIndex iborIndex1 = new Euribor(new Period(6, TimeUnit.Months), flatYts);

        Real[] calibrationHelperVols1 = [0.20, 0.20, 0.20, 0.20];
        BlackCalibrationHelper[] calibrationHelper1 = Helpers(calibrationHelperVols1, iborIndex1, flatYts, context);

        var mf1 = new MarkovFunctional(
            flatYts,
            0.01,
            volStepDates,
            vols,
            flatSwaptionVts,
            ExpiriesCalBasket1(referenceDate),
            TenorsCalBasket1(),
            swapIndexBase,
            context,
            new MarkovFunctionalModelSettings()
                .WithYGridPoints(64)
                .WithYStdDevs(7.0)
                .WithGaussHermitePoints(32)
                .WithDigitalGap(1e-5)
                .WithMarketRateAccuracy(1e-7)
                .WithLowerRateBound(0.0)
                .WithUpperRateBound(2.0)
                .WithSmileMoneynessCheckpoints(money));

        MarkovFunctional calibrated1 = mf1.Calibrate(calibrationHelper1, om, ec, context).Model;

        // std::cout << "Calibrated parameters 1: ";
        // Array params1 = mf1->params();
        // for(Size i=0;i<params1.size();i++) std::cout << params1[i] << ";";
        // std::cout << std::endl;
        Swaption[] ch1 = Coterminals(flatYts, context);
        CheckSecondarySet(ch1, calibrationHelperVols1, flatYts, calibrated1, context, "Basket 1 / flat yts, vts", tol1);

        // MarkovFunctional::ModelOutputs outputs1 = mf1->modelOutputs();
        // BOOST_TEST_MESSAGE(outputs1);

        // Calibration Basket 1 / real yts, vts / Secondary calibration set consists
        // of coterminal swaptions
        IborIndex iborIndex2 = new Euribor(new Period(6, TimeUnit.Months), md0Yts);

        var mf2 = new MarkovFunctional(
            md0Yts,
            0.01,
            volStepDates,
            vols,
            md0SwaptionVts,
            ExpiriesCalBasket1(referenceDate),
            TenorsCalBasket1(),
            swapIndexBase,
            context,
            new MarkovFunctionalModelSettings()
                .WithYGridPoints(64)
                .WithYStdDevs(7.0)
                .WithGaussHermitePoints(32)
                .WithDigitalGap(1e-5)
                .WithMarketRateAccuracy(1e-7)
                .WithLowerRateBound(0.0)
                .WithUpperRateBound(2.0)
                .WithSmileMoneynessCheckpoints(money));

        var cube = (SwaptionVolatilityCube)md0SwaptionVts;
        Real[] calibrationHelperVols2 = [.. new (int O, int S)[] { (1, 4), (2, 3), (3, 2), (4, 1) }.Select(p =>
            md0SwaptionVts.Volatility(
                new Period(p.O, TimeUnit.Years),
                new Period(p.S, TimeUnit.Years),
                cube.AtmStrike(new Period(p.O, TimeUnit.Years), new Period(p.S, TimeUnit.Years), context)))];

        BlackCalibrationHelper[] calibrationHelper2 = Helpers(calibrationHelperVols2, iborIndex2, md0Yts, context);

        MarkovFunctional calibrated2 = mf2.Calibrate(calibrationHelper2, om, ec, context).Model;

        // std::cout << "Calibrated parameters 2: ";
        // Array params2 = mf2->params();
        // for(Size i=0;i<params2.size();i++) std::cout << params2[i] << ";";
        // std::cout << std::endl;
        Swaption[] ch2 = Coterminals(md0Yts, context);
        CheckSecondarySet(ch2, calibrationHelperVols2, md0Yts, calibrated2, context, "Basket 1 / real yts, vts", tol1);

        // MarkovFunctional::ModelOutputs outputs2 = mf2->modelOutputs();
        // BOOST_TEST_MESSAGE(outputs2);
    }

    // Adapted: the evaluation date (line 1647) is the context's, under which the curves, the cube and the swap are
    // built; the model calibrates when it is built, under the context.
    [Fact]
    public void TestBermudanSwaption()
    {
        Real tol0 = 0.0001; // 1bp tolerance against cached values

        // Testing Markov functional Bermudan swaption engine...
        var referenceDate = new Date(14, November, 2012);
        var context = new EvaluationContext { EvaluationDate = referenceDate };

        YieldTermStructure md0Yts = Md0Yts(context);
        SwaptionVolatilityStructure md0SwaptionVts = Md0SwaptionVts(md0Yts, context);

        SwapIndex swapIndexBase = new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years));

        Date[] volStepDates = [];
        Real[] vols = [1.0];

        IborIndex iborIndex1 = new Euribor(new Period(6, TimeUnit.Months), md0Yts);

        var mf1 = new MarkovFunctional(
            md0Yts,
            0.01,
            volStepDates,
            vols,
            md0SwaptionVts,
            ExpiriesCalBasket3(referenceDate),
            TenorsCalBasket3(),
            swapIndexBase,
            context,
            new MarkovFunctionalModelSettings()
                .WithYGridPoints(32)
                .WithYStdDevs(7.0)
                .WithGaussHermitePoints(16)
                .WithMarketRateAccuracy(1e-7)
                .WithDigitalGap(1e-5)
                .WithLowerRateBound(0.0)
                .WithUpperRateBound(2.0));

        VanillaSwap underlyingCall = new MakeVanillaSwap(new Period(10, TimeUnit.Years), iborIndex1)
            .WithFixedRate(0.03)
            .WithEffectiveDate(new TARGET().Advance(referenceDate, 2, TimeUnit.Days))

            // .withNominal(100000000.0)
            .ReceiveFixed(false)
            .ToVanillaSwap(context);

        Date[] expiries = ExpiriesCalBasket3(referenceDate);
        var europeanSwaptions = new List<Swaption>();
        for (Size i = 0; i < expiries.Length; i++)
        {
            europeanSwaptions.Add(new Swaption(underlyingCall, new EuropeanExercise(expiries[i])));
        }

        var bermudanSwaption = new Swaption(underlyingCall, new BermudanExercise(expiries));

        Real[] cachedValues = [0.0030757, 0.0107344, 0.0179862, 0.0225881, 0.0243215, 0.0229148, 0.0191415, 0.0139035, 0.0076354];
        Real cachedValue = 0.0327776;

        for (Size i = 0; i < expiries.Length; i++)
        {
            Real npv = Gaussian1dSwaptionEngine.Calculate(europeanSwaptions[i], context, mf1, 64, 7.0).NPV!.Value;
            if (Math.Abs(npv - cachedValues[i]) > tol0)
            {
                Assert.Fail($"European swaption value ({npv}) deviates from cached value ({cachedValues[i]})");
            }
        }

        Real bermudan = Gaussian1dSwaptionEngine.Calculate(bermudanSwaption, context, mf1, 64, 7.0).NPV!.Value;
        if (Math.Abs(bermudan - cachedValue) > tol0)
        {
            Assert.Fail($"Bermudan swaption value ({bermudan}) deviates from cached value ({cachedValue})");
        }
    }

    // Flat yield term structure at 3%
    internal static YieldTermStructure FlatYts(EvaluationContext context) =>
        FlatForward.Create(0, new TARGET(), 0.03, new Actual365Fixed(), context);

    // Flat swaption volatility structure at 20%
    internal static SwaptionVolatilityStructure FlatSwaptionVts(EvaluationContext context) =>
        ConstantSwaptionVolatility.Create(0, new TARGET(), BusinessDayConvention.ModifiedFollowing, 0.20, new Actual365Fixed(), context);

    // Flat cap volatility structure at 20%
    internal static OptionletVolatilityStructure FlatOptionletVts(EvaluationContext context) =>
        ConstantOptionletVolatility.Create(0, new TARGET(), BusinessDayConvention.ModifiedFollowing, 0.20, new Actual365Fixed(), context);

    // Yield term structure as of 14.11.2012 (6m discounting)
    internal static YieldTermStructure Md0Yts(EvaluationContext context)
    {
        IborIndex euribor6mEmpty = new Euribor(new Period(6, TimeUnit.Months));

        Real[] q6mh =
        [
            0.0001, 0.0001, 0.0001, 0.0003, 0.00055, 0.0009, 0.0014, 0.0019, 0.0025, 0.0031, 0.00325, 0.00313, 0.0031,
            0.00307, 0.00309, 0.00339, 0.00316, 0.00326, 0.00335, 0.00343, 0.00358, 0.00351, 0.00388, 0.00404, 0.00425,
            0.00442, 0.00462, 0.00386, 0.00491, 0.00647, 0.00837, 0.01033, 0.01218, 0.01382, 0.01527, 0.01654, 0.0177,
            0.01872, 0.01959, 0.0203, 0.02088, 0.02132, 0.02164, 0.02186, 0.02202, 0.02213, 0.02222, 0.02229, 0.02234,
            0.02238, 0.02241, 0.02243, 0.02244, 0.02245, 0.02247, 0.0225, 0.02284, 0.02336, 0.02407, 0.0245,
        ];

        Period[] q6mh1 =
        [
            new(1, TimeUnit.Days), new(1, TimeUnit.Days), new(1, TimeUnit.Days), new(1, TimeUnit.Weeks), new(1, TimeUnit.Months),
            new(2, TimeUnit.Months), new(3, TimeUnit.Months), new(4, TimeUnit.Months), new(5, TimeUnit.Months), new(6, TimeUnit.Months),
        ];

        Period[] q6mh2 =
        [
            new(7, TimeUnit.Months), new(8, TimeUnit.Months), new(9, TimeUnit.Months), new(10, TimeUnit.Months), new(11, TimeUnit.Months),
            new(1, TimeUnit.Years), new(13, TimeUnit.Months), new(14, TimeUnit.Months), new(15, TimeUnit.Months), new(16, TimeUnit.Months),
            new(17, TimeUnit.Months), new(18, TimeUnit.Months), new(19, TimeUnit.Months), new(20, TimeUnit.Months), new(21, TimeUnit.Months),
            new(22, TimeUnit.Months), new(23, TimeUnit.Months), new(2, TimeUnit.Years), new(3, TimeUnit.Years), new(4, TimeUnit.Years),
            new(5, TimeUnit.Years), new(6, TimeUnit.Years), new(7, TimeUnit.Years), new(8, TimeUnit.Years), new(9, TimeUnit.Years),
            new(10, TimeUnit.Years), new(11, TimeUnit.Years), new(12, TimeUnit.Years), new(13, TimeUnit.Years), new(14, TimeUnit.Years),
            new(15, TimeUnit.Years), new(16, TimeUnit.Years), new(17, TimeUnit.Years), new(18, TimeUnit.Years), new(19, TimeUnit.Years),
            new(20, TimeUnit.Years), new(21, TimeUnit.Years), new(22, TimeUnit.Years), new(23, TimeUnit.Years), new(24, TimeUnit.Years),
            new(25, TimeUnit.Years), new(26, TimeUnit.Years), new(27, TimeUnit.Years), new(28, TimeUnit.Years), new(29, TimeUnit.Years),
            new(30, TimeUnit.Years), new(35, TimeUnit.Years), new(40, TimeUnit.Years), new(50, TimeUnit.Years), new(60, TimeUnit.Years),
        ];

        var target = new TARGET();
        var r6m = new List<BootstrapHelper<YieldTermStructure>>();
        for (int i = 0; i < 10; i++)
        {
            r6m.Add(new DepositRateHelper(
                q6mh[i], q6mh1[i], i < 2 ? i : 2, target, BusinessDayConvention.ModifiedFollowing, false, new Actual360(), context));
        }

        for (int i = 0; i < 18; i++)
        {
            if (i + 1 != 6 && i + 1 != 12 && i + 1 != 18)
            {
                r6m.Add(new FraRateHelper(
                    q6mh[10 + i], i + 1, i + 7, 2, target, BusinessDayConvention.ModifiedFollowing, false, new Actual360(), context));
            }
        }

        for (int i = 0; i < 15 + 35; i++)
        {
            if (i + 7 == 12 || i + 7 == 18 || i + 7 >= 24)
            {
                r6m.Add(new SwapRateHelper(
                    q6mh[10 + i], q6mh2[i], target, Frequency.Annual, BusinessDayConvention.ModifiedFollowing, new Actual360(), euribor6mEmpty, context));
            }
        }

        return PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>.Create(0, target, r6m, new Actual365Fixed(), context)
            .EnableExtrapolation();
    }

    // Swaption volatility cube as of 14.11.2012, 1y underlying vols are not
    // converted here from 3m to 6m
    internal static SwaptionVolatilityStructure Md0SwaptionVts(YieldTermStructure md0Yts, EvaluationContext context)
    {
        Period[] optionTenors =
        [
            new(1, TimeUnit.Months), new(2, TimeUnit.Months), new(3, TimeUnit.Months), new(6, TimeUnit.Months), new(9, TimeUnit.Months),
            new(1, TimeUnit.Years), new(18, TimeUnit.Months), new(2, TimeUnit.Years), new(3, TimeUnit.Years), new(4, TimeUnit.Years),
            new(5, TimeUnit.Years), new(6, TimeUnit.Years), new(7, TimeUnit.Years), new(8, TimeUnit.Years), new(9, TimeUnit.Years),
            new(10, TimeUnit.Years), new(15, TimeUnit.Years), new(20, TimeUnit.Years), new(25, TimeUnit.Years), new(30, TimeUnit.Years),
        ];

        Period[] swapTenors =
        [
            new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years), new(4, TimeUnit.Years), new(5, TimeUnit.Years),
            new(6, TimeUnit.Years), new(7, TimeUnit.Years), new(8, TimeUnit.Years), new(9, TimeUnit.Years), new(10, TimeUnit.Years),
            new(15, TimeUnit.Years), new(20, TimeUnit.Years), new(25, TimeUnit.Years), new(30, TimeUnit.Years),
        ];

        Real[] qSwAtmh =
        [
            1.81, 0.897, 0.819, 0.692, 0.551, 0.47, 0.416, 0.379, 0.357, 0.335, 0.283, 0.279, 0.283, 0.287, 1.717, 0.886,
            0.79, 0.69, 0.562, 0.481, 0.425, 0.386, 0.359, 0.339, 0.29, 0.287, 0.292, 0.296, 1.762, 0.903, 0.804, 0.693,
            0.582, 0.5, 0.448, 0.411, 0.387, 0.365, 0.31, 0.307, 0.312, 0.317, 1.662, 0.882, 0.764, 0.67, 0.586, 0.513,
            0.468, 0.432, 0.408, 0.388, 0.331, 0.325, 0.33, 0.334, 1.53, 0.854, 0.728, 0.643, 0.565, 0.503, 0.464, 0.435,
            0.415, 0.393, 0.337, 0.33, 0.333, 0.338, 1.344, 0.786, 0.683, 0.609, 0.54, 0.488, 0.453, 0.429, 0.411, 0.39,
            0.335, 0.329, 0.332, 0.336, 1.1, 0.711, 0.617, 0.548, 0.497, 0.456, 0.43, 0.408, 0.392, 0.374, 0.328, 0.323,
            0.326, 0.33, 0.956, 0.638, 0.553, 0.496, 0.459, 0.427, 0.403, 0.385, 0.371, 0.359, 0.321, 0.318, 0.323,
            0.327, 0.671, 0.505, 0.45, 0.42, 0.397, 0.375, 0.36, 0.347, 0.337, 0.329, 0.305, 0.303, 0.309, 0.313, 0.497,
            0.406, 0.378, 0.36, 0.348, 0.334, 0.323, 0.315, 0.309, 0.304, 0.289, 0.289, 0.294, 0.297, 0.404, 0.352,
            0.334, 0.322, 0.313, 0.304, 0.296, 0.291, 0.288, 0.286, 0.278, 0.277, 0.281, 0.282, 0.345, 0.312, 0.302,
            0.294, 0.286, 0.28, 0.276, 0.274, 0.273, 0.273, 0.267, 0.265, 0.268, 0.269, 0.305, 0.285, 0.277, 0.271,
            0.266, 0.262, 0.26, 0.259, 0.26, 0.262, 0.259, 0.256, 0.257, 0.256, 0.282, 0.265, 0.259, 0.254, 0.251, 0.25,
            0.25, 0.251, 0.253, 0.256, 0.253, 0.25, 0.249, 0.246, 0.263, 0.248, 0.244, 0.241, 0.24, 0.24, 0.242, 0.245,
            0.249, 0.252, 0.249, 0.245, 0.243, 0.238, 0.242, 0.234, 0.232, 0.232, 0.233, 0.235, 0.239, 0.243, 0.247,
            0.249, 0.246, 0.242, 0.237, 0.231, 0.233, 0.234, 0.241, 0.246, 0.249, 0.253, 0.257, 0.261, 0.263, 0.264,
            0.251, 0.236, 0.222, 0.214, 0.262, 0.26, 0.262, 0.263, 0.263, 0.266, 0.268, 0.269, 0.269, 0.265, 0.237,
            0.214, 0.202, 0.196, 0.26, 0.26, 0.261, 0.261, 0.258, 0.255, 0.252, 0.248, 0.245, 0.24, 0.207, 0.187, 0.182,
            0.176, 0.236, 0.223, 0.221, 0.218, 0.214, 0.21, 0.207, 0.204, 0.202, 0.2, 0.175, 0.167, 0.163, 0.158,
        ];

        var qSwAtm = new Matrix(20, 14);
        for (int i = 0; i < 20; i++)
        {
            for (int j = 0; j < 14; j++)
            {
                qSwAtm[i, j] = qSwAtmh[(i * 14) + j];
            }
        }

        SwaptionVolatilityStructure swaptionVolAtm = SwaptionVolatilityMatrix.Create(
            new TARGET(), BusinessDayConvention.ModifiedFollowing, optionTenors, swapTenors, qSwAtm, new Actual365Fixed(), context);

        Period[] optionTenorsSmile =
        [
            new(3, TimeUnit.Months), new(1, TimeUnit.Years), new(5, TimeUnit.Years), new(10, TimeUnit.Years), new(20, TimeUnit.Years),
            new(30, TimeUnit.Years),
        ];
        Period[] swapTenorsSmile =
        [
            new(2, TimeUnit.Years), new(5, TimeUnit.Years), new(10, TimeUnit.Years), new(20, TimeUnit.Years), new(30, TimeUnit.Years),
        ];
        Real[] strikeSpreads = [-0.02, -0.01, -0.0050, -0.0025, 0.0, 0.0025, 0.0050, 0.01, 0.02];

        Real[] qSwSmileh =
        [
            2.2562, 2.2562, 2.2562, 0.1851, 0.0, -0.0389, -0.0507, -0.0571, -0.06, 14.9619, 14.9619, 0.1249, 0.0328, 0.0,
            -0.0075, -0.005, 0.0078, 0.0328, 0.2296, 0.2296, 0.0717, 0.0267, 0.0, -0.0115, -0.0126, -0.0002, 0.0345,
            0.6665, 0.1607, 0.0593, 0.0245, 0.0, -0.0145, -0.0204, -0.0164, 0.0102, 0.6509, 0.1649, 0.0632, 0.027, 0.0,
            -0.018, -0.0278, -0.0303, -0.0105, 0.6303, 0.6303, 0.6303, 0.1169, 0.0, -0.0469, -0.0699, -0.091, -0.1065,
            0.4437, 0.4437, 0.0944, 0.0348, 0.0, -0.0206, -0.0327, -0.0439, -0.0472, 2.1557, 0.1501, 0.0531, 0.0225, 0.0,
            -0.0161, -0.0272, -0.0391, -0.0429, 0.4365, 0.1077, 0.0414, 0.0181, 0.0, -0.0137, -0.0237, -0.0354, -0.0401,
            0.4415, 0.1117, 0.0437, 0.0193, 0.0, -0.015, -0.0264, -0.0407, -0.0491, 0.4301, 0.0776, 0.0283, 0.0122, 0.0,
            -0.0094, -0.0165, -0.0262, -0.035, 0.2496, 0.0637, 0.0246, 0.0109, 0.0, -0.0086, -0.0153, -0.0247, -0.0334,
            0.1912, 0.0569, 0.023, 0.0104, 0.0, -0.0085, -0.0155, -0.0256, -0.0361, 0.2095, 0.06, 0.0239, 0.0107, 0.0,
            -0.0087, -0.0156, -0.0254, -0.0348, 0.2357, 0.0669, 0.0267, 0.012, 0.0, -0.0097, -0.0174, -0.0282, -0.0383,
            0.1291, 0.0397, 0.0158, 0.007, 0.0, -0.0056, -0.01, -0.0158, -0.0203, 0.1281, 0.0397, 0.0159, 0.0071, 0.0,
            -0.0057, -0.0102, -0.0164, -0.0215, 0.1547, 0.0468, 0.0189, 0.0085, 0.0, -0.0069, -0.0125, -0.0205, -0.0283,
            0.1851, 0.0522, 0.0208, 0.0093, 0.0, -0.0075, -0.0135, -0.0221, -0.0304, 0.1782, 0.0506, 0.02, 0.0089, 0.0,
            -0.0071, -0.0128, -0.0206, -0.0276, 0.2665, 0.0654, 0.0255, 0.0113, 0.0, -0.0091, -0.0163, -0.0265, -0.0367,
            0.2873, 0.0686, 0.0269, 0.0121, 0.0, -0.0098, -0.0179, -0.0298, -0.043, 0.2993, 0.0688, 0.0273, 0.0123, 0.0,
            -0.0103, -0.0189, -0.0324, -0.0494, 0.1869, 0.0501, 0.0202, 0.0091, 0.0, -0.0076, -0.014, -0.0239, -0.0358,
            0.1573, 0.0441, 0.0178, 0.008, 0.0, -0.0066, -0.0121, -0.0202, -0.0294, 0.196, 0.0525, 0.0204, 0.009, 0.0,
            -0.0071, -0.0125, -0.0197, -0.0253, 0.1795, 0.0497, 0.0197, 0.0088, 0.0, -0.0071, -0.0128, -0.0208, -0.0286,
            0.1401, 0.0415, 0.0171, 0.0078, 0.0, -0.0066, -0.0122, -0.0209, -0.0318, 0.112, 0.0344, 0.0142, 0.0065, 0.0,
            -0.0055, -0.01, -0.0171, -0.0256, 0.1077, 0.0328, 0.0134, 0.0061, 0.0, -0.005, -0.0091, -0.0152, -0.0216,
        ];

        var qSwSmile = new List<Real[]>();
        for (int i = 0; i < 30; i++)
        {
            qSwSmile.Add(qSwSmileh[(i * 9)..((i * 9) + 9)]);
        }

        Real[] qSwSmileh1 = [.. Enumerable.Range(0, 30).SelectMany(_ => new[] { 0.01, 0.2, 0.8, -0.2 })];

        bool[] parameterFixed = [false, false, false, false];

        var parameterGuess = new List<Real[]>();
        for (int i = 0; i < 30; i++)
        {
            parameterGuess.Add(qSwSmileh1[(i * 4)..((i * 4) + 4)]);
        }

        var ec = new EndCriteria(50000, 250, 1E-6, 1E-6, 1E-6);

        SwapIndex swapIndex = new EuriborSwapIsdaFixA(new Period(30, TimeUnit.Years), md0Yts);

        // We assume that we have 6m vols (which we actually don't have for 1y underlying, but this is just a test...)
        SwapIndex shortSwapIndex = new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years), md0Yts);

        // return Handle<SwaptionVolatilityStructure>(new
        // InterpolatedSwaptionVolatilityCube(swaptionVolAtm,optionTenorsSmile,swapTenorsSmile,strikeSpreads,qSwSmile,swapIndex,shortSwapIndex,false));
        // // bilinear interpolation gives nasty digitals
        return new SabrSwaptionVolatilityCube(
                swaptionVolAtm,
                optionTenorsSmile,
                swapTenorsSmile,
                strikeSpreads,
                qSwSmile,
                swapIndex,
                shortSwapIndex,
                true,
                parameterGuess,
                parameterFixed,
                true,
                context,
                ec,
                0.0050) // put a big error tolerance here ... we just want a
                        // smooth cube for testing
            .EnableExtrapolation();
    }

    // Cap volatility surface as of 14.11.2012. Par vols up to 2y are converted
    // to 6m to get a consistent caplet surface.
    internal static OptionletVolatilityStructure Md0OptionletVts(YieldTermStructure md0Yts, EvaluationContext context)
    {
        const Size nOptTen = 16;
        const Size nStrikes = 12; // leave out last strike 10% because it causes an
                                  // exception in bootstrapping

        Period[] optionTenors =
        [
            new(1, TimeUnit.Years), new(18, TimeUnit.Months), new(2, TimeUnit.Years), new(3, TimeUnit.Years), new(4, TimeUnit.Years),
            new(5, TimeUnit.Years), new(6, TimeUnit.Years), new(7, TimeUnit.Years), new(8, TimeUnit.Years), new(9, TimeUnit.Years),
            new(10, TimeUnit.Years), new(12, TimeUnit.Years), new(15, TimeUnit.Years), new(20, TimeUnit.Years), new(25, TimeUnit.Years),
            new(30, TimeUnit.Years),
        ];

        Real[] strikes = [0.0025, 0.0050, 0.0100, 0.0150, 0.0200, 0.0225, 0.0250, 0.0300, 0.0350, 0.0400, 0.0500, 0.0600];

        Real[][] volsa =
        [
            [1.3378, 1.3032, 1.2514, 1.081, 1.019, 0.961, 0.907, 0.862, 0.822, 0.788, 0.758, 0.709, 0.66, 0.619, 0.597, 0.579], // strike1
            [1.1882, 1.1057, 0.9823, 0.879, 0.828, 0.779, 0.736, 0.7, 0.67, 0.644, 0.621, 0.582, 0.544, 0.513, 0.496, 0.482], // strike2
            [1.1646, 1.0356, 0.857, 0.742, 0.682, 0.626, 0.585, 0.553, 0.527, 0.506, 0.488, 0.459, 0.43, 0.408, 0.396, 0.386], // ...
            [1.1932, 1.0364, 0.8291, 0.691, 0.618, 0.553, 0.509, 0.477, 0.452, 0.433, 0.417, 0.391, 0.367, 0.35, 0.342, 0.335],
            [1.2233, 1.0489, 0.8268, 0.666, 0.582, 0.51, 0.463, 0.43, 0.405, 0.387, 0.372, 0.348, 0.326, 0.312, 0.306, 0.301],
            [1.2369, 1.0555, 0.8283, 0.659, 0.57, 0.495, 0.447, 0.414, 0.388, 0.37, 0.355, 0.331, 0.31, 0.298, 0.293, 0.289],
            [1.2498, 1.0622, 0.8307, 0.653, 0.56, 0.483, 0.434, 0.4, 0.374, 0.356, 0.341, 0.318, 0.297, 0.286, 0.282, 0.279],
            [1.2719, 1.0747, 0.8368, 0.646, 0.546, 0.465, 0.415, 0.38, 0.353, 0.335, 0.32, 0.296, 0.277, 0.268, 0.265, 0.263],
            [1.2905, 1.0858, 0.8438, 0.643, 0.536, 0.453, 0.403, 0.367, 0.339, 0.32, 0.305, 0.281, 0.262, 0.255, 0.254, 0.252],
            [1.3063, 1.0953, 0.8508, 0.642, 0.53, 0.445, 0.395, 0.358, 0.329, 0.31, 0.294, 0.271, 0.252, 0.246, 0.246, 0.244],
            [1.332, 1.1108, 0.8631, 0.642, 0.521, 0.436, 0.386, 0.348, 0.319, 0.298, 0.282, 0.258, 0.24, 0.237, 0.237, 0.236],
            [1.3513, 1.1226, 0.8732, 0.645, 0.517, 0.43, 0.381, 0.344, 0.314, 0.293, 0.277, 0.252, 0.235, 0.233, 0.234, 0.233],
            [1.395, 1.1491, 0.9003, 0.661, 0.511, 0.425, 0.38, 0.344, 0.314, 0.292, 0.275, 0.251, 0.236, 0.236, 0.238, 0.235],
        ];

        var vols = new Matrix(nOptTen, nStrikes);
        for (Size i = 0; i < nStrikes; i++)
        {
            for (Size j = 0; j < nOptTen; j++)
            {
                vols[j, i] = volsa[i][j];
            }
        }

        IborIndex iborIndex = new Euribor(new Period(6, TimeUnit.Months), md0Yts);
        CapFloorTermVolSurface cf = CapFloorTermVolSurface.Create(
            0, new TARGET(), BusinessDayConvention.ModifiedFollowing, optionTenors, strikes, vols, context);
        OptionletStripper stripper = new OptionletStripper1(cf, iborIndex, context);

        return StrippedOptionletAdapter.Create(stripper, context);
    }

    // Calibration Basket 1: CMS10y Swaptions, 5 yearly fixings
    internal static Date[] ExpiriesCalBasket1(Date referenceDate) =>
        [.. Enumerable.Range(1, 5).Select(i => new TARGET().Advance(referenceDate, i, TimeUnit.Years))];

    internal static Period[] TenorsCalBasket1() => [.. Enumerable.Repeat(new Period(10, TimeUnit.Years), 5)];

    // Calibration Basket 2: 6m caplets, 5 years
    internal static Date[] ExpiriesCalBasket2(Date referenceDate) =>
        [.. Enumerable.Range(1, 10).Select(i => new TARGET().Advance(referenceDate, 6 * i, TimeUnit.Months))];

    // Calibration Basket 3: Coterminal Swaptions 10y
    internal static Date[] ExpiriesCalBasket3(Date referenceDate) =>
        [.. Enumerable.Range(1, 9).Select(i => new TARGET().Advance(referenceDate, i, TimeUnit.Years))];

    internal static Period[] TenorsCalBasket3() => [.. Enumerable.Range(1, 9).Select(i => new Period(10 - i, TimeUnit.Years))];

    private static Real[] ImpliedStdDevs(Real atm, Real[] strikes, Real[] prices)
    {
        var result = new Real[prices.Length];
        for (Size i = 0; i < prices.Length; i++)
        {
            result[i] = BlackFormulaImpliedStdDev(OptionType.Call, strikes[i], atm, prices[i], 1.0, 0.0, 0.2, 1E-8, 1000);
        }

        return result;
    }

    // QuantLib's checks of the digitals of two Kahale sections, from 0.001 to twice the last strike.
    private static void CheckDigitals(SmileSection first, SmileSection second, string firstName, string secondName, Real[] strikes)
    {
        const Real tol = 1E-8;
        Real k = 0.0010;
        Real dig00 = 1.0, dig10 = 1.0;
        while (k <= (2.0 * strikes[^1]) + tol)
        {
            Real dig0 = first.DigitalOptionPrice(k);
            Real dig1 = second.DigitalOptionPrice(k);
            if (!(dig0 <= dig00 + tol && dig0 >= 0.0))
            {
                Assert.Fail($"arbitrage in digitals{firstName} ({dig00},{dig0}) at strike {k}");
            }

            if (!(dig1 <= dig10 + tol && dig1 >= 0.0))
            {
                Assert.Fail($"arbitrage in digitals{secondName} ({dig10},{dig1}) at strike {k}");
            }

            dig00 = dig0;
            dig10 = dig1;
            k += 0.0001;
        }
    }

    // QuantLib's checks of a model's outputs: the zero rates within tol0, the premia within tol1.
    private static void CheckOutputs(MarkovFunctionalModelOutputs outputs, string name, Real tol0, Real tol1)
    {
        for (Size i = 0; i < outputs.Expiries.Length; i++)
        {
            if (Math.Abs(outputs.MarketZerorate[i] - outputs.ModelZerorate[i]) > tol0)
            {
                Assert.Fail($"{name} : Market zero rate ({outputs.MarketZerorate[i]}) and model zero rate ({outputs.ModelZerorate[i]}) do not agree.");
            }
        }

        for (Size i = 0; i < outputs.Expiries.Length; i++)
        {
            for (Size j = 0; j < outputs.SmileStrikes[i].Length; j++)
            {
                if (Math.Abs(outputs.MarketCallPremium[i][j] - outputs.ModelCallPremium[i][j]) > tol1)
                {
                    Assert.Fail($"{name} : Market call premium ({outputs.MarketCallPremium[i][j]}) does not match model premium ({outputs.ModelCallPremium[i][j]})");
                }

                if (Math.Abs(outputs.MarketPutPremium[i][j] - outputs.ModelPutPremium[i][j]) > tol1)
                {
                    Assert.Fail($"{name} : Market put premium ({outputs.MarketPutPremium[i][j]}) does not match model premium ({outputs.ModelPutPremium[i][j]})");
                }
            }
        }
    }

    // The four coterminal calibration helpers, priced on the model being calibrated by its Gaussian1d swaption engine.
    private static BlackCalibrationHelper[] Helpers(Real[] vols, IborIndex index, YieldTermStructure curve, EvaluationContext context)
    {
        (int O, int L)[] tenors = [(1, 4), (2, 3), (3, 2), (4, 1)];
        return [.. tenors.Select((t, i) => new SwaptionHelper(
                new Period(t.O, TimeUnit.Years),
                new Period(t.L, TimeUnit.Years),
                vols[i],
                index,
                new Period(1, TimeUnit.Years),
                new Thirty360(Thirty360Convention.BondBasis),
                new Actual360(),
                curve,
                context)
            .SetPricingEngine<MarkovFunctional>((s, c, m) => Gaussian1dSwaptionEngine.Calculate(s, c, m, 64, 7.0)))];
    }

    // The secondary instrument set: coterminal swaptions into 4Y, 3Y, 2Y and 1Y.
    private static Swaption[] Coterminals(YieldTermStructure curve, EvaluationContext context) =>
    [
        new MakeSwaption(new EuriborSwapIsdaFixA(new Period(4, TimeUnit.Years), curve), new Period(1, TimeUnit.Years)).ToSwaption(context),
        new MakeSwaption(new EuriborSwapIsdaFixA(new Period(3, TimeUnit.Years), curve), new Period(2, TimeUnit.Years)).ToSwaption(context),
        new MakeSwaption(new EuriborSwapIsdaFixA(new Period(2, TimeUnit.Years), curve), new Period(3, TimeUnit.Years)).ToSwaption(context),
        new MakeSwaption(new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years), curve), new Period(4, TimeUnit.Years)).ToSwaption(context),
    ];

    // QuantLib's report of the secondary set: Black's price against the model's, in units of Black's vega.
    private static void CheckSecondarySet(Swaption[] swaptions, Real[] vols, YieldTermStructure curve, MarkovFunctional model, EvaluationContext context, string name, Real tol1)
    {
        for (Size i = 0; i < swaptions.Length; i++)
        {
            InstrumentResults black = BlackSwaptionEngine.Calculate(swaptions[i], context, curve, vols[i]);
            Real blackPrice = black.NPV!.Value;
            Real blackVega = black.Result<Real>("vega");
            Real mfPrice = Gaussian1dSwaptionEngine.Calculate(swaptions[i], context, model, 64, 7.0).NPV!.Value;
            if (Math.Abs(blackPrice - mfPrice) / blackVega > tol1)
            {
                TestContext.Current.TestOutputHelper?.WriteLine(
                    $"{name}: Secondary instrument set calibration failed for instrument #{i} black premium is {blackPrice} while model premium is {mfPrice} (market vega is {blackVega})");
            }
        }
    }
}
