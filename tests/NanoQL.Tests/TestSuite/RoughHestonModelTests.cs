// SPDX-FileCopyrightText: Copyright (C) 2026 Colin Alberts
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/roughhestonmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;
using System.Runtime.CompilerServices;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;
using Vector = NanoQL.Numerics.Vector;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class RoughHestonModelTests
{
    private static readonly Date Today = new(2, July, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Fact]
    public void TestFractionalAdamsScheme()
    {
        // Testing fractional Adams scheme against known solutions...

        // D^a y = Gamma(5)/Gamma(5-a) t^(4-a) + t^4 - y with y(0) = 0
        // has the exact solution y(t) = t^4
        const double Alpha = 0.6;
        double g = new GammaFunction().Value(5.0) / new GammaFunction().Value(5.0 - Alpha);

        const double TMax = 1.0;

        double MaxError(int steps)
        {
            double[] y = new FractionalAdams<double>(Alpha).Solve(
                (t, x) => (g * Math.Pow(t, 4 - Alpha)) + (t * t * t * t) - x, 0.0, TMax, steps);

            double error = 0.0;
            for (int i = 0; i <= steps; ++i)
            {
                double t = (double)i * TMax / steps;
                error = Math.Max(error, Math.Abs(y[i] - (t * t * t * t)));
            }

            return error;
        }

        double coarseError = MaxError(100), fineError = MaxError(200);

        const double Tol = 3e-4;
        Assert.True(fineError <= Tol, $"failed to reproduce known fractional ODE solution: max error {fineError}");

        // at convergence order 1 + alpha = 1.6, halving the step size
        // should reduce the error by about a factor of three
        const double MinRatio = 2.2;
        Assert.True(
            coarseError / fineError >= MinRatio,
            $"failed to reproduce fractional Adams convergence order: ratio {coarseError / fineError}");

        // the linear problem D^a y = -y with y(0) = 1 has the solution
        // y(t) = E_a(-t^a), the Mittag-Leffler function, computed here
        // by direct series summation
        double sum = 0.0, sgn = 1.0;
        for (int k = 0; k < 100; ++k, sgn = -sgn)
        {
            sum += sgn / new GammaFunction().Value((Alpha * k) + 1.0);
        }

        double mittagLefflerRef = sum;

        double[] w = new FractionalAdams<double>(Alpha).Solve((_, x) => -x, 1.0, 1.0, 400);

        const double MlTol = 1e-4;
        Assert.True(
            Math.Abs(w[^1] - mittagLefflerRef) <= MlTol,
            $"failed to reproduce Mittag-Leffler solution: calculated {w[^1]}, expected {mittagLefflerRef}");

        // the pricing engine only ever instantiates FractionalAdams<complex<Real>>;
        // D^a z = i omega z with z(0) = 1 has the solution z(t) = E_a(i omega t^a)
        var lambda = new Complex(0.0, 1.5);
        Complex sumC = Complex.Zero, term = Complex.One;
        for (int k = 0; k < 100; ++k, term *= lambda)
        {
            sumC += term / new GammaFunction().Value((Alpha * k) + 1.0);
        }

        Complex[] wc = new FractionalAdams<Complex>(Alpha).Solve((_, x) => lambda * x, Complex.One, 1.0, 400);

        Assert.True(
            Complex.Abs(wc[^1] - sumC) <= MlTol,
            $"failed to reproduce complex Mittag-Leffler solution: calculated {wc[^1]}, expected {sumC}");
    }

    [Fact]
    public void TestRiemannLiouvilleIntegral()
    {
        // Testing product-trapezoidal Riemann-Liouville integration...

        // I^a t^p = Gamma(p+1)/Gamma(p+1+a) t^(p+a)
        const int Steps = 500;
        const double TMax = 2.0;
        const double Dt = TMax / Steps;

        foreach (double p in new[] { 1.0, 2.0, 3.0 })
        {
            double[] y = new double[Steps + 1];
            for (int i = 0; i <= Steps; ++i)
            {
                y[i] = Math.Pow(i * Dt, p);
            }

            foreach (double a in new[] { 0.0, 0.25, 0.6, 1.0 })
            {
                double calculated = FractionalAdamsFunctions.RiemannLiouvilleIntegral<double>(y, a, Dt);

                double expected = new GammaFunction().Value(p + 1.0) / new GammaFunction().Value(p + 1.0 + a)
                    * Math.Pow(TMax, p + a);

                double tol = (p == 1.0) ? 1e-12 : 5e-5;
                Assert.True(
                    Math.Abs(calculated - expected) <= tol,
                    $"failed to reproduce fractional integral of t^p: p {p}, alpha {a}, calculated {calculated}, expected {expected}");

                // FractionalAdams only ever integrates complex-valued vectors in
                // production; check the templated complex path directly
                var yc = new Complex[y.Length];
                for (int i = 0; i <= Steps; ++i)
                {
                    yc[i] = new Complex(1.0, 1.0) * y[i];
                }

                Complex calculatedC = FractionalAdamsFunctions.RiemannLiouvilleIntegral<Complex>(yc, a, Dt);
                Complex expectedC = new Complex(1.0, 1.0) * expected;

                Assert.True(
                    Complex.Abs(calculatedC - expectedC) <= tol,
                    $"failed to reproduce complex fractional integral of t^p: p {p}, alpha {a}, calculated {calculatedC}, expected {expectedC}");
            }
        }
    }

    [Fact]
    public void TestRiemannLiouvilleWeightReuse()
    {
        // Testing reusable Riemann-Liouville quadrature weights...

        // steps = 1 leaves the interior loop empty, steps = 2 runs it once and
        // reaches index 0 of the power table, steps = 256 is the engine default.
        foreach (int steps in new[] { 1, 2, 256 })
        {
            double[] real = new double[steps + 1];
            var complex = new Complex[steps + 1];

            for (int i = 0; i <= steps; ++i)
            {
                double x = (double)i / steps;
                real[i] = Math.Exp(-0.7 * x) * (1.0 + (x * x));
                complex[i] = new Complex(real[i], Math.Sin(37.0 * x));
            }

            // 0 and 1 are the degenerate orders; 0.25 is the fractional one the
            // engine asks for at H = 0.25
            foreach (double alpha in new[] { 0.0, 0.25, 1.0 })
            {
                var reused = new RiemannLiouvilleWeights(alpha, steps);

                // the spacing varies with the maturity while the weights do not,
                // so a reused object has to survive a change of dt
                foreach (double dt in new[] { 1e-3, 0.5 })
                {
                    double calculated = reused.Integrate<double>(real, dt);
                    double expected = FractionalAdamsFunctions.RiemannLiouvilleIntegral<double>(real, alpha, dt);
                    Assert.Equal(expected, calculated);

                    // production only ever integrates complex vectors
                    Complex calculatedC = reused.Integrate<Complex>(complex, dt);
                    Complex expectedC = FractionalAdamsFunctions.RiemannLiouvilleIntegral<Complex>(complex, alpha, dt);
                    Assert.Equal(expectedC.Real, calculatedC.Real);
                    Assert.Equal(expectedC.Imaginary, calculatedC.Imaginary);
                }
            }
        }

        const int Steps = 64;
        const double Dt = 0.01;

        double[] yv = new double[Steps + 1];
        for (int i = 0; i <= Steps; ++i)
        {
            yv[i] = 1.0 + Math.Sin(i);
        }

        // An oracle for the weights themselves rather than for the integral: at
        // alpha = 1 they have to be exactly the trapezoidal rule, which pins down
        // the formula more tightly than agreement with I^a t^p to 5e-5 does.
        double trapezoid = 0.5 * (yv[0] + yv[Steps]);
        for (int i = 1; i < Steps; ++i)
        {
            trapezoid += yv[i];
        }

        trapezoid *= Dt;

        CheckClose(new RiemannLiouvilleWeights(1.0, Steps).Integrate<double>(yv, Dt), trapezoid, 1e-10);

        // steps = 0 would underflow (steps - 1) into an out-of-bounds index, and a
        // grid that does not match the weights is the mistake a caller reusing a
        // cached object will make
        Assert.Throws<ArgumentException>(() => new RiemannLiouvilleWeights(0.6, 0));
        Assert.Throws<ArgumentException>(() => new RiemannLiouvilleWeights(-1e-8, Steps));
        Assert.Throws<ArgumentException>(() => new RiemannLiouvilleWeights(0.6, Steps - 1).Integrate<double>(yv, Dt));
        Assert.Throws<ArgumentException>(() => new RiemannLiouvilleWeights(0.6, Steps).Integrate<double>(yv, 0.0));
    }

    [Fact]
    public void TestWeightCacheInvalidation()
    {
        // Testing that the rough Heston engine drops cached quadrature weights when the Hurst exponent moves...

        // QuantLib moves the model's parameters under an engine, whose update() drops its caches; here a model with
        // other parameters is a new model, priced by an engine of its own (the S-hes plan's HR4 (i))
        YieldTermStructure rTS = new FlatForward(Today, 0.03, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.01, Dc);

        const double V0 = 0.04, Kappa = 1.5, Theta = 0.04, Sigma = 0.3, Rho = -0.7;

        RoughHestonModel MakeModel(double hurst) =>
            new(new HestonProcess(rTS, qTS, 100.0, V0, Kappa, Theta, Sigma, Rho), hurst);

        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        const double T = 1.0;

        foreach (AnalyticRoughHestonEngineApproximation approximation in new[]
                 {
                     AnalyticRoughHestonEngineApproximation.AdamsPredictorCorrector,
                     AnalyticRoughHestonEngineApproximation.Pade,
                 })
        {
            RoughHestonModel model = MakeModel(0.1);
            var engine = new AnalyticRoughHestonEngine(model, 128, 64, approximation);

            double firstPrice = engine.PriceVanillaPayoff(payoff, T);

            // The fractional weights are keyed on 1 - (H + 1/2); moving H has to
            // rebuild them, or the engine keeps pricing the old model.
            Vector parameters = model.Params();
            parameters[5] = 0.3;
            var moved = (RoughHestonModel)model.WithParams(parameters);

            double movedPrice = new AnalyticRoughHestonEngine(moved, 128, 64, approximation).PriceVanillaPayoff(payoff, T);

            double referencePrice = new AnalyticRoughHestonEngine(MakeModel(0.3), 128, 64, approximation).PriceVanillaPayoff(payoff, T);

            Assert.True(
                Math.Abs(movedPrice - referencePrice) <= 1e-12,
                $"stale quadrature weights after a change of the Hurst exponent: {movedPrice} against {referencePrice}");

            Assert.True(
                Math.Abs(movedPrice - firstPrice) >= 1e-6,
                $"the Hurst exponent did not move the price: {firstPrice} and {movedPrice}");
        }
    }

    [Fact]
    public void TestEquivalenceWithHestonModel()
    {
        // Testing rough Heston engine against the classical Heston engine for H = 0.5...

        YieldTermStructure rTS = new FlatForward(Today, 0.05, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.02, Dc);

        // three parameter sets: the original strongly-correlated case, one
        // with near-zero correlation, and one with a small vol-of-vol
        (double V0, double Kappa, double Theta, double Sigma, double Rho)[] paramSets =
        [
            (0.05, 1.5, 0.04, 0.5, -0.75),
            (0.05, 1.5, 0.04, 0.5, 0.02),
            (0.05, 1.5, 0.04, 0.1, -0.75),
        ];

        foreach ((double v0, double kappa, double theta, double sigma, double rho) in paramSets)
        {
            var roughModel = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, v0, kappa, theta, sigma, rho), 0.5);
            var roughEngine = new AnalyticRoughHestonEngine(roughModel, 128, 512);

            var hestonModel = new HestonModel(new HestonProcess(rTS, qTS, 100.0, v0, kappa, theta, sigma, rho));
            var hestonEngine = new AnalyticHestonEngine(hestonModel, 192);

            // characteristic function comparison along the integration contour
            foreach (double u in new[] { 0.5, 1.0, 5.0, 10.0, 25.0, 50.0 })
            {
                foreach (double t in new[] { 0.5, 2.0 })
                {
                    var z = new Complex(u, -0.5);
                    Complex calculated = roughEngine.ChF(z, t);
                    Complex expected = hestonEngine.ChF(z, t);

                    const double Tol = 1e-5;
                    Assert.True(
                        Complex.Abs(calculated - expected) <= Tol,
                        $"failed to reproduce Heston characteristic function for H = 0.5: rho {rho}, sigma {sigma}, u {u}, t {t}, calculated {calculated}, expected {expected}");
                }
            }

            // price comparison through the full pricing-engine interface
            foreach (OptionType optionType in new[] { OptionType.Call, OptionType.Put })
            {
                foreach (double strike in new[] { 80.0, 90.0, 100.0, 110.0, 125.0 })
                {
                    foreach (int months in new[] { 6, 12, 24 })
                    {
                        Date maturity = Today + new Period(months, TimeUnit.Months);

                        var option = new VanillaOption(new PlainVanillaPayoff(optionType, strike), new EuropeanExercise(maturity));

                        double calculated = roughEngine.Calculate(option, Context).NPV!.Value;
                        double expected = hestonEngine.Calculate(option, Context).NPV!.Value;

                        const double Tol = 5e-4;
                        Assert.True(
                            Math.Abs(calculated - expected) <= Tol,
                            $"failed to reproduce Heston price for H = 0.5: rho {rho}, sigma {sigma}, {optionType} {strike} {maturity}, calculated {calculated}, expected {expected}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestIntegrationAlgorithmConsistency()
    {
        // Testing consistency of rough Heston prices across Fourier integration algorithms...

        YieldTermStructure rTS = new FlatForward(Today, 0.03, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.0, Dc);

        var model = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, -0.7), 0.1);

        const int TimeSteps = 256;
        var laguerreEngine = new AnalyticRoughHestonEngine(model, 128, TimeSteps);
        var lobattoEngine = new AnalyticRoughHestonEngine(model, FourierIntegration.GaussLobatto(1e-10, 1e-10, 100000), TimeSteps);

        foreach (double strike in new[] { 75.0, 100.0, 130.0 })
        {
            foreach (int months in new[] { 3, 12 })
            {
                Date maturity = Today + new Period(months, TimeUnit.Months);

                var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturity));

                double laguerreNPV = laguerreEngine.Calculate(option, Context).NPV!.Value;
                double lobattoNPV = lobattoEngine.Calculate(option, Context).NPV!.Value;

                const double Tol = 1e-6;
                Assert.True(
                    Math.Abs(laguerreNPV - lobattoNPV) <= Tol,
                    $"Gauss-Laguerre and Gauss-Lobatto rough Heston prices differ: strike {strike}, maturity {maturity}, {laguerreNPV} against {lobattoNPV}");
            }
        }
    }

    [Fact]
    public void TestKnownReferenceValues()
    {
        // Testing rough Heston prices against an independent reference implementation...

        YieldTermStructure rTS = new FlatForward(Today, 0.03, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.0, Dc);

        var model = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, -0.7), 0.1);
        var engine = new AnalyticRoughHestonEngine(model, 128, 512);

        // reference prices from a from-scratch NumPy/SciPy reimplementation,
        // independent of this engine's C++ code, converged to ~1e-4
        (double Maturity, double Strike, double Price)[] references =
        [
            (0.25, 75.0, 25.762400),
            (0.25, 100.0, 3.793313),
            (0.25, 130.0, 0.001586),
            (1.00, 75.0, 28.394103),
            (1.00, 100.0, 8.336634),
            (1.00, 130.0, 0.202295),
            (2.00, 75.0, 31.520600),
            (2.00, 100.0, 12.722717),
            (2.00, 130.0, 1.285820),
        ];

        foreach ((double maturity, double strike, double price) in references)
        {
            double calculated = engine.PriceVanillaPayoff(new PlainVanillaPayoff(OptionType.Call, strike), maturity);

            const double Tol = 5e-4;
            Assert.True(
                Math.Abs(calculated - price) <= Tol,
                $"failed to reproduce independent reference price: strike {strike}, maturity {maturity}, calculated {calculated}, expected {price}");
        }
    }

    [Fact]
    public void TestPutCallParity()
    {
        // Testing put-call parity for the rough Heston engine...

        YieldTermStructure rTS = new FlatForward(Today, 0.03, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.01, Dc);

        foreach (double hurst in new[] { 0.1, 0.3, 0.5 })
        {
            var model = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, -0.7), hurst);
            var engine = new AnalyticRoughHestonEngine(model, 128, 256);

            foreach (double strike in new[] { 70.0, 100.0, 130.0 })
            {
                foreach (double t in new[] { 0.1, 1.0, 2.0 })
                {
                    double callPrice = engine.PriceVanillaPayoff(new PlainVanillaPayoff(OptionType.Call, strike), t);
                    double putPrice = engine.PriceVanillaPayoff(new PlainVanillaPayoff(OptionType.Put, strike), t);

                    double fwd = 100.0 * qTS.Discount(t) / rTS.Discount(t);

                    double expected = (fwd - strike) * rTS.Discount(t);

                    const double Tol = 1e-6;
                    Assert.True(
                        Math.Abs(callPrice - putPrice - expected) <= Tol,
                        $"failed put-call parity: hurst {hurst}, strike {strike}, maturity {t}, call - put {callPrice - putPrice}, expected {expected}");
                }
            }
        }
    }

    [Fact]
    public void TestMonotonicityAndBounds()
    {
        // Testing rough Heston call-price monotonicity, convexity and no-arbitrage bounds...

        YieldTermStructure rTS = new FlatForward(Today, 0.03, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.01, Dc);

        var model = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, -0.7), 0.15);
        var engine = new AnalyticRoughHestonEngine(model, 128, 256);

        CheckMonotoneConvexBounded(engine, rTS, qTS, [60.0, 70.0, 80.0, 90.0, 100.0, 110.0, 120.0, 130.0, 140.0]);
    }

    [Fact]
    public void TestInputValidation()
    {
        // Testing input validation of the rough Heston engine...

        YieldTermStructure rTS = new FlatForward(Today, 0.03, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.0, Dc);

        var model = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, -0.7), 0.15);

        const string NoTimeSteps = "at least one time step required";
        const string AlphaOutOfRange = "must be in (-1, 0)";
        const string NonPositiveMaturity = "maturity must be positive";

        Assert.Contains(NoTimeSteps, Assert.Throws<ArgumentException>(() => new AnalyticRoughHestonEngine(model, 128, 0)).Message);

        FourierIntegration integration = FourierIntegration.GaussLaguerre(128);
        Assert.Contains(NoTimeSteps, Assert.Throws<ArgumentException>(() => new AnalyticRoughHestonEngine(model, integration, 0)).Message);
        Assert.Contains(
            AlphaOutOfRange, Assert.Throws<ArgumentException>(() => new AnalyticRoughHestonEngine(model, integration, 256, 1e-25, 0.0)).Message);
        Assert.Contains(
            AlphaOutOfRange, Assert.Throws<ArgumentException>(() => new AnalyticRoughHestonEngine(model, integration, 256, 1e-25, -1.0)).Message);
        Assert.Contains(
            AlphaOutOfRange, Assert.Throws<ArgumentException>(() => new AnalyticRoughHestonEngine(model, integration, 256, 1e-25, 0.5)).Message);

        var engine = new AnalyticRoughHestonEngine(model, 128, 256);

        Assert.Contains(
            NonPositiveMaturity,
            Assert.Throws<ArgumentException>(() => engine.PriceVanillaPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0), 0.0)).Message);
        Assert.Contains(
            NonPositiveMaturity,
            Assert.Throws<ArgumentException>(() => engine.PriceVanillaPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0), -1.0)).Message);

        var americanOption = new VanillaOption(
            new PlainVanillaPayoff(OptionType.Call, 100.0), new AmericanExercise(Today, Today + new Period(1, TimeUnit.Years)));
        Assert.Contains(
            "not an European option", Assert.Throws<NanoQLException>(() => engine.Calculate(americanOption, Context)).Message);
    }

    [Fact]
    public void TestShortMaturitySkewExplosion()
    {
        // Testing rough Heston short-maturity skew explosion...

        YieldTermStructure rTS = new FlatForward(Today, 0.0, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.0, Dc);

        // at-the-money implied volatility skew d(sigma_iv)/d(ln K)
        static double AtmSkew(AnalyticRoughHestonEngine engine, double t)
        {
            const double Dk = 0.02;

            double[] iv = new double[2];
            for (int i = 0; i < 2; ++i)
            {
                double strike = 100.0 * Math.Exp(i == 0 ? -Dk : Dk);
                double price = engine.PriceVanillaPayoff(new PlainVanillaPayoff(OptionType.Call, strike), t);
                iv[i] = BlackFormulaFunctions.BlackFormulaImpliedStdDev(OptionType.Call, strike, 100.0, price) / Math.Sqrt(t);
            }

            return (iv[1] - iv[0]) / (2 * Dk);
        }

        const double T1 = 0.1, T2 = 0.4;

        // classical Heston (H = 0.5) reference for the "materially less
        // explosion" comparison below
        var flatModel = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, -0.7), 0.5);
        var flatEngine = new AnalyticRoughHestonEngine(flatModel, 128, 512);
        double flatSkewRatio = AtmSkew(flatEngine, T1) / AtmSkew(flatEngine, T2);

        // El Euch, Fukasawa, Rosenbaum: the ATM skew of rough volatility
        // models explodes like T^(H - 1/2) for T -> 0
        foreach (double hurst in new[] { 0.05, 0.1, 0.3 })
        {
            var model = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, -0.7), hurst);
            var engine = new AnalyticRoughHestonEngine(model, 128, 512);

            double skewRatio = AtmSkew(engine, T1) / AtmSkew(engine, T2);

            double expected = Math.Pow(T1 / T2, hurst - 0.5);

            double tol = 0.15 * expected;
            Assert.True(
                Math.Abs(skewRatio - expected) <= tol,
                $"failed to reproduce short-maturity skew power law: hurst {hurst}, skew ratio {skewRatio}, expected {expected}");

            // classical Heston must show materially less short-maturity
            // skew steepening than any rough case
            Assert.True(
                flatSkewRatio <= 0.5 * (skewRatio + 1.0),
                $"classical limit shows unexpected skew explosion: hurst {hurst}, rough {skewRatio}, classical {flatSkewRatio}");
        }
    }

    [Fact]
    public void TestCalibration()
    {
        // Testing rough Heston calibration to a synthetic surface...

        CheckCalibration(AnalyticRoughHestonEngineApproximation.AdamsPredictorCorrector, 20);
    }

    [Fact]
    public void TestPadeRiccatiAsymptotics()
    {
        // Testing short- and long-time asymptotics of the Pade Riccati solution...

        YieldTermStructure rTS = new FlatForward(Today, 0.0, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.0, Dc);

        const double Kappa = 0.3, Sigma = 0.4, Rho = -0.7;

        foreach (double hurst in new[] { 0.1, 0.3 })
        {
            double a = hurst + 0.5;
            var model = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, Kappa, 0.04, Sigma, Rho), hurst);
            var engine = new AnalyticRoughHestonEngine(model, 128, 256, AnalyticRoughHestonEngineApproximation.Pade);

            foreach (double u in new[] { 0.5, 2.0, 5.0 })
            {
                var z = new Complex(u, -0.5);
                var i = new Complex(0.0, 1.0);

                // short-time leading term h(z, t) -> b_1 t^a with
                // b_1 = -z * (z + i) / (2 * \Gamma(1 + a)); check the ratio h / t^a
                Complex b1 = -0.5 * z * (z + i) / new GammaFunction().Value(1.0 + a);
                const double TShort = 1e-7;
                Complex hShort = engine.RiccatiSolution(z, TShort);
                Complex ratio = hShort / Math.Pow(TShort, a);

                const double ShortTol = 1e-3;
                Assert.True(
                    Complex.Abs(ratio - b1) / Complex.Abs(b1) <= ShortTol,
                    $"Pade Riccati short-time limit off: u {u}, hurst {hurst}, h / t^a {ratio}, b_1 {b1}");

                // long-time limit h(z, t) -> r_- / \sigma, the attracting root
                // of the Riccati quadratic
                Complex w = (Kappa / Sigma) - (i * (Rho * z));
                Complex aRoot = Complex.Sqrt((z * (z + i)) + (w * w));
                Complex g0 = (w - aRoot) / Sigma;

                const double TLong = 1e8;
                Complex hLong = engine.RiccatiSolution(z, TLong);

                const double LongTol = 1e-3;
                Assert.True(
                    Complex.Abs(hLong - g0) / Complex.Abs(g0) <= LongTol,
                    $"Pade Riccati long-time limit off: u {u}, hurst {hurst}, h(inf) {hLong}, r_-/sigma {g0}");
            }
        }
    }

    [Fact]
    public void TestPadeTrivialContour()
    {
        // Testing the Pade Riccati solution against the exact solution on the contour where the Riccati equation
        // degenerates...

        YieldTermStructure rTS = new FlatForward(Today, 0.0, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.0, Dc);

        // at z = 0 or z = -i, the Riccati forcing term c0 = -z * (z + i) / 2 vanishes
        // identically, so h' = c_1 * h + c_2 * h^2 with h(0) = 0 has the exact,
        // Pade-construction-independent solution h ≡ 0. This checks the (3, 3)
        // approximation against ground truth derived directly from the ODE.
        foreach (double hurst in new[] { 0.05, 0.2, 0.4 })
        {
            var model = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, -0.7), hurst);
            var engine = new AnalyticRoughHestonEngine(model, 128, 256, AnalyticRoughHestonEngineApproximation.Pade);

            foreach (Complex z in new[] { new Complex(0.0, 0.0), new Complex(0.0, -1.0) })
            {
                foreach (double t in new[] { 0.01, 1.0, 5.0 })
                {
                    Complex h = engine.RiccatiSolution(z, t);

                    const double Tol = 1e-10;
                    Assert.True(
                        Complex.Abs(h) <= Tol,
                        $"Pade Riccati solution is not exactly zero on the degenerate contour: hurst {hurst}, z {z}, maturity {t}, h {h}");
                }
            }
        }
    }

    [Fact]
    public void TestPadeAgainstAdamsRiccati()
    {
        // Testing the Pade Riccati solution against the Adams reference...

        CheckRiccatiAgainstAdams(AnalyticRoughHestonEngineApproximation.Pade, [256], 2e-2, 0.0);
    }

    [Fact]
    public void TestPadeAgainstAdamsPricing()
    {
        // Testing Pade rough Heston prices against the Adams reference...

        YieldTermStructure rTS = new FlatForward(Today, 0.03, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.01, Dc);

        foreach (double hurst in new[] { 0.05, 0.1, 0.2 })
        {
            foreach (double rho in new[] { -0.7, 0.3 })
            {
                var model = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, rho), hurst);
                var adams = new AnalyticRoughHestonEngine(model, 128, 512, AnalyticRoughHestonEngineApproximation.AdamsPredictorCorrector);
                var pade = new AnalyticRoughHestonEngine(model, 128, 256, AnalyticRoughHestonEngineApproximation.Pade);

                foreach (OptionType optionType in new[] { OptionType.Call, OptionType.Put })
                {
                    foreach (double strike in new[] { 80.0, 90.0, 100.0, 110.0, 120.0 })
                    {
                        foreach (double t in new[] { 0.25, 1.0, 2.0 })
                        {
                            var payoff = new PlainVanillaPayoff(optionType, strike);
                            double reference = adams.PriceVanillaPayoff(payoff, t);
                            double approx = pade.PriceVanillaPayoff(payoff, t);

                            // a few basis points of the spot on prices of order
                            // 1 to 30 for the strongly negative correlation the
                            // approximation is designed for (rho = -0.7); the
                            // wider tolerance accommodates the observed ~4x
                            // larger error at rho = 0.3, still a few percent of
                            // the spot, not a sign of a wrong root being picked
                            const double Tol = 5e-2;
                            Assert.True(
                                Math.Abs(approx - reference) <= Tol,
                                $"Pade price deviates from the Adams reference: hurst {hurst}, rho {rho}, {optionType} {strike} {t}, Pade {approx}, Adams {reference}");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestPadeHestonLimit()
    {
        // Testing the Pade rough Heston engine against classical Heston for H = 0.5...

        YieldTermStructure rTS = new FlatForward(Today, 0.05, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.02, Dc);

        const double V0 = 0.05, Kappa = 1.5, Theta = 0.04, Sigma = 0.5, Rho = -0.75;

        var roughModel = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, V0, Kappa, Theta, Sigma, Rho), 0.5);
        var padeEngine = new AnalyticRoughHestonEngine(roughModel, 128, 256, AnalyticRoughHestonEngineApproximation.Pade);

        var hestonModel = new HestonModel(new HestonProcess(rTS, qTS, 100.0, V0, Kappa, Theta, Sigma, Rho));
        var hestonEngine = new AnalyticHestonEngine(hestonModel, 192);

        // the (3, 3) rational approximation is built for the rough regime and
        // degrades towards the classical limit
        foreach (OptionType optionType in new[] { OptionType.Call, OptionType.Put })
        {
            foreach (double strike in new[] { 80.0, 90.0, 100.0, 110.0, 125.0 })
            {
                foreach (int months in new[] { 6, 12, 24 })
                {
                    Date maturity = Today + new Period(months, TimeUnit.Months);

                    var option = new VanillaOption(new PlainVanillaPayoff(optionType, strike), new EuropeanExercise(maturity));

                    double calculated = padeEngine.Calculate(option, Context).NPV!.Value;
                    double expected = hestonEngine.Calculate(option, Context).NPV!.Value;

                    double tol = Math.Max(0.05, 0.04 * expected);
                    Assert.True(
                        Math.Abs(calculated - expected) <= tol,
                        $"Pade limit fails to match classical Heston: {optionType} {strike} {maturity}, Pade {calculated}, Heston {expected}");
                }
            }
        }
    }

    [Fact]
    public void TestPadeMonotonicityAndBounds()
    {
        // Testing no-arbitrage bounds of the Pade engine...

        YieldTermStructure rTS = new FlatForward(Today, 0.03, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.01, Dc);

        var model = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, -0.7), 0.1);
        var engine = new AnalyticRoughHestonEngine(model, 128, 256, AnalyticRoughHestonEngineApproximation.Pade);

        // call prices stay monotone, convex and within the no-arbitrage box
        CheckMonotoneConvexBounded(engine, rTS, qTS, [70.0, 80.0, 90.0, 100.0, 110.0, 120.0, 130.0]);
    }

    [Fact]
    public void TestPadeCalibration()
    {
        // Testing rough Heston calibration through the fast Pade engine...

        // the synthetic surface is generated with the same Pade engine, so the
        // round trip isolates the optimizer's ability to recover parameters
        // from the fast approximation itself
        CheckCalibration(AnalyticRoughHestonEngineApproximation.Pade, 20);
    }

    [Fact]
    public void TestFractionalKernelApproximation()
    {
        // Testing the sum-of-exponentials approximation of the fractional kernel...

        const double TMax = 1.0;

        // Relative L1 error on (0, tMax]. K is integrable at the origin, so
        // this needs no cut-off and is free of any time grid
        static double L1Error(FractionalKernelApproximation kernel, double alpha)
        {
            const int M = 6, Q = 4000;
            double error = 0.0;
            for (int j = 0; j < Q; ++j)
            {
                double u = (j + 0.5) / Q;

                // t = tMax u^m concentrates the nodes near the singularity
                double t = TMax * Math.Pow(u, M);
                double weight = TMax * M * Math.Pow(u, M - 1) / Q;
                double exact = Math.Pow(t, alpha - 1.0) / new GammaFunction().Value(alpha);
                error += weight * Math.Abs(kernel.Value(t) - exact);
            }

            return error / (Math.Pow(TMax, alpha) / new GammaFunction().Value(1.0 + alpha));
        }

        foreach (double hurst in new[] { 0.05, 0.1, 0.3 })
        {
            double alpha = hurst + 0.5;
            double previousError = double.MaxValue;

            foreach (int n in new[] { 10, 20, 40, 80 })
            {
                var kernel = new FractionalKernelApproximation(alpha, n, TMax);

                Assert.Equal(n, kernel.Size);

                // Non-negativity is what keeps K^n completely monotone, and
                // with it the lifted model well posed
                for (int i = 0; i < n; ++i)
                {
                    Assert.False(kernel.Weights[i] < 0.0 || kernel.Rates[i] < 0.0, $"negative kernel node: hurst {hurst}, n {n}, i {i}");
                    Assert.False(i > 0 && kernel.Rates[i] <= kernel.Rates[i - 1], $"kernel rates not increasing: hurst {hurst}, n {n}, i {i}");
                }

                double error = L1Error(kernel, alpha);

                // The measured rate is n^(-1.55), a factor of 2.9 per
                // doubling; 1.7 leaves room at the coarse end
                double expected = previousError / 1.7;
                Assert.True(error <= expected, $"kernel approximation not converging: hurst {hurst}, n {n}, error {error}, expected below {expected}");

                previousError = error;
            }
        }

        // At alpha = 1 the measure is a point mass at the origin, which the
        // leading interval absorbs entirely: the construction collapses to the
        // single node (1, 0) and reproduces K exactly, with no special-casing
        var classical = new FractionalKernelApproximation(1.0, 20, TMax);
        const double Tol = 10 * 2.220446049250313e-16; // 10 QL_EPSILON
        Assert.True(Math.Abs(classical.Weights[0] - 1.0) <= Tol && classical.Rates[0] == 0.0, "alpha = 1 doesn't collapse to the node (1, 0)");
        for (int i = 1; i < classical.Size; ++i)
        {
            Assert.True(Math.Abs(classical.Weights[i]) <= Tol, $"alpha = 1 leaves weight on node {i}");
        }

        foreach (double t in new[] { 0.1, 0.5, 1.0 })
        {
            Assert.True(Math.Abs(classical.Value(t) - 1.0) <= Tol, $"alpha = 1 kernel isn't 1 at {t}");
        }
    }

    [Fact]
    public void TestLiftedAgainstAdamsRiccati()
    {
        // Testing convergence of the lifted Riccati solution to the Adams reference...

        // Unlike Pade, the lifted route has a convergence parameter, so what
        // is asserted is not closeness at a fixed n but that the error keeps
        // falling as n grows; a factor of 16 in n at the measured rate n^(-1.5)
        // is about 64, and 6 is a wide margin on the worst contour point
        CheckRiccatiAgainstAdams(AnalyticRoughHestonEngineApproximation.Lifted, [10, 40, 160], 5e-3, 6.0);
    }

    [Fact]
    public void TestLiftedHestonLimit()
    {
        // Testing the lifted rough Heston engine against classical Heston for H = 0.5...

        // At H = 0.5 the kernel is constant, the surviving factor has x = 0
        // and the integrator degenerates to the trapezoidal rule, so the
        // lifted route is not an approximation here at all - hence a much
        // tighter tolerance than the Pade equivalent
        YieldTermStructure rTS = new FlatForward(Today, 0.05, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.02, Dc);

        const double V0 = 0.05, Kappa = 1.5, Theta = 0.04, Sigma = 0.5, Rho = -0.75;

        var roughModel = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, V0, Kappa, Theta, Sigma, Rho), 0.5);
        var lifted = new AnalyticRoughHestonEngine(roughModel, 128, 256, AnalyticRoughHestonEngineApproximation.Lifted);

        var hestonModel = new HestonModel(new HestonProcess(rTS, qTS, 100.0, V0, Kappa, Theta, Sigma, Rho));
        var classical = new AnalyticHestonEngine(hestonModel, 192);

        // Calls only: puts differ by an exact constant in priceVanillaPayoff
        foreach (double strike in new[] { 80.0, 90.0, 100.0, 110.0, 125.0 })
        {
            foreach (int months in new[] { 6, 12, 24 })
            {
                Date maturity = Today + new Period(months, TimeUnit.Months);
                var payoff = new PlainVanillaPayoff(OptionType.Call, strike);

                var option = new VanillaOption(payoff, new EuropeanExercise(maturity));
                double reference = classical.Calculate(option, Context).NPV!.Value;
                double approx = lifted.PriceVanillaPayoff(payoff, maturity);

                const double Tol = 5e-4;
                Assert.True(
                    Math.Abs(approx - reference) <= Tol,
                    $"lifted limit fails to match classical Heston: strike {strike}, maturity {maturity}, lifted {approx}, Heston {reference}");
            }
        }
    }

    [Fact]
    public void TestLiftedAgainstAdamsPricing()
    {
        // Testing convergence of lifted rough Heston prices to the Adams reference...

        YieldTermStructure rTS = new FlatForward(Today, 0.03, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.01, Dc);

        foreach (double hurst in new[] { 0.05, 0.1, 0.2 })
        {
            foreach (double rho in new[] { -0.7, 0.3 })
            {
                var model = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, rho), hurst);
                var adams = new AnalyticRoughHestonEngine(model, 128, 1024, AnalyticRoughHestonEngineApproximation.AdamsPredictorCorrector);

                double previousError = double.MaxValue;
                foreach (int n in new[] { 10, 40, 160 })
                {
                    var lifted = new AnalyticRoughHestonEngine(model, 128, 512, AnalyticRoughHestonEngineApproximation.Lifted, n);

                    double maxError = 0.0;
                    foreach (double strike in new[] { 80.0, 90.0, 100.0, 110.0, 120.0 })
                    {
                        foreach (double t in new[] { 0.25, 1.0, 2.0 })
                        {
                            var payoff = new PlainVanillaPayoff(OptionType.Call, strike);
                            maxError = Math.Max(maxError, Math.Abs(lifted.PriceVanillaPayoff(payoff, t) - adams.PriceVanillaPayoff(payoff, t)));
                        }
                    }

                    double expected = previousError / 4.0;
                    Assert.True(
                        maxError <= expected,
                        $"lifted prices not converging to the Adams reference: hurst {hurst}, rho {rho}, n {n}, error {maxError}, expected below {expected}");

                    previousError = maxError;
                }

                // On prices of order 1 to 30; the bound is set by the roughest
                // kernel (H = 0.05) at the longest maturity
                const double Tol = 3e-3;
                Assert.True(
                    previousError <= Tol,
                    $"lifted prices deviate from the Adams reference: hurst {hurst}, rho {rho}, error {previousError}");
            }
        }
    }

    [Fact]
    public void TestLiftedCalibration()
    {
        // Testing rough Heston calibration through the lifted engine...

        // The kernel nodes depend on the Hurst exponent, so this is what
        // catches update() failing to invalidate the per-maturity grid cache
        CheckCalibration(AnalyticRoughHestonEngineApproximation.Lifted, 20);
    }

    // QL_CHECK_CLOSE, Boost's BOOST_CHECK_CLOSE: |a - b| within the tolerance, in percent, of both values.
    private static void CheckClose(double a, double b, double tolerancePercent)
    {
        double diff = Math.Abs(a - b);
        Assert.True(
            diff <= tolerancePercent / 100.0 * Math.Abs(a) && diff <= tolerancePercent / 100.0 * Math.Abs(b),
            $"difference between {a} and {b} exceeds {tolerancePercent}%");
    }

    // The one-year calls' no-arbitrage box, their monotonicity and convexity in the strike, shared by QuantLib's
    // Adams and Pade cases
    private static void CheckMonotoneConvexBounded(
        AnalyticRoughHestonEngine engine, YieldTermStructure rTS, YieldTermStructure qTS, double[] strikes)
    {
        const double T = 1.0;
        double fwd = 100.0 * qTS.Discount(T) / rTS.Discount(T);
        double df = rTS.Discount(T);

        double[] prices = [.. strikes.Select(k => engine.PriceVanillaPayoff(new PlainVanillaPayoff(OptionType.Call, k), T))];

        for (int i = 0; i < strikes.Length; ++i)
        {
            double lower = Math.Max(fwd - strikes[i], 0.0) * df;
            double upper = fwd * df;

            Assert.False(
                prices[i] < lower - 1e-8 || prices[i] > upper + 1e-8,
                $"call price violates no-arbitrage bounds: strike {strikes[i]}, price {prices[i]}, lower {lower}, upper {upper}");

            Assert.False(
                i > 0 && prices[i] > prices[i - 1] + 1e-8,
                $"call price is not monotonically decreasing in strike: {strikes[Math.Max(i - 1, 0)]} -> {strikes[i]}");

            if (i > 0 && i + 1 < strikes.Length)
            {
                double secondDiff = prices[i + 1] - (2 * prices[i]) + prices[i - 1];
                Assert.False(secondDiff < -1e-6, $"call price is not convex in strike: strike {strikes[i]}, second difference {secondDiff}");
            }
        }
    }

    // QuantLib's Pade and lifted Riccati cases against the Adams reference at 1024 steps: the maximal relative error on
    // the pricing contour; each factor count's error below the last one's over the shrink factor where it is given,
    // and the last one's within the tolerance
    private static void CheckRiccatiAgainstAdams(
        AnalyticRoughHestonEngineApproximation approximation, int[] nFactors, double tol, double shrink)
    {
        YieldTermStructure rTS = new FlatForward(Today, 0.0, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.0, Dc);

        // The rational approximation is sharpest for small Hurst exponents,
        // the empirically relevant regime; rho is swept through both signs
        // since the long-time coefficients hinge on a root selection
        // (w -/+ A) that could plausibly flip sign for some (kappa, sigma,
        // rho) combinations
        foreach (double hurst in new[] { 0.05, 0.1, 0.2 })
        {
            foreach (double rho in new[] { -0.7, 0.3 })
            {
                var model = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, rho), hurst);
                var adams = new AnalyticRoughHestonEngine(model, 128, 1024, AnalyticRoughHestonEngineApproximation.AdamsPredictorCorrector);

                double previousError = double.MaxValue;
                foreach (int n in nFactors)
                {
                    AnalyticRoughHestonEngine approx = approximation == AnalyticRoughHestonEngineApproximation.Pade
                        ? new AnalyticRoughHestonEngine(model, 128, n, approximation)
                        : new AnalyticRoughHestonEngine(model, 128, 512, approximation, n);

                    double maxError = 0.0;

                    // pricing contour z = u - i/2 over the calibration-relevant band
                    foreach (double u in new[] { 0.5, 1.0, 2.0, 5.0, 10.0, 20.0 })
                    {
                        foreach (double t in new[] { 0.25, 1.0, 2.0 })
                        {
                            var z = new Complex(u, -0.5);
                            Complex reference = adams.RiccatiSolution(z, t);
                            Complex value = approx.RiccatiSolution(z, t);
                            maxError = Math.Max(maxError, Complex.Abs(value - reference) / Complex.Abs(reference));
                        }
                    }

                    if (shrink > 0.0)
                    {
                        double expected = previousError / shrink;
                        Assert.True(
                            maxError <= expected,
                            $"{approximation} Riccati solution not converging to the Adams reference: hurst {hurst}, rho {rho}, n {n}, error {maxError}, expected below {expected}");
                    }

                    previousError = maxError;
                }

                Assert.True(
                    previousError <= tol,
                    $"{approximation} Riccati solution deviates from the Adams reference: hurst {hurst}, rho {rho}, max rel. error {previousError}");
            }
        }
    }

    // QuantLib's three calibration cases: a synthetic surface from known parameters through the route's engine, the
    // model calibrated from deliberately wrong ones, each evaluation's helpers priced through one engine on the model
    // the calibration hands them, as QuantLib's engine serves them (the S-hes plan's HR4 (i))
    private static void CheckCalibration(AnalyticRoughHestonEngineApproximation approximation, int nFactors)
    {
        Calendar calendar = new NullCalendar();

        YieldTermStructure rTS = new FlatForward(Today, 0.02, Dc);
        YieldTermStructure qTS = new FlatForward(Today, 0.0, Dc);

        const double V0 = 0.0225, Kappa = 0.6, Theta = 0.04, Sigma = 0.35, Rho = -0.65, Hurst = 0.12;

        // the time steps must keep the fractional Adams scheme stable up to
        // frequencies where the characteristic function has decayed (u ~ 0.35 N/T)
        const int IntegrationOrder = 64, TimeSteps = 256;

        // synthetic market surface generated from known parameters
        var trueModel = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, V0, Kappa, Theta, Sigma, Rho), Hurst);
        var trueEngine = new AnalyticRoughHestonEngine(trueModel, IntegrationOrder, TimeSteps, approximation, nFactors);

        var engines = new ConditionalWeakTable<RoughHestonModel, AnalyticRoughHestonEngine>();
        var helpers = new List<HestonModelHelper>();
        foreach (int months in new[] { 3, 6, 12, 24 })
        {
            var maturity = new Period(months, TimeUnit.Months);
            Date maturityDate = Today + maturity;
            double t = Dc.YearFraction(Today, maturityDate);

            double fwd = 100.0 * qTS.Discount(maturityDate) / rTS.Discount(maturityDate);
            double df = rTS.Discount(maturityDate);

            foreach (double strike in new[] { 80.0, 90.0, 100.0, 110.0, 120.0 })
            {
                double price = trueEngine.PriceVanillaPayoff(new PlainVanillaPayoff(OptionType.Call, strike), maturityDate);
                double impliedVol = BlackFormulaFunctions.BlackFormulaImpliedStdDev(OptionType.Call, strike, fwd, price, df) / Math.Sqrt(t);

                // implied-vol errors keep the objective well scaled; relative
                // price errors can strand the optimizer in local minima
                helpers.Add(new HestonModelHelper(
                        maturity, calendar, 100.0, strike, impliedVol, rTS, qTS, Context,
                        BlackCalibrationHelperCalibrationErrorType.ImpliedVolError)
                    .SetPricingEngine<RoughHestonModel>((option, c, m) => engines.GetValue(
                        m, mm => new AnalyticRoughHestonEngine(mm, IntegrationOrder, TimeSteps, approximation, nFactors)).Calculate(option, c)));
            }
        }

        // calibration starts from deliberately wrong parameters
        var model = new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 1.0, 0.02, 0.25, -0.4), 0.2);

        var om = new LevenbergMarquardt(1e-8, 1e-8, 1e-8);
        RoughHestonModel calibrated = model.Calibrate(helpers, om, new EndCriteria(400, 40, 1.0e-8, 1.0e-8, 1.0e-8), Context).Model;

        double sse = 0.0;
        foreach (HestonModelHelper helper in helpers)
        {
            double error = helper.CalibrationError(calibrated, Context);
            sse += error * error;
        }

        const double ExpectedSse = 1e-8;
        Assert.True(
            sse <= ExpectedSse,
            $"failed to calibrate rough Heston model to a synthetic surface: sse {sse}, v0 {calibrated.V0}, kappa {calibrated.Kappa}, theta {calibrated.Theta}, sigma {calibrated.Sigma}, rho {calibrated.Rho}, hurst {calibrated.Hurst}");

        // the Hurst exponent controls the skew term structure and should be
        // identified well
        const double HurstTol = 0.02;
        Assert.True(Math.Abs(calibrated.Hurst - Hurst) <= HurstTol, $"failed to recover the Hurst exponent: {calibrated.Hurst}");
    }
}
