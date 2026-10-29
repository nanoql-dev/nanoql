// SPDX-FileCopyrightText: Copyright (C) 2017 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/squarerootclvmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class SquareRootCLVModelTests
{
    // Adapted: the test suite's flatRate without a date reads the evaluation date, which the context carries; Boost's
    // non-central chi-squared density is the polynomial's weight function, the same Boost pdf.
    [Fact]
    public void TestSquareRootCLVVanillaPricing()
    {
        // Testing vanilla option pricing with square-root kernel process...

        var todaysDate = new Date(5, Month.October, 2016);

        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        Date maturityDate = todaysDate + new Period(3, TimeUnit.Months);
        double maturity = dc.YearFraction(todaysDate, maturityDate);

        const double s0 = 100;

        const double r = 0.08;
        const double q = 0.03;
        const double vol = 0.3;

        YieldTermStructure rTS = new FlatForward(todaysDate, r, dc);
        YieldTermStructure qTS = new FlatForward(todaysDate, q, dc);
        BlackVolTermStructure volTS = new BlackConstantVol(todaysDate, new NullCalendar(), vol, dc);
        double fwd = s0 * qTS.Discount(maturity) / rTS.Discount(maturity);

        var bsProcess = new GeneralizedBlackScholesProcess(s0, qTS, rTS, volTS);

        const double kappa = 1.0;
        const double theta = 0.06;
        const double sigma = 0.2;
        const double x0 = 0.09;

        var sqrtProcess = new SquareRootProcess(theta, kappa, sigma, x0);

        Date[] maturityDates = [maturityDate];

        var model = new SquareRootCLVModel(bsProcess, sqrtProcess, maturityDates, 14, 1 - 1e-14, 1e-14);

        Vector x = model.CollocationPointsX(maturityDate);
        Vector y = model.CollocationPointsY(maturityDate);

        var g = new LagrangeInterpolation(x.AsSpan(), y.AsSpan());

        double df = 4 * theta * kappa / (sigma * sigma);
        double ncp = 4 * kappa * Math.Exp(-kappa * maturity) / (sigma * sigma * (1 - Math.Exp(-kappa * maturity)))
            * sqrtProcess.X0;

        var dist = new GaussNonCentralChiSquaredPolynomial(df, ncp);

        double[] strikes = [50, 75, 100, 125, 150, 200];
        foreach (double strike in strikes)
        {
            OptionType optionType = strike > fwd ? OptionType.Call : OptionType.Put;

            double expected = new BlackCalculator(
                optionType, strike, fwd, Math.Sqrt(volTS.BlackVariance(maturity, strike)), rTS.Discount(maturity)).Value;

            var clvModelPayoff = new PlainVanillaPayoff(optionType, strike);

            double F(double xi) => clvModelPayoff.Evaluate(g.Evaluate(xi)) * dist.W(xi);

            double calculated = new GaussLobattoIntegral(1000, 1e-6).Integrate(F, x[0], x[x.Size - 1]) * rTS.Discount(maturity);

            const double tol = 5e-3;
            if (Math.Abs(expected - calculated) > tol)
            {
                Assert.Fail(
                    "failed to reproduce option SquaredCLVMOdel prices"
                    + $"\n    time:       {maturityDate}"
                    + $"\n    strike:     {strike}"
                    + $"\n    expected:   {expected}"
                    + $"\n    calculated: {calculated}");
            }
        }
    }

    [Fact]
    public void TestSquareRootCLVMappingFunction()
    {
        // Testing mapping function of the square-root kernel process...

        var todaysDate = new Date(16, Month.October, 2016);
        Date maturityDate = todaysDate + new Period(1, TimeUnit.Years);

        DayCounter dc = new Actual365Fixed();

        const double s0 = 100;

        const double r = 0.05;
        const double q = 0.02;

        YieldTermStructure rTS = new FlatForward(todaysDate, r, dc);
        YieldTermStructure qTS = new FlatForward(todaysDate, q, dc);

        // SABR
        const double beta = 0.95;
        const double alpha = 0.2;
        const double rho = -0.9;
        const double gamma = 0.8;

        BlackVolTermStructure sabrVol = new SABRVolTermStructure(alpha, beta, gamma, rho, s0, r, todaysDate, dc);

        var bsProcess = new GeneralizedBlackScholesProcess(s0, qTS, rTS, sabrVol);

        List<Date> calibrationDates = [todaysDate + new Period(3, TimeUnit.Months)];
        while (calibrationDates[^1] < maturityDate)
        {
            calibrationDates.Add(calibrationDates[^1] + new Period(1, TimeUnit.Weeks));
        }

        // sqrt process
        const double kappa = 1.0;
        const double theta = 0.09;
        const double sigma = 0.2;
        const double x0 = 0.09;

        var sqrtProcess = new SquareRootProcess(theta, kappa, sigma, x0);

        var model = new SquareRootCLVModel(bsProcess, sqrtProcess, calibrationDates, 14, 1 - 1e-10, 1e-10);

        Func<double, double, double> g = model.G();

        double[] strikes = [80, 100, 120];
        int[] offsets = [92, 182, 183, 184, 185, 186, 365];
        foreach (int offset in offsets)
        {
            Date m = todaysDate + new Period(offset, TimeUnit.Days);
            double t = dc.YearFraction(todaysDate, m);

            double df = 4 * theta * kappa / (sigma * sigma);
            double ncp = 4 * kappa * Math.Exp(-kappa * t) / (sigma * sigma * (1 - Math.Exp(-kappa * t))) * sqrtProcess.X0;

            var dist = new GaussNonCentralChiSquaredPolynomial(df, ncp);

            double fwd = s0 * qTS.Discount(m) / rTS.Discount(m);

            foreach (double strike in strikes)
            {
                OptionType optionType = strike > fwd ? OptionType.Call : OptionType.Put;

                double expected = new BlackCalculator(
                    optionType, strike, fwd, Math.Sqrt(sabrVol.BlackVariance(m, strike)), rTS.Discount(m)).Value;

                var clvModelPayoff = new PlainVanillaPayoff(optionType, strike);

                double F(double xi) => clvModelPayoff.Evaluate(g(t, xi)) * dist.W(xi);

                Vector x = model.CollocationPointsX(m);
                double calculated = new GaussLobattoIntegral(1000, 1e-3).Integrate(F, x[0], x[x.Size - 1]) * rTS.Discount(m);

                const double tol = 0.075;

                if (Math.Abs(expected) > 0.01 && Math.Abs((calculated - expected) / calculated) > tol)
                {
                    Assert.Fail(
                        "failed to reproduce option SquaredCLVMOdel prices"
                        + $"\n    time:       {m}"
                        + $"\n    strike:     {strike}"
                        + $"\n    expected:   {expected}"
                        + $"\n    calculated: {calculated}");
                }
            }
        }
    }
}
