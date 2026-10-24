// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/digitalcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

// Adapted throughout: CommonVars' evaluation date is the test runner's, 16 September 2015; its relinkable handle,
// linked to the curve after the index is built on it, is the curve itself, built first; the evaluation date it sets is
// the context the coupons, the pricers and the engines read; each pricer, built with QuantLib's defaults, reads the
// context's at-par switch; setPricer on a digital coupon returns a copy holding the pricer, whose underlying holds it
// too, so the underlying's rate is read from the copy, where QuantLib's shared underlying is given the pricer in place;
// a coupon's price takes the context; and each vanilla option's engine is the static AnalyticEuropeanEngine.Calculate.
[Trait("Oracle", "QuantLib")]
public class DigitalCouponTests
{
    [Fact]
    public void TestAssetOrNothing()
    {
        // Testing European asset-or-nothing digital coupon...

        /*  Call Payoff = (aL+b)Heaviside(aL+b-X) =  a Max[L-X'] + (b+aX')Heaviside(L-X')
            Value Call = aF N(d1') + bN(d2')
            Put Payoff =  (aL+b)Heaviside(X-aL-b) = -a Max[X-L'] + (b+aX')Heaviside(X'-L)
            Value Put = aF N(-d1') + bN(-d2')
            where:
            d1' = ln(F/X')/stdDev + 0.5*stdDev;
        */

        var vars = new CommonVars();

        double[] vols = [0.05, 0.15, 0.30];
        double[] strikes = [0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07];
        double[] gearings = [1.0, 2.8];
        double[] spreads = [0.0, 0.005];

        double gap = 1e-7; /* low, in order to compare digital option value
                            with black formula result */
        var replication = new DigitalReplication(ReplicationType.Central, gap);
        foreach (double capletVol in vols)
        {
            OptionletVolatilityStructure vol = new ConstantOptionletVolatility(
                vars.Today, vars.Calendar, BusinessDayConvention.Following, capletVol, new Actual360());
            foreach (double strike in strikes)
            {
                for (int k = 9; k < 10; k++)
                {
                    Date startDate = vars.Calendar.Advance(vars.Settlement, (k + 1) * TimeUnit.Years);
                    Date endDate = vars.Calendar.Advance(vars.Settlement, (k + 2) * TimeUnit.Years);
                    double? nullstrike = null;
                    Date paymentDate = endDate;
                    for (int h = 0; h < gearings.Length; h++)
                    {
                        double gearing = gearings[h];
                        double spread = spreads[h];

                        var underlying = new IborCoupon(
                            paymentDate, vars.Nominal, startDate, endDate, vars.FixingDays, vars.Index, gearing, spread);

                        // Floating Rate Coupon - Call Digital option
                        var pricer = new BlackIborCouponPricer(vars.Context, vol);
                        DigitalCoupon digitalCappedCoupon = new DigitalCoupon(
                            underlying, strike, PositionType.Short, false, nullstrike,
                            nullstrike, PositionType.Short, false, nullstrike, replication).SetPricer(pricer);

                        // Check digital option price vs N(d1) price
                        FloatingRateCoupon priced = digitalCappedCoupon.Underlying;
                        double accrualPeriod = underlying.AccrualPeriod();
                        double discount = vars.TermStructure.Discount(endDate);
                        Date exerciseDate = underlying.FixingDate();
                        double forward = priced.Rate(vars.Context);
                        double effFwd = (forward - spread) / gearing;
                        double effStrike = (strike - spread) / gearing;
                        double stdDev = Math.Sqrt(vol.BlackVariance(exerciseDate, effStrike));
                        var phi = new CumulativeNormalDistribution();
                        double d1 = (Math.Log(effFwd / effStrike) / stdDev) + (0.5 * stdDev);
                        double d2 = d1 - stdDev;
                        double nD1 = phi.Evaluate(d1);
                        double nD2 = phi.Evaluate(d2);
                        double nd1Price = ((gearing * effFwd * nD1) + (spread * nD2)) * vars.Nominal * accrualPeriod * discount;
                        double optionPrice = digitalCappedCoupon.CallOptionRate(vars.Context) * vars.Nominal * accrualPeriod * discount;
                        double error = Math.Abs(nd1Price - optionPrice);
                        if (error > vars.OptionTolerance)
                        {
                            Assert.Fail($"\nDigital Call Option:\nVolatility = {capletVol}\nStrike = {strike}\nExercise = {k + 1} years"
                                        + $"\nOption price by replication = {optionPrice}\nOption price by Cox-Rubinstein formula = {nd1Price}"
                                        + $"\nError {error}");
                        }

                        // Check digital option price vs N(d1) price using Vanilla Option class
                        if (spread == 0.0)
                        {
                            double callVO = vars.Nominal * gearing * accrualPeriod
                                * VanillaNpv(vars, new AssetOrNothingPayoff(OptionType.Call, effStrike), exerciseDate, effFwd, capletVol)
                                * discount / vars.TermStructure.Discount(exerciseDate) * forward / effFwd;
                            error = Math.Abs(nd1Price - callVO);
                            if (error > vars.BlackTolerance)
                            {
                                Assert.Fail($"\nDigital Call Option:\nVolatility = {capletVol}\nStrike = {strike}\nExercise = {k + 1} years"
                                            + $"\nOption price by Black asset-ot-nothing payoff = {callVO}\nOption price by Cox-Rubinstein = {nd1Price}"
                                            + $"\nError {error}");
                            }
                        }

                        // Floating Rate Coupon + Put Digital option
                        DigitalCoupon digitalFlooredCoupon = new DigitalCoupon(
                            underlying, nullstrike, PositionType.Long, false, nullstrike,
                            strike, PositionType.Long, false, nullstrike, replication).SetPricer(pricer);

                        // Check digital option price vs N(d1) price
                        nD1 = phi.Evaluate(-d1);
                        nD2 = phi.Evaluate(-d2);
                        nd1Price = ((gearing * effFwd * nD1) + (spread * nD2)) * vars.Nominal * accrualPeriod * discount;
                        optionPrice = digitalFlooredCoupon.PutOptionRate(vars.Context) * vars.Nominal * accrualPeriod * discount;
                        error = Math.Abs(nd1Price - optionPrice);
                        if (error > vars.OptionTolerance)
                        {
                            Assert.Fail($"\nDigital Put Option:\nVolatility = {capletVol}\nStrike = {strike}\nExercise = {k + 1} years"
                                        + $"\nOption price by replication = {optionPrice}\nOption price by Cox-Rubinstein = {nd1Price}"
                                        + $"\nError {error}");
                        }

                        // Check digital option price vs N(d1) price using Vanilla Option class
                        if (spread == 0.0)
                        {
                            double putVO = vars.Nominal * gearing * accrualPeriod
                                * VanillaNpv(vars, new AssetOrNothingPayoff(OptionType.Put, effStrike), exerciseDate, effFwd, capletVol)
                                * discount / vars.TermStructure.Discount(exerciseDate) * forward / effFwd;
                            error = Math.Abs(nd1Price - putVO);
                            if (error > vars.BlackTolerance)
                            {
                                Assert.Fail($"\nDigital Put Option:\nVolatility = {capletVol}\nStrike = {strike}\nExercise = {k + 1} years"
                                            + $"\nOption price by Black asset-ot-nothing payoff = {putVO}\nOption price by Cox-Rubinstein = {nd1Price}"
                                            + $"\nError {error}");
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestAssetOrNothingDeepInTheMoney()
    {
        // Testing European deep in-the-money asset-or-nothing digital coupon...

        var vars = new CommonVars();

        double gearing = 1.0;
        double spread = 0.0;

        double capletVolatility = 0.0001;
        OptionletVolatilityStructure volatility = new ConstantOptionletVolatility(
            vars.Today, vars.Calendar, BusinessDayConvention.Following, capletVolatility, new Actual360());

        for (int k = 0; k < 10; k++)
        {
            // Loop on start and end dates
            Date startDate = vars.Calendar.Advance(vars.Settlement, (k + 1) * TimeUnit.Years);
            Date endDate = vars.Calendar.Advance(vars.Settlement, (k + 2) * TimeUnit.Years);
            double? nullstrike = null;
            Date paymentDate = endDate;

            var pricer = new BlackIborCouponPricer(vars.Context, volatility);
            IborCoupon underlying = new IborCoupon(
                paymentDate, vars.Nominal, startDate, endDate, vars.FixingDays, vars.Index, gearing, spread).SetPricer(pricer);

            // Floating Rate Coupon - Deep-in-the-money Call Digital option
            double strike = 0.001;
            DigitalCoupon digitalCappedCoupon = new DigitalCoupon(
                underlying, strike, PositionType.Short, false, nullstrike,
                nullstrike, PositionType.Short, false, nullstrike).SetPricer(pricer);

            // Check price vs its target price
            double accrualPeriod = underlying.AccrualPeriod();
            double discount = vars.TermStructure.Discount(endDate);

            double targetOptionPrice = underlying.Price(vars.TermStructure, vars.Context);
            double targetPrice = 0.0;
            double digitalPrice = digitalCappedCoupon.Price(vars.TermStructure, vars.Context);
            double error = Math.Abs(targetPrice - digitalPrice);
            double tolerance = 1e-08;
            Check(error, tolerance, $"\nFloating Coupon - Digital Call Option:\nStrike = {strike}\nExercise = {k + 1} years\nCoupon Price = {digitalPrice}\nTarget price = {targetPrice}");

            // Check digital option price
            double replicationOptionPrice = digitalCappedCoupon.CallOptionRate(vars.Context) * vars.Nominal * accrualPeriod * discount;
            error = Math.Abs(targetOptionPrice - replicationOptionPrice);
            double optionTolerance = 1e-08;
            Check(error, optionTolerance, $"\nDigital Call Option:\nStrike = {strike}\nExercise = {k + 1} years\nPrice by replication = {replicationOptionPrice}\nTarget price = {targetOptionPrice}");

            // Floating Rate Coupon + Deep-in-the-money Put Digital option
            strike = 0.99;
            DigitalCoupon digitalFlooredCoupon = new DigitalCoupon(
                underlying, nullstrike, PositionType.Long, false, nullstrike,
                strike, PositionType.Long, false, nullstrike).SetPricer(pricer);

            // Check price vs its target price
            targetOptionPrice = underlying.Price(vars.TermStructure, vars.Context);
            targetPrice = underlying.Price(vars.TermStructure, vars.Context) + targetOptionPrice;
            digitalPrice = digitalFlooredCoupon.Price(vars.TermStructure, vars.Context);
            error = Math.Abs(targetPrice - digitalPrice);
            tolerance = 2.5e-06;
            Check(error, tolerance, $"\nFloating Coupon + Digital Put Option:\nStrike = {strike}\nExercise = {k + 1} years\nDigital coupon price = {digitalPrice}\nTarget price = {targetPrice}");

            // Check digital option
            replicationOptionPrice = digitalFlooredCoupon.PutOptionRate(vars.Context) * vars.Nominal * accrualPeriod * discount;
            error = Math.Abs(targetOptionPrice - replicationOptionPrice);
            optionTolerance = 2.5e-06;
            Check(error, optionTolerance, $"\nDigital Put Option:\nStrike = {strike}\nExercise = {k + 1} years\nPrice by replication = {replicationOptionPrice}\nTarget price = {targetOptionPrice}");
        }
    }

    [Fact]
    public void TestAssetOrNothingDeepOutTheMoney()
    {
        // Testing European deep out-the-money asset-or-nothing digital coupon...

        var vars = new CommonVars();

        double gearing = 1.0;
        double spread = 0.0;

        double capletVolatility = 0.0001;
        OptionletVolatilityStructure volatility = new ConstantOptionletVolatility(
            vars.Today, vars.Calendar, BusinessDayConvention.Following, capletVolatility, new Actual360());

        for (int k = 0; k < 10; k++)
        {
            // loop on start and end dates
            Date startDate = vars.Calendar.Advance(vars.Settlement, (k + 1) * TimeUnit.Years);
            Date endDate = vars.Calendar.Advance(vars.Settlement, (k + 2) * TimeUnit.Years);
            double? nullstrike = null;
            Date paymentDate = endDate;

            var pricer = new BlackIborCouponPricer(vars.Context, volatility);
            IborCoupon underlying = new IborCoupon(
                paymentDate, vars.Nominal, startDate, endDate, vars.FixingDays, vars.Index, gearing, spread).SetPricer(pricer);

            // Floating Rate Coupon - Deep-out-of-the-money Call Digital option
            double strike = 0.99;
            DigitalCoupon digitalCappedCoupon = new DigitalCoupon(
                underlying, strike, PositionType.Short, false, nullstrike,
                nullstrike, PositionType.Long, false, nullstrike).SetPricer(pricer);

            // Check price vs its target
            double accrualPeriod = underlying.AccrualPeriod();
            double discount = vars.TermStructure.Discount(endDate);

            double targetPrice = underlying.Price(vars.TermStructure, vars.Context);
            double digitalPrice = digitalCappedCoupon.Price(vars.TermStructure, vars.Context);
            double error = Math.Abs(targetPrice - digitalPrice);
            double tolerance = 1e-10;
            Check(error, tolerance, $"\nFloating Coupon - Digital Call Option :\nStrike = {strike}\nExercise = {k + 1} years\nCoupon price = {digitalPrice}\nTarget price = {targetPrice}");

            // Check digital option price
            double targetOptionPrice = 0.0;
            double replicationOptionPrice = digitalCappedCoupon.CallOptionRate(vars.Context) * vars.Nominal * accrualPeriod * discount;
            error = Math.Abs(targetOptionPrice - replicationOptionPrice);
            double optionTolerance = 1e-08;
            Check(error, optionTolerance, $"\nDigital Call Option:\nStrike = {strike}\nExercise = {k + 1} years\nPrice by replication = {replicationOptionPrice}\nTarget price = {targetOptionPrice}");

            // Floating Rate Coupon - Deep-out-of-the-money Put Digital option
            strike = 0.01;
            DigitalCoupon digitalFlooredCoupon = new DigitalCoupon(
                underlying, nullstrike, PositionType.Long, false, nullstrike,
                strike, PositionType.Long, false, nullstrike).SetPricer(pricer);

            // Check price vs its target
            targetPrice = underlying.Price(vars.TermStructure, vars.Context);
            digitalPrice = digitalFlooredCoupon.Price(vars.TermStructure, vars.Context);
            tolerance = 1e-08;
            error = Math.Abs(targetPrice - digitalPrice);
            Check(error, tolerance, $"\nFloating Coupon + Digital Put Coupon:\nStrike = {strike}\nExercise = {k + 1} years\nCoupon price = {digitalPrice}\nTarget price = {targetPrice}");

            // Check digital option
            targetOptionPrice = 0.0;
            replicationOptionPrice = digitalFlooredCoupon.PutOptionRate(vars.Context) * vars.Nominal * accrualPeriod * discount;
            error = Math.Abs(targetOptionPrice - replicationOptionPrice);
            Check(error, optionTolerance, $"\nDigital Put Coupon:\nStrike = {strike}\nExercise = {k + 1} years\nPrice by replication = {replicationOptionPrice}\nTarget price = {targetOptionPrice}");
        }
    }

    [Fact]
    public void TestCashOrNothing()
    {
        // Testing European cash-or-nothing digital coupon...

        /*  Call Payoff = R Heaviside(aL+b-X)
            Value Call = R N(d2')
            Put Payoff =  R Heaviside(X-aL-b)
            Value Put = R N(-d2')
            where:
            d2' = ln(F/X')/stdDev - 0.5*stdDev;
        */

        var vars = new CommonVars();

        double[] vols = [0.05, 0.15, 0.30];
        double[] strikes = [0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07];

        double gearing = 3.0;
        double spread = -0.0002;

        double gap = 1e-08; /* very low, in order to compare digital option value
                                         with black formula result */
        var replication = new DigitalReplication(ReplicationType.Central, gap);

        foreach (double capletVol in vols)
        {
            OptionletVolatilityStructure vol = new ConstantOptionletVolatility(
                vars.Today, vars.Calendar, BusinessDayConvention.Following, capletVol, new Actual360());
            foreach (double strike in strikes)
            {
                for (int k = 0; k < 10; k++)
                {
                    Date startDate = vars.Calendar.Advance(vars.Settlement, (k + 1) * TimeUnit.Years);
                    Date endDate = vars.Calendar.Advance(vars.Settlement, (k + 2) * TimeUnit.Years);
                    double? nullstrike = null;
                    double cashRate = 0.01;

                    Date paymentDate = endDate;
                    var underlying = new IborCoupon(
                        paymentDate, vars.Nominal, startDate, endDate, vars.FixingDays, vars.Index, gearing, spread);

                    // Floating Rate Coupon - Call Digital option
                    var pricer = new BlackIborCouponPricer(vars.Context, vol);
                    DigitalCoupon digitalCappedCoupon = new DigitalCoupon(
                        underlying, strike, PositionType.Short, false, cashRate,
                        nullstrike, PositionType.Short, false, nullstrike, replication).SetPricer(pricer);

                    // Check digital option price vs N(d2) price
                    Date exerciseDate = underlying.FixingDate();
                    double forward = digitalCappedCoupon.Underlying.Rate(vars.Context);
                    double effFwd = (forward - spread) / gearing;
                    double effStrike = (strike - spread) / gearing;
                    double accrualPeriod = underlying.AccrualPeriod();
                    double discount = vars.TermStructure.Discount(endDate);
                    double stdDev = Math.Sqrt(vol.BlackVariance(exerciseDate, effStrike));
                    double itm = BlackFormulaFunctions.BlackFormulaCashItmProbability(OptionType.Call, effStrike, effFwd, stdDev);
                    double nd2Price = itm * vars.Nominal * accrualPeriod * discount * cashRate;
                    double optionPrice = digitalCappedCoupon.CallOptionRate(vars.Context) * vars.Nominal * accrualPeriod * discount;
                    double error = Math.Abs(nd2Price - optionPrice);
                    Check(error, vars.OptionTolerance, $"\nDigital Call Option:\nVolatility = {capletVol}\nStrike = {strike}\nExercise = {k + 1} years\nPrice by replication = {optionPrice}\nPrice by Reiner-Rubinstein = {nd2Price}");

                    // Check digital option price vs N(d2) price using Vanilla Option class
                    double discountAtFixing = vars.TermStructure.Discount(exerciseDate);
                    double callVO = vars.Nominal * accrualPeriod
                        * VanillaNpv(vars, new CashOrNothingPayoff(OptionType.Call, effStrike, cashRate), exerciseDate, effFwd, capletVol)
                        * discount / discountAtFixing;
                    error = Math.Abs(nd2Price - callVO);
                    Check(error, vars.BlackTolerance, $"\nDigital Call Option:\nVolatility = {capletVol}\nStrike = {strike}\nExercise = {k + 1} years\nOption price by Black asset-ot-nothing payoff = {callVO}\nOption price by Reiner-Rubinstein = {nd2Price}");

                    // Floating Rate Coupon + Put Digital option
                    DigitalCoupon digitalFlooredCoupon = new DigitalCoupon(
                        underlying, nullstrike, PositionType.Long, false, nullstrike,
                        strike, PositionType.Long, false, cashRate, replication).SetPricer(pricer);

                    // Check digital option price vs N(d2) price
                    itm = BlackFormulaFunctions.BlackFormulaCashItmProbability(OptionType.Put, effStrike, effFwd, stdDev);
                    nd2Price = itm * vars.Nominal * accrualPeriod * discount * cashRate;
                    optionPrice = digitalFlooredCoupon.PutOptionRate(vars.Context) * vars.Nominal * accrualPeriod * discount;
                    error = Math.Abs(nd2Price - optionPrice);
                    Check(error, vars.OptionTolerance, $"\nPut Digital Option:\nVolatility = {capletVol}\nStrike = {strike}\nExercise = {k + 1} years\nPrice by replication = {optionPrice}\nPrice by Reiner-Rubinstein = {nd2Price}");

                    // Check digital option price vs N(d2) price using Vanilla Option class
                    double putVO = vars.Nominal * accrualPeriod
                        * VanillaNpv(vars, new CashOrNothingPayoff(OptionType.Put, effStrike, cashRate), exerciseDate, effFwd, capletVol)
                        * discount / discountAtFixing;
                    error = Math.Abs(nd2Price - putVO);
                    Check(error, vars.BlackTolerance, $"\nDigital Put Option:\nVolatility = {capletVol}\nStrike = {strike}\nExercise = {k + 1} years\nOption price by Black asset-ot-nothing payoff = {putVO}\nOption price by Reiner-Rubinstein = {nd2Price}");
                }
            }
        }
    }

    [Fact]
    public void TestCashOrNothingDeepInTheMoney()
    {
        // Testing European deep in-the-money cash-or-nothing digital coupon...

        var vars = new CommonVars();

        double gearing = 1.0;
        double spread = 0.0;

        double capletVolatility = 0.0001;
        OptionletVolatilityStructure volatility = new ConstantOptionletVolatility(
            vars.Today, vars.Calendar, BusinessDayConvention.Following, capletVolatility, new Actual360());

        for (int k = 0; k < 10; k++)
        {
            // Loop on start and end dates
            Date startDate = vars.Calendar.Advance(vars.Settlement, (k + 1) * TimeUnit.Years);
            Date endDate = vars.Calendar.Advance(vars.Settlement, (k + 2) * TimeUnit.Years);
            double? nullstrike = null;
            double cashRate = 0.01;
            Date paymentDate = endDate;

            var pricer = new BlackIborCouponPricer(vars.Context, volatility);
            IborCoupon underlying = new IborCoupon(
                paymentDate, vars.Nominal, startDate, endDate, vars.FixingDays, vars.Index, gearing, spread).SetPricer(pricer);

            // Floating Rate Coupon - Deep-in-the-money Call Digital option
            double strike = 0.001;
            DigitalCoupon digitalCappedCoupon = new DigitalCoupon(
                underlying, strike, PositionType.Short, false, cashRate,
                nullstrike, PositionType.Short, false, nullstrike).SetPricer(pricer);

            // Check price vs its target
            double accrualPeriod = underlying.AccrualPeriod();
            double discount = vars.TermStructure.Discount(endDate);

            double targetOptionPrice = cashRate * vars.Nominal * accrualPeriod * discount;
            double targetPrice = underlying.Price(vars.TermStructure, vars.Context) - targetOptionPrice;
            double digitalPrice = digitalCappedCoupon.Price(vars.TermStructure, vars.Context);

            double error = Math.Abs(targetPrice - digitalPrice);
            double tolerance = 1e-07;
            Check(error, tolerance, $"\nFloating Coupon - Digital Call Coupon:\nStrike = {strike}\nExercise = {k + 1} years\nCoupon price = {digitalPrice}\nTarget price = {targetPrice}");

            // Check digital option price
            double replicationOptionPrice = digitalCappedCoupon.CallOptionRate(vars.Context) * vars.Nominal * accrualPeriod * discount;
            error = Math.Abs(targetOptionPrice - replicationOptionPrice);
            double optionTolerance = 1e-07;
            Check(error, optionTolerance, $"\nDigital Call Option:\nStrike = {strike}\nExercise = {k + 1} years\nPrice by replication = {replicationOptionPrice}\nTarget price = {targetOptionPrice}");

            // Floating Rate Coupon + Deep-in-the-money Put Digital option
            strike = 0.99;
            DigitalCoupon digitalFlooredCoupon = new DigitalCoupon(
                underlying, nullstrike, PositionType.Long, false, nullstrike,
                strike, PositionType.Long, false, cashRate).SetPricer(pricer);

            // Check price vs its target
            targetPrice = underlying.Price(vars.TermStructure, vars.Context) + targetOptionPrice;
            digitalPrice = digitalFlooredCoupon.Price(vars.TermStructure, vars.Context);
            error = Math.Abs(targetPrice - digitalPrice);
            Check(error, tolerance, $"\nFloating Coupon + Digital Put Option:\nStrike = {strike}\nExercise = {k + 1} years\nCoupon price = {digitalPrice}\nTarget price = {targetPrice}");

            // Check digital option
            replicationOptionPrice = digitalFlooredCoupon.PutOptionRate(vars.Context) * vars.Nominal * accrualPeriod * discount;
            error = Math.Abs(targetOptionPrice - replicationOptionPrice);
            Check(error, optionTolerance, $"\nDigital Put Coupon:\nStrike = {strike}\nExercise = {k + 1} years\nPrice by replication = {replicationOptionPrice}\nTarget price = {targetOptionPrice}");
        }
    }

    [Fact]
    public void TestCashOrNothingDeepOutTheMoney()
    {
        // Testing European deep out-the-money cash-or-nothing digital coupon...

        var vars = new CommonVars();

        double gearing = 1.0;
        double spread = 0.0;

        double capletVolatility = 0.0001;
        OptionletVolatilityStructure volatility = new ConstantOptionletVolatility(
            vars.Today, vars.Calendar, BusinessDayConvention.Following, capletVolatility, new Actual360());

        for (int k = 0; k < 10; k++)
        {
            // loop on start and end dates
            Date startDate = vars.Calendar.Advance(vars.Settlement, (k + 1) * TimeUnit.Years);
            Date endDate = vars.Calendar.Advance(vars.Settlement, (k + 2) * TimeUnit.Years);
            double? nullstrike = null;
            double cashRate = 0.01;
            Date paymentDate = endDate;

            var pricer = new BlackIborCouponPricer(vars.Context, volatility);
            IborCoupon underlying = new IborCoupon(
                paymentDate, vars.Nominal, startDate, endDate, vars.FixingDays, vars.Index, gearing, spread).SetPricer(pricer);

            // Deep out-of-the-money Capped Digital Coupon
            double strike = 0.99;
            DigitalCoupon digitalCappedCoupon = new DigitalCoupon(
                underlying, strike, PositionType.Short, false, cashRate,
                nullstrike, PositionType.Short, false, nullstrike).SetPricer(pricer);

            // Check price vs its target
            double accrualPeriod = underlying.AccrualPeriod();
            double discount = vars.TermStructure.Discount(endDate);

            double targetPrice = underlying.Price(vars.TermStructure, vars.Context);
            double digitalPrice = digitalCappedCoupon.Price(vars.TermStructure, vars.Context);
            double error = Math.Abs(targetPrice - digitalPrice);
            double tolerance = 1e-10;
            Check(error, tolerance, $"\nFloating Coupon + Digital Call Option:\nStrike = {strike}\nExercise = {k + 1} years\nCoupon price = {digitalPrice}\nTarget price = {targetPrice}");

            // Check digital option price
            double targetOptionPrice = 0.0;
            double replicationOptionPrice = digitalCappedCoupon.CallOptionRate(vars.Context) * vars.Nominal * accrualPeriod * discount;
            error = Math.Abs(targetOptionPrice - replicationOptionPrice);
            double optionTolerance = 1e-10;
            Check(error, optionTolerance, $"\nDigital Call Option:\nStrike = {strike}\nExercise = {k + 1} years\nPrice by replication = {replicationOptionPrice}\nTarget price = {targetOptionPrice}");

            // Deep out-of-the-money Floored Digital Coupon
            strike = 0.01;
            DigitalCoupon digitalFlooredCoupon = new DigitalCoupon(
                underlying, nullstrike, PositionType.Long, false, nullstrike,
                strike, PositionType.Long, false, cashRate).SetPricer(pricer);

            // Check price vs its target
            targetPrice = underlying.Price(vars.TermStructure, vars.Context);
            digitalPrice = digitalFlooredCoupon.Price(vars.TermStructure, vars.Context);
            tolerance = 1e-09;
            error = Math.Abs(targetPrice - digitalPrice);
            Check(error, tolerance, $"\nFloating Coupon + Digital Put Option:\nStrike = {strike}\nExercise = {k + 1} years\nCoupon price = {digitalPrice}\nTarget price = {targetPrice}");

            // Check digital option
            targetOptionPrice = 0.0;
            replicationOptionPrice = digitalFlooredCoupon.PutOptionRate(vars.Context) * vars.Nominal * accrualPeriod * discount;
            error = Math.Abs(targetOptionPrice - replicationOptionPrice);
            Check(error, optionTolerance, $"\nDigital Put Coupon:\nStrike = {strike}\nExercise = {k + 1} years\nPrice by replication = {replicationOptionPrice}\nTarget price = {targetOptionPrice}");
        }
    }

    [Fact]
    public void TestCallPutParity()
    {
        // Testing call/put parity for European digital coupon...

        var vars = new CommonVars();

        double[] vols = [0.05, 0.15, 0.30];
        double[] strikes = [0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07];

        double gearing = 1.0;
        double spread = 0.0;

        foreach (double capletVolatility in vols)
        {
            OptionletVolatilityStructure volatility = new ConstantOptionletVolatility(
                vars.Today, vars.Calendar, BusinessDayConvention.Following, capletVolatility, new Actual360());
            foreach (double strike in strikes)
            {
                for (int k = 0; k < 10; k++)
                {
                    Date startDate = vars.Calendar.Advance(vars.Settlement, (k + 1) * TimeUnit.Years);
                    Date endDate = vars.Calendar.Advance(vars.Settlement, (k + 2) * TimeUnit.Years);
                    double? nullstrike = null;

                    Date paymentDate = endDate;

                    var pricer = new BlackIborCouponPricer(vars.Context, volatility);
                    IborCoupon underlying = new IborCoupon(
                        paymentDate, vars.Nominal, startDate, endDate, vars.FixingDays, vars.Index, gearing, spread).SetPricer(pricer);

                    // Cash-or-Nothing
                    double cashRate = 0.01;

                    // Floating Rate Coupon + Call Digital option
                    DigitalCoupon cashDigitalCallCoupon = new DigitalCoupon(
                        underlying, strike, PositionType.Long, false, cashRate,
                        nullstrike, PositionType.Long, false, nullstrike).SetPricer(pricer);

                    // Floating Rate Coupon - Put Digital option
                    DigitalCoupon cashDigitalPutCoupon = new DigitalCoupon(
                        underlying, nullstrike, PositionType.Long, false, nullstrike,
                        strike, PositionType.Short, false, cashRate).SetPricer(pricer);

                    double digitalPrice = cashDigitalCallCoupon.Price(vars.TermStructure, vars.Context)
                        - cashDigitalPutCoupon.Price(vars.TermStructure, vars.Context);

                    // Target price
                    double accrualPeriod = underlying.AccrualPeriod();
                    double discount = vars.TermStructure.Discount(endDate);
                    double targetPrice = vars.Nominal * accrualPeriod * discount * cashRate;

                    double error = Math.Abs(targetPrice - digitalPrice);
                    double tolerance = 1.0e-08;
                    Check(error, tolerance, $"\nCash-or-nothing:\nVolatility = {capletVolatility}\nStrike = {strike}\nExercise = {k + 1} years\nPrice = {digitalPrice}\nTarget Price  = {targetPrice}");

                    // Asset-or-Nothing
                    // Floating Rate Coupon + Call Digital option
                    DigitalCoupon assetDigitalCallCoupon = new DigitalCoupon(
                        underlying, strike, PositionType.Long, false, nullstrike,
                        nullstrike, PositionType.Long, false, nullstrike).SetPricer(pricer);

                    // Floating Rate Coupon - Put Digital option
                    DigitalCoupon assetDigitalPutCoupon = new DigitalCoupon(
                        underlying, nullstrike, PositionType.Long, false, nullstrike,
                        strike, PositionType.Short, false, nullstrike).SetPricer(pricer);
                    digitalPrice = assetDigitalCallCoupon.Price(vars.TermStructure, vars.Context)
                        - assetDigitalPutCoupon.Price(vars.TermStructure, vars.Context);

                    // Target price
                    targetPrice = vars.Nominal * accrualPeriod * discount * underlying.Rate(vars.Context);
                    error = Math.Abs(targetPrice - digitalPrice);
                    tolerance = 1.0e-07;
                    Check(error, tolerance, $"\nAsset-or-nothing:\nVolatility = {capletVolatility}\nStrike = {strike}\nExercise = {k + 1} years\nPrice = {digitalPrice}\nTarget Price  = {targetPrice}");
                }
            }
        }
    }

    [Fact]
    public void TestReplicationType()
    {
        // Testing replication type for European digital coupon...

        var vars = new CommonVars();

        double[] vols = [0.05, 0.15, 0.30];
        double[] strikes = [0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07];

        double gearing = 1.0;
        double spread = 0.0;

        double gap = 1e-04;
        var subReplication = new DigitalReplication(ReplicationType.Sub, gap);
        var centralReplication = new DigitalReplication(ReplicationType.Central, gap);
        var superReplication = new DigitalReplication(ReplicationType.Super, gap);

        foreach (double capletVolatility in vols)
        {
            OptionletVolatilityStructure volatility = new ConstantOptionletVolatility(
                vars.Today, vars.Calendar, BusinessDayConvention.Following, capletVolatility, new Actual360());
            foreach (double strike in strikes)
            {
                for (int k = 0; k < 10; k++)
                {
                    Date startDate = vars.Calendar.Advance(vars.Settlement, (k + 1) * TimeUnit.Years);
                    Date endDate = vars.Calendar.Advance(vars.Settlement, (k + 2) * TimeUnit.Years);
                    double? nullstrike = null;

                    Date paymentDate = endDate;

                    var underlying = new IborCoupon(
                        paymentDate, vars.Nominal, startDate, endDate, vars.FixingDays, vars.Index, gearing, spread);

                    // Cash-or-Nothing
                    double cashRate = 0.005;
                    var pricer = new BlackIborCouponPricer(vars.Context, volatility);

                    // Floating Rate Coupon + Call Digital option
                    CheckReplication(
                        vars, "Floating Rate Coupon + Call Digital option", capletVolatility, strike, k,
                        r => new DigitalCoupon(
                            underlying, strike, PositionType.Long, false, cashRate,
                            nullstrike, PositionType.Long, false, nullstrike, r).SetPricer(pricer),
                        subReplication, centralReplication, superReplication);

                    // Floating Rate Coupon - Call Digital option
                    CheckReplication(
                        vars, "Floating Rate Coupon - Call Digital option", capletVolatility, strike, k,
                        r => new DigitalCoupon(
                            underlying, strike, PositionType.Short, false, cashRate,
                            nullstrike, PositionType.Long, false, nullstrike, r).SetPricer(pricer),
                        subReplication, centralReplication, superReplication);

                    // Floating Rate Coupon + Put Digital option
                    CheckReplication(
                        vars, "Floating Rate Coupon + Put Digital option", capletVolatility, strike, k,
                        r => new DigitalCoupon(
                            underlying, nullstrike, PositionType.Long, false, nullstrike,
                            strike, PositionType.Long, false, cashRate, r).SetPricer(pricer),
                        subReplication, centralReplication, superReplication);

                    // Floating Rate Coupon - Put Digital option
                    CheckReplication(
                        vars, "Floating Rate Coupon - Put Digital option", capletVolatility, strike, k,
                        r => new DigitalCoupon(
                            underlying, nullstrike, PositionType.Long, false, nullstrike,
                            strike, PositionType.Short, false, cashRate, r).SetPricer(pricer),
                        subReplication, centralReplication, superReplication);
                }
            }
        }
    }

    // The sub-, central and super-replicated prices of one coupon are ordered, within QuantLib's 1e-9 (lines 985-1093,
    // one block per coupon there).
    private static void CheckReplication(
        CommonVars vars,
        string what,
        double capletVolatility,
        double strike,
        int k,
        Func<DigitalReplication, DigitalCoupon> coupon,
        DigitalReplication subReplication,
        DigitalReplication centralReplication,
        DigitalReplication superReplication)
    {
        double subDigitalPrice = coupon(subReplication).Price(vars.TermStructure, vars.Context);
        double centralDigitalPrice = coupon(centralReplication).Price(vars.TermStructure, vars.Context);
        double overDigitalPrice = coupon(superReplication).Price(vars.TermStructure, vars.Context);
        double tolerance = 1.0e-09;
        if (((subDigitalPrice > centralDigitalPrice) && Math.Abs(centralDigitalPrice - subDigitalPrice) > tolerance)
            || ((centralDigitalPrice > overDigitalPrice) && Math.Abs(centralDigitalPrice - overDigitalPrice) > tolerance))
        {
            Assert.Fail($"\n{what}:\nVolatility = {capletVolatility}\nStrike = {strike}\nExercise = {k + 1} years"
                        + $"\nSub-Replication Price = {subDigitalPrice:R}\nCentral-Replication Price = {centralDigitalPrice:R}"
                        + $"\nOver-Replication Price = {overDigitalPrice:R}");
        }
    }

    // A European option's NPV on a Black-Scholes process with spot effFwd·P(exercise), no dividends, the curve's rates
    // and a flat volatility on Actual/360 (lines 152-171, 215-232, 545-565).
    private static double VanillaNpv(CommonVars vars, StrikedTypePayoff payoff, Date exerciseDate, double effFwd, double capletVol)
    {
        var exercise = new EuropeanExercise(exerciseDate);
        double discountAtFixing = vars.TermStructure.Discount(exerciseDate);
        var qTS = new FlatForward(vars.Today, 0.0, new Actual360());
        var volTS = new BlackConstantVol(vars.Today, new NullCalendar(), capletVol, new Actual360());
        var stochProcess = new BlackScholesMertonProcess(effFwd * discountAtFixing, qTS, vars.TermStructure, volTS);
        var option = new VanillaOption(payoff, exercise);
        return AnalyticEuropeanEngine.Calculate(option, vars.Context, stochProcess).NPV!.Value;
    }

    private static void Check(double error, double tolerance, string message)
    {
        if (error > tolerance)
        {
            Assert.Fail(message + $"\nError = {error}");
        }
    }

    private sealed class CommonVars
    {
        // setup
        public CommonVars()
        {
            FixingDays = 2;
            Nominal = 1000000.0;
            Calendar = new TARGET();
            Today = Calendar.Adjust(new Date(16, Month.September, 2015));
            Context = new EvaluationContext { EvaluationDate = Today };
            Settlement = Calendar.Advance(Today, FixingDays, TimeUnit.Days);
            TermStructure = new FlatForward(Settlement, 0.05, new Actual365Fixed());
            Index = new Euribor6M(TermStructure);
            OptionTolerance = 1.0e-04;
            BlackTolerance = 1e-10;
        }

        // global data
        public Date Today { get; }

        public Date Settlement { get; }

        public double Nominal { get; }

        public Calendar Calendar { get; }

        public IborIndex Index { get; }

        public int FixingDays { get; }

        public YieldTermStructure TermStructure { get; }

        public double OptionTolerance { get; }

        public double BlackTolerance { get; }

        public EvaluationContext Context { get; }
    }
}
