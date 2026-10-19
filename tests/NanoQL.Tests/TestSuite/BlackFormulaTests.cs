// SPDX-FileCopyrightText: Copyright (C) 2013 Gary Kennedy
// SPDX-FileCopyrightText: Copyright (C) 2015, 2024 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2020 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/blackformula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class BlackFormulaTests
{
    [Fact]
    public void TestBachelierImpliedVol()
    {
        // Testing Bachelier implied vol...

        Real forward = 1.0;
        Real bpvol = 0.01;
        Real tte = 10.0;
        Real stdDev = bpvol * Math.Sqrt(tte);
        OptionType optionType = OptionType.Call;
        Real discount = 0.95;

        Real[] d = [-3.0, -2.0, -1.0, -0.5, 0.0, 0.5, 1.0, 2.0, 3.0];
        foreach (Real i in d)
        {
            Real strike = forward - (i * bpvol * Math.Sqrt(tte));

            Real callPrem = BachelierBlackFormula(optionType, strike, forward, stdDev, discount);

            Real impliedBpVol = BachelierBlackFormulaImpliedVolChoi(optionType, strike, forward, tte, callPrem, discount);

            if (Math.Abs(bpvol - impliedBpVol) > 1.0e-12)
            {
                Assert.Fail($"Failed, expected {bpvol} realised {impliedBpVol}");
            }

            Real impliedBpVolExact = BachelierBlackFormulaImpliedVol(optionType, strike, forward, tte, callPrem, discount);

            if (Math.Abs(bpvol - impliedBpVolExact) > 1.0e-15)
            {
                Assert.Fail($"Failed, expected {bpvol} realised {impliedBpVolExact}");
            }
        }
    }

    [Fact]
    public void TestChambersImpliedVol()
    {
        // Testing Chambers-Nawalkha implied vol approximation...

        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] displacements = [0.0000, 0.0010, 0.0050, 0.0100, 0.0200];
        Real[] forwards = [-0.0010, 0.0000, 0.0050, 0.0100, 0.0200, 0.0500];
        Real[] strikes = [-0.0100, -0.0050, -0.0010, 0.0000, 0.0010, 0.0050, 0.0100, 0.0200, 0.0500, 0.1000];
        Real[] stdDevs = [0.10, 0.15, 0.20, 0.30, 0.50, 0.60, 0.70, 0.80, 1.00, 1.50, 2.00];
        Real[] discounts = [1.00, 0.95, 0.80, 1.10];

        Real tol = 5.0E-4;

        foreach (OptionType type in types)
        {
            foreach (Real displacement in displacements)
            {
                foreach (Real forward in forwards)
                {
                    foreach (Real strike in strikes)
                    {
                        foreach (Real stdDev in stdDevs)
                        {
                            foreach (Real discount in discounts)
                            {
                                if (forward + displacement > 0.0 && strike + displacement > 0.0)
                                {
                                    Real premium = BlackFormula(type, strike, forward, stdDev, discount, displacement);
                                    Real atmPremium = BlackFormula(type, forward, forward, stdDev, discount, displacement);
                                    Real iStdDev = BlackFormulaImpliedStdDevChambers(
                                        type, strike, forward, premium, atmPremium, discount, displacement);
                                    Real moneyness = (strike + displacement) / (forward + displacement);
                                    if (moneyness > 1.0)
                                    {
                                        moneyness = 1.0 / moneyness;
                                    }

                                    Real error = (iStdDev - stdDev) / stdDev * moneyness;
                                    if (error > tol)
                                    {
                                        Assert.Fail(
                                            $"Failed to verify Chambers-Nawalkha approximation for {type} displacement="
                                            + $"{displacement} forward={forward} strike={strike} discount={discount} "
                                            + $"stddev={stdDev} result={iStdDev} exceeds maximum error tolerance");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestRadoicicStefanicaImpliedVol()
    {
        // Testing Radoicic-Stefanica implied vol approximation...

        const double T = 1.7;
        const Rate r = 0.1;
        DiscountFactor df = Math.Exp(-r * T);

        const Real forward = 100;

        const Volatility vol = 0.3;
        Real stdDev = vol * Math.Sqrt(T);

        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [50, 60, 70, 80, 90, 100, 110, 125, 150, 200, 300];

        const Real tol = 0.02;

        foreach (Real strike in strikes)
        {
            foreach (OptionType type in types)
            {
                var payoff = new PlainVanillaPayoff(type, strike);

                Real marketValue = BlackFormula(payoff, forward, stdDev, df);

                Real estVol = BlackFormulaImpliedStdDevApproximationRS(payoff, forward, marketValue, df) / Math.Sqrt(T);

                Real error = Math.Abs(estVol - vol);
                if (error > tol)
                {
                    Assert.Fail(
                        $"Failed to verify Radoicic-Stefanicaapproximation for {type}\n forward     :{forward}\n strike      :"
                        + $"{strike}\n discount    :{df}\n implied vol :{vol}\n result      :{estVol}\n error       :{error}\n"
                        + $" tolerance   :{tol}");
                }
            }
        }
    }

    [Fact]
    public void TestRadoicicStefanicaLowerBound()
    {
        // Testing Radoicic-Stefanica lower bound...

        // testing lower bound plot figure 3.1 from
        // "Tighter Bounds for Implied Volatility",
        // J. Gatheral, I. Matic, R. Radoicic, D. Stefanica
        // https://papers.ssrn.com/sol3/papers.cfm?abstract_id=2922742

        const Real forward = 1.0;
        const Real k = 1.2;

        for (Real s = 0.17; s < 2.9; s += 0.01)
        {
            Real strike = Math.Exp(k) * forward;
            Real c = BlackFormula(OptionType.Call, strike, forward, s);
            Real estimate = BlackFormulaImpliedStdDevApproximationRS(OptionType.Call, strike, forward, c);

            Real error = s - estimate;
            if (double.IsNaN(estimate) || Math.Abs(error) > 0.05)
            {
                Assert.Fail(
                    $"Failed to lower bound Radoicic-Stefanicaapproximation for \n forward     :{forward}\n strike      :{k}\n"
                    + $" stdDev      :{s}\n result      :{estimate}\n error       :{error}");
            }

            if (c > 1e-6 && error < 0.0)
            {
                Assert.Fail(
                    $"Failed to verify Radoicic-Stefanica is lower bound\n forward     :{forward}\n strike      :{k}\n"
                    + $" stdDev      :{s}\n result      :{estimate}\n error       :{error}");
            }
        }
    }

    [Fact]
    public void TestImpliedVolAdaptiveSuccessiveOverRelaxation()
    {
        // Testing implied volatility calculation via adaptive successive over-relaxation...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(12, Month.July, 2017);
        var context = new EvaluationContext { EvaluationDate = today };

        Date exerciseDate = today + new Period(15, TimeUnit.Months);
        double exerciseTime = dc.YearFraction(today, exerciseDate);

        YieldTermStructure rTS = FlatForward.Create(0, new NullCalendar(), 0.10, dc, context);
        YieldTermStructure qTS = FlatForward.Create(0, new NullCalendar(), 0.06, dc, context);

        DiscountFactor df = rTS.Discount(exerciseDate);

        const Volatility vol = 0.20;
        Real stdDev = vol * Math.Sqrt(exerciseTime);

        const Real s0 = 100;
        Real forward = s0 * qTS.Discount(exerciseDate) / df;

        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [50, 60, 70, 80, 90, 100, 110, 125, 150, 200];
        Real[] displacements = [0, 25, 50, 100];

        const Real tol = 1e-8;

        foreach (Real strike in strikes)
        {
            foreach (OptionType type in types)
            {
                var payoff = new PlainVanillaPayoff(type, strike);

                foreach (Real displacement in displacements)
                {
                    Real marketValue = BlackFormula(payoff, forward, stdDev, df, displacement);

                    Real impliedStdDev = BlackFormulaImpliedStdDevLiRS(
                        payoff, forward, marketValue, df, displacement, null, 1.0, tol, 100);

                    Real error = Math.Abs(impliedStdDev - stdDev);
                    if (error > 10 * tol)
                    {
                        Assert.Fail(
                            "Failed to calculated implied volatility with adaptive successive over-relaxation"
                            + $"\n forward     :{forward}\n strike      :{strike}\n stdDev      :{stdDev}\n displacement:"
                            + $"{displacement}\n result      :{impliedStdDev}\n error       :{error}\n tolerance   :{tol}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestBlackFormulaForwardDerivative()
    {
        // Testing forward derivative of the Black formula...

        Real[] strikes = [0.1, 0.5, 1.0, 2.0, 3.0];
        const Real vol = 0.1;
        AssertBlackFormulaForwardDerivative(OptionType.Call, strikes, vol);
        AssertBlackFormulaForwardDerivative(OptionType.Put, strikes, vol);
    }

    [Fact]
    public void TestBlackFormulaForwardDerivativeWithZeroStrike()
    {
        // Testing forward derivative of the Black formula with zero strike...

        Real[] strikes = [0.0];
        const Real vol = 0.1;
        AssertBlackFormulaForwardDerivative(OptionType.Call, strikes, vol);
        AssertBlackFormulaForwardDerivative(OptionType.Put, strikes, vol);
    }

    [Fact]
    public void TestBlackFormulaForwardDerivativeWithZeroVolatility()
    {
        // Testing forward derivative of the Black formula with zero volatility...

        Real[] strikes = [0.1, 0.5, 1.0, 2.0, 3.0];
        const Real vol = 0.0;
        AssertBlackFormulaForwardDerivative(OptionType.Call, strikes, vol);
        AssertBlackFormulaForwardDerivative(OptionType.Put, strikes, vol);
    }

    [Fact]
    public void TestBachelierBlackFormulaForwardDerivative()
    {
        // Testing forward derivative of the Bachelier Black formula...

        Real[] strikes = [-3.0, -2.0, -1.0, -0.5, 0.0, 0.5, 1.0, 2.0, 3.0];
        const Real vol = 0.001;
        AssertBachelierBlackFormulaForwardDerivative(OptionType.Call, strikes, vol);
        AssertBachelierBlackFormulaForwardDerivative(OptionType.Put, strikes, vol);
    }

    [Fact]
    public void TestBachelierBlackFormulaForwardDerivativeWithZeroVolatility()
    {
        // Testing forward derivative of the Bachelier Black formula with zero volatility...

        Real[] strikes = [-3.0, -2.0, -1.0, -0.5, 0.0, 0.5, 1.0, 2.0, 3.0];
        const Real vol = 0.0;
        AssertBachelierBlackFormulaForwardDerivative(OptionType.Call, strikes, vol);
        AssertBachelierBlackFormulaForwardDerivative(OptionType.Put, strikes, vol);
    }

    [Fact]
    public void TestItmProbabilitiesWithZeroVolatility()
    {
        // Testing in-the-money probabilities with zero volatility...

        // With zero standard deviation the probabilities must be the limit of the
        // formulas as the standard deviation goes to zero, i.e. 1 if the option is
        // in the money and 0 if it is out of the money.
        (OptionType Type, Real Strike, Real Forward)[] cases =
        [
            (OptionType.Call, 0.03, 0.05), (OptionType.Call, 0.05, 0.03),
            (OptionType.Put, 0.03, 0.05), (OptionType.Put, 0.05, 0.03),
            (OptionType.Call, 90.0, 100.0), (OptionType.Put, 90.0, 100.0),
        ];

        const Real tinyStdDev = 1.0e-8;
        foreach ((OptionType type, Real strike, Real forward) in cases)
        {
            Real theta = type == OptionType.Call ? 1.0 : -1.0;
            Real expected = theta * (forward - strike) > 0.0 ? 1.0 : 0.0;

            Real[] results =
            [
                BlackFormulaCashItmProbability(type, strike, forward, 0.0),
                BlackFormulaAssetItmProbability(type, strike, forward, 0.0),
                BachelierBlackFormulaAssetItmProbability(type, strike, forward, 0.0),
            ];
            Real[] limits =
            [
                BlackFormulaCashItmProbability(type, strike, forward, tinyStdDev),
                BlackFormulaAssetItmProbability(type, strike, forward, tinyStdDev),
                BachelierBlackFormulaAssetItmProbability(type, strike, forward, tinyStdDev),
            ];
            string[] names = ["Black cash", "Black asset", "Bachelier asset"];

            for (Size i = 0; i < 3; ++i)
            {
                if (Math.Abs(limits[i] - expected) > 1.0e-12)
                {
                    Assert.Fail($"unexpected {names[i]} probability with tiny std dev");
                }

                if (Math.Abs(results[i] - expected) > 1.0e-12)
                {
                    Assert.Fail(
                        $"{names[i]} in-the-money probability with zero std dev\n option type: {type}\n strike:      "
                        + $"{strike}\n forward:     {forward}\n calculated:  {results[i]}\n expected:    {expected}");
                }
            }
        }
    }

    [Fact]
    public void TestBachelierImpliedVolAtIntrinsic()
    {
        // Testing Bachelier implied volatility for prices at intrinsic value...

        // For a deep in-the-money option the Bachelier price rounds to the
        // intrinsic value, so the time value computed by the inversion can come
        // out a few ulps below zero.  Such a price must still invert instead of
        // being rejected as arbitrageable.
        const Real tte = 0.01;
        const Real vol = 1e-5;

        foreach (Real forward in new[] { -0.01, 0.005, 0.05, 1.0 })
        {
            foreach (Real offset in new[] { 0.01, 0.03, 0.07 })
            {
                foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
                {
                    Real strike = type == OptionType.Call ? forward - offset : forward + offset;
                    Real price = BachelierBlackFormula(type, strike, forward, vol * Math.Sqrt(tte));

                    Real impliedVol = -1.0;
                    Exception? error = Record.Exception(() =>
                        impliedVol = BachelierBlackFormulaImpliedVol(type, strike, forward, tte, price));
                    Assert.Null(error);
                    Assert.True(impliedVol >= 0.0);
                }
            }
        }
    }

    [Fact]
    public void TestDeepInTheMoneyPricesNotBelowIntrinsic()
    {
        // Testing that deep in-the-money Black and Bachelier prices are not below their intrinsic value...

        // With a small enough standard deviation the cumulative normal
        // saturates and the price has to reduce to the discounted intrinsic
        // value exactly; a result a few ulps below it is rejected by the
        // no-arbitrage checks of the implied-volatility inversions.
        (OptionType Type, Real Strike, Real Forward)[] cases =
        [
            (OptionType.Call, 50.0, 100.0), (OptionType.Put, 100.0, 50.0),
            (OptionType.Call, 0.03, 0.05), (OptionType.Put, 0.05, 0.03),
            (OptionType.Call, 1000.0, 1234.5), (OptionType.Put, 1234.5, 1000.0),
        ];

        foreach ((OptionType type, Real strike, Real forward) in cases)
        {
            Real theta = type == OptionType.Call ? 1.0 : -1.0;
            Real moneyness = theta * (forward - strike);
            foreach (Real discount in new[] { 1.0, 0.97 })
            {
                Real intrinsic = discount * Math.Max(moneyness, 0.0);

                // both d1 and d2 are above 20
                Real black = BlackFormula(type, strike, forward, 0.01, discount);
                if (black < intrinsic)
                {
                    Assert.Fail(
                        $"Black price for strike {strike}, forward {forward}, discount {discount} is below intrinsic by "
                        + $"{intrinsic - black}");
                }

                // (forward - strike)/stdDev is 20
                Real bachelier = BachelierBlackFormula(type, strike, forward, moneyness / 20.0, discount);
                if (bachelier < intrinsic)
                {
                    Assert.Fail(
                        $"Bachelier price for strike {strike}, forward {forward}, discount {discount} is below intrinsic "
                        + $"by {intrinsic - bachelier}");
                }
            }
        }
    }

    [Fact]
    public void TestImpliedStdDevApproximationRSStaysFinite()
    {
        // Testing RS implied-standard-deviation approximation away from the money...

        const Real forward = 1.0;
        const Real discount = 1.0;

        foreach (Real ratio in new[] { 1.01, 1.1, 1.5, 2.0, 3.0, 5.0, 10.0 })
        {
            Real strike = forward / ratio;
            foreach (Real stdDev in new[] { 0.01, 0.05, 0.1, 0.2, 0.4, 0.8, 1.5 })
            {
                foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
                {
                    Real price = BlackFormula(type, strike, forward, stdDev, discount);
                    Real approx = BlackFormulaImpliedStdDevApproximationRS(type, strike, forward, price, discount);

                    if (!double.IsFinite(approx))
                    {
                        Assert.Fail(
                            $"RS approximation is not finite\n    forward:    {forward:R}\n    strike:     {strike:R}\n"
                            + $"    std dev:    {stdDev:R}\n    price:      {price:R}\n    calculated: {approx:R}");
                    }

                    if (approx < 0.0)
                    {
                        Assert.Fail(
                            $"RS approximation is negative\n    forward:    {forward:R}\n    strike:     {strike:R}\n"
                            + $"    std dev:    {stdDev:R}\n    calculated: {approx:R}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestImpliedStdDevLiRSAtIntrinsic()
    {
        // Testing Li-RS implied standard deviation at intrinsic...

        const Real forward = 1.0;
        const Real discount = 1.0;

        foreach (Real ratio in new[] { 1.5, 2.0, 5.0, 10.0 })
        {
            Real strike = forward / ratio;
            foreach (Real stdDev in new[] { 1e-7, 1e-4, 0.01 })
            {
                Real price = BlackFormula(OptionType.Call, strike, forward, stdDev, discount);
                Real expected = BlackFormulaImpliedStdDev(OptionType.Call, strike, forward, price, discount);
                if (expected != 0.0)
                {
                    continue;
                }

                Real calculated;
                try
                {
                    calculated = BlackFormulaImpliedStdDevLiRS(OptionType.Call, strike, forward, price, discount);
                }
                catch (Exception e) when (e is ArgumentException or NanoQLException)
                {
                    Assert.Fail(
                        $"Li-RS threw where the price sits on intrinsic\n    forward: {forward:R}\n    strike:  {strike:R}\n"
                        + $"    std dev: {stdDev:R}\n    price:   {price:R}\n    error:   {e.Message}");
                    continue;
                }

                if (calculated != expected)
                {
                    Assert.Fail(
                        $"failed to reproduce Li-RS implied standard deviation\n    forward:    {forward:R}\n"
                        + $"    strike:     {strike:R}\n    std dev:    {stdDev:R}\n    calculated: {calculated:R}\n"
                        + $"    expected:   {expected:R}");
                }
            }
        }
    }

    private static void AssertBlackFormulaForwardDerivative(OptionType optionType, Real[] strikes, Real bpvol)
    {
        Real forward = 1.0;
        Real tte = 10.0;
        Real stdDev = bpvol * Math.Sqrt(tte);
        Real discount = 0.95;
        Real displacement = 0.01;
        Real bump = 0.0001;
        Real epsilon = 1.0e-10;
        string type = optionType == OptionType.Call ? "Call" : "Put";

        foreach (Real strike in strikes)
        {
            Real delta = BlackFormulaForwardDerivative(optionType, strike, forward, stdDev, discount, displacement);
            Real bumpedDelta = BlackFormulaForwardDerivative(
                optionType, strike, forward + bump, stdDev, discount, displacement);

            Real basePremium = BlackFormula(optionType, strike, forward, stdDev, discount, displacement);
            Real bumpedPremium = BlackFormula(optionType, strike, forward + bump, stdDev, discount, displacement);
            Real deltaApprox = (bumpedPremium - basePremium) / bump;

            // Based on the Mean Value Theorem, the below inequality
            // should hold for any function that is monotonic in the
            // area of the bump.
            bool success =
                (StdMax(delta, bumpedDelta) + epsilon > deltaApprox)
                && (deltaApprox > StdMin(delta, bumpedDelta) - epsilon);

            if (!success)
            {
                Assert.Fail(
                    $"Failed to calculate the derivative of the Black formula w.r.t. forward\n option type       :{type}\n"
                    + $" forward           :{forward}\n strike            :{strike}\n stdDev            :{stdDev}\n"
                    + $" displacement      :{displacement}\n analytical delta  :{delta}\n approximated delta:{deltaApprox}");
            }
        }
    }

    private static void AssertBachelierBlackFormulaForwardDerivative(OptionType optionType, Real[] strikes, Real bpvol)
    {
        Real forward = 1.0;
        Real tte = 10.0;
        Real stdDev = bpvol * Math.Sqrt(tte);
        Real discount = 0.95;
        Real bump = 0.0001;
        Real epsilon = 1.0e-10;
        string type = optionType == OptionType.Call ? "Call" : "Put";

        foreach (Real strike in strikes)
        {
            Real delta = BachelierBlackFormulaForwardDerivative(optionType, strike, forward, stdDev, discount);
            Real bumpedDelta = BachelierBlackFormulaForwardDerivative(optionType, strike, forward + bump, stdDev, discount);

            Real basePremium = BachelierBlackFormula(optionType, strike, forward, stdDev, discount);
            Real bumpedPremium = BachelierBlackFormula(optionType, strike, forward + bump, stdDev, discount);
            Real deltaApprox = (bumpedPremium - basePremium) / bump;

            // Based on the Mean Value Theorem, the below inequality
            // should hold for any function that is monotonic in the
            // area of the bump.
            bool success =
                (StdMax(delta, bumpedDelta) + epsilon > deltaApprox)
                && (deltaApprox > StdMin(delta, bumpedDelta) - epsilon);

            if (!success)
            {
                Assert.Fail(
                    "Failed to calculate the derivative of the Bachelier Black formula w.r.t. forward\n option type       :"
                    + $"{type}\n forward           :{forward}\n strike            :{strike}\n stdDev            :{stdDev}\n"
                    + $" analytical delta  :{delta}\n approximated delta:{deltaApprox}");
            }
        }
    }

    // std::max's and std::min's semantics.
    private static Real StdMax(Real a, Real b) => a < b ? b : a;

    private static Real StdMin(Real a, Real b) => b < a ? b : a;
}
