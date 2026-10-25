// SPDX-FileCopyrightText: Copyright (C) 2008 Yee Man Chan
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/gjrgarchmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class GjrGarchModelTests
{
    [Fact]
    public void TestEngines()
    {
        // Testing Monte Carlo GJR-GARCH engine against analytic GJR-GARCH engine...

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISDA);

        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure riskFreeTS = new FlatForward(today, 0.05, dayCounter);
        YieldTermStructure dividendTS = new FlatForward(today, 0.0, dayCounter);

        const double s0 = 50.0;
        const double omega = 2.0e-6;
        const double alpha = 0.024;
        const double beta = 0.93;
        const double gamma = 0.059;
        const double daysPerYear = 365.0; // number of trading days per year
        int[] maturity = [90, 180];
        double[] strike = [35, 40, 45, 50, 55, 60];
        double[] lambdas = [0.0, 0.1, 0.2];

        // correct values of analytic approximation
        double[,,] analytic =
        {
            {
                { 15.4315, 10.5552, 5.9625, 2.3282, 0.5408, 0.0835 },
                { 15.8969, 11.2173, 6.9112, 3.4788, 1.3769, 0.4357 },
            },
            {
                { 15.4556, 10.6929, 6.2381, 2.6831, 0.7822, 0.1738 },
                { 16.0587, 11.5338, 7.3170, 3.9074, 1.7279, 0.6568 },
            },
            {
                { 15.8000, 11.2734, 7.0376, 3.6767, 1.5871, 0.5934 },
                { 16.9286, 12.3170, 8.0405, 4.6348, 2.3429, 1.0590 },
            },
        };

        // correct values of Monte Carlo
        double[,,] mcValues =
        {
            {
                { 15.4332, 10.5453, 5.9351, 2.3521, 0.5597, 0.0776 },
                { 15.8910, 11.1772, 6.8827, 3.5096, 1.4196, 0.4502 },
            },
            {
                { 15.4580, 10.6433, 6.2019, 2.7513, 0.8374, 0.1706 },
                { 15.9884, 11.4139, 7.3103, 4.0497, 1.8862, 0.7322 },
            },
            {
                { 15.6619, 11.1263, 7.0968, 3.9152, 1.8133, 0.7010 },
                { 16.5195, 12.3181, 8.6085, 5.5700, 3.3103, 1.8053 },
            },
        };

        var cumNormal = new CumulativeNormalDistribution();
        for (int k = 0; k < 3; ++k)
        {
            double lambda = lambdas[k];
            double m1 = beta + ((alpha + (gamma * cumNormal.Evaluate(lambda)))
                * (1.0 + (lambda * lambda))) + (gamma * lambda * Math.Exp(-lambda * lambda / 2.0)
                / Math.Sqrt(2.0 * Math.PI));
            double v0 = omega / (1.0 - m1);
            var process = new GJRGARCHProcess(riskFreeTS, dividendTS, s0, v0, omega, alpha, beta, gamma, lambda, daysPerYear);
            Func<VanillaOption, EvaluationContext, OneAssetOptionResults> engine1 =
                new MakeMCEuropeanGJRGARCHEngine<PseudoRandom, GeneralStatistics>(process, 1234)
                    .WithStepsPerYear(20)
                    .WithAbsoluteTolerance(0.02)
                    .ToEngine();

            var engine2 = new AnalyticGJRGARCHEngine(new GJRGARCHModel(process));
            for (int i = 0; i < 2; ++i)
            {
                for (int j = 0; j < 6; ++j)
                {
                    double x = strike[j];

                    var payoff = new PlainVanillaPayoff(OptionType.Call, x);
                    Date exDate = today + maturity[i];
                    var exercise = new EuropeanExercise(exDate);

                    var option = new VanillaOption(payoff, exercise);

                    double calculated = engine1(option, context).NPV!.Value;

                    double expected = engine2.Calculate(option, context).NPV!.Value;
                    double tolerance = 7.5e-2;

                    Assert.False(
                        Math.Abs(expected - analytic[k, i, j]) > 2.0 * tolerance,
                        $"failed to match results from engines\n    correct value:    {analytic[k, i, j]}\n    Analytic Approx.: {expected} +/- {tolerance}");
                    Assert.False(
                        Math.Abs(calculated - mcValues[k, i, j]) > 2.0 * tolerance,
                        $"failed to match results from engines\n    correct value:    {mcValues[k, i, j]}\n    Monte Carlo: {calculated} +/- {tolerance}");
                }
            }
        }
    }

    [Fact]
    public void TestDAXCalibration()
    {
        /* this example is taken from A. Sepp
           Pricing European-Style Options under Jump Diffusion Processes
           with Stochstic Volatility: Applications of Fourier Transform
           http://math.ut.ee/~spartak/papers/stochjumpvols.pdf
        */

        // Testing GJR-GARCH model calibration using DAX volatility data...

        var settlementDate = new Date(5, Month.July, 2002);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        DayCounter dayCounter = new Actual365Fixed();
        Calendar calendar = new TARGET();

        int[] t = [13, 41, 75, 165, 256, 345, 524, 703];
        double[] r = [0.0357, 0.0349, 0.0341, 0.0355, 0.0359, 0.0368, 0.0386, 0.0401];

        var dates = new List<Date> { settlementDate };
        var rates = new List<double> { 0.0357 };
        int i;
        for (i = 0; i < 8; ++i)
        {
            dates.Add(settlementDate + t[i]);
            rates.Add(r[i]);
        }

        YieldTermStructure riskFreeTS = new ZeroCurve(dates, rates, dayCounter);

        YieldTermStructure dividendTS = new FlatForward(settlementDate, 0.0, dayCounter);

        double[] v =
        [
            0.6625, 0.4875, 0.4204, 0.3667, 0.3431, 0.3267, 0.3121, 0.3121,
            0.6007, 0.4543, 0.3967, 0.3511, 0.3279, 0.3154, 0.2984, 0.2921,
            0.5084, 0.4221, 0.3718, 0.3327, 0.3155, 0.3027, 0.2919, 0.2889,
            0.4541, 0.3869, 0.3492, 0.3149, 0.2963, 0.2926, 0.2819, 0.2800,
            0.4060, 0.3607, 0.3330, 0.2999, 0.2887, 0.2811, 0.2751, 0.2775,
            0.3726, 0.3396, 0.3108, 0.2781, 0.2788, 0.2722, 0.2661, 0.2686,
            0.3550, 0.3277, 0.3012, 0.2781, 0.2781, 0.2661, 0.2661, 0.2681,
            0.3428, 0.3209, 0.2958, 0.2740, 0.2688, 0.2627, 0.2580, 0.2620,
            0.3302, 0.3062, 0.2799, 0.2631, 0.2573, 0.2533, 0.2504, 0.2544,
            0.3343, 0.2959, 0.2705, 0.2540, 0.2504, 0.2464, 0.2448, 0.2462,
            0.3460, 0.2845, 0.2624, 0.2463, 0.2425, 0.2385, 0.2373, 0.2422,
            0.3857, 0.2860, 0.2578, 0.2399, 0.2357, 0.2327, 0.2312, 0.2351,
            0.3976, 0.2860, 0.2607, 0.2356, 0.2297, 0.2268, 0.2241, 0.2320,
        ];

        const double s0 = 4468.17;
        double[] strike = [3400, 3600, 3800, 4000, 4200, 4400, 4500, 4600, 4800, 5000, 5200, 5400, 5600];

        var options = new List<HestonModelHelper>();

        const double omega = 2.0e-6;
        const double alpha = 0.024;
        const double beta = 0.93;
        const double gamma = 0.059;
        const double lambda = 0.1;
        const double daysPerYear = 365.0; // number of trading days per year
        double m1 = beta + ((alpha + (gamma * new CumulativeNormalDistribution().Evaluate(lambda)))
            * (1.0 + (lambda * lambda))) + (gamma * lambda * Math.Exp(-lambda * lambda / 2.0)
            / Math.Sqrt(2.0 * Math.PI));
        double v0 = omega / (1.0 - m1);

        var process = new GJRGARCHProcess(riskFreeTS, dividendTS, s0, v0, omega, alpha, beta, gamma, lambda, daysPerYear);
        var model = new GJRGARCHModel(process);

        // QuantLib's one engine, which its helpers share: an engine for each model the calibration hands the helpers,
        // its moments' memo shared by the evaluation's helpers (the S-hes plan's HR4 (iv))
        AnalyticGJRGARCHEngine? engine = null;
        InstrumentResults Price(VanillaOption option, EvaluationContext c, GJRGARCHModel m)
        {
            if (engine is null || !ReferenceEquals(engine.Model, m))
            {
                engine = new AnalyticGJRGARCHEngine(m);
            }

            return engine.Calculate(option, c);
        }

        for (int s = 3; s < 10; ++s)
        {
            for (int m = 0; m < 3; ++m)
            {
                double vol = v[(s * 8) + m];

                var maturity = new Period((int)((t[m] + 3) / 7.0), TimeUnit.Weeks); // round to weeks
                var option = new HestonModelHelper(
                    maturity,
                    calendar,
                    s0,
                    strike[s],
                    vol,
                    riskFreeTS,
                    dividendTS,
                    context,
                    BlackCalibrationHelperCalibrationErrorType.ImpliedVolError);
                options.Add(option.SetPricingEngine<GJRGARCHModel>(Price));
            }
        }

        var om = new Simplex(0.05);
        GJRGARCHModel calibrated = model.Calibrate(options, om, new EndCriteria(400, 40, 1.0e-8, 1.0e-8, 1.0e-8), context).Model;

        double sse = 0;
        for (i = 0; i < options.Count; ++i)
        {
            double diff = options[i].CalibrationError(calibrated, context) * 100.0;
            sse += diff * diff;
        }

        double maxExpected = 15;
        Assert.False(
            sse > maxExpected,
            $"Failed to reproduce calibration error\n    calculated: {sse}\n    expected: < {maxExpected}");
    }
}
