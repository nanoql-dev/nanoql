// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The integral Heston variance-option engine's put, other payoffs, start date and dividend curve. QuantLib sums any
/// payoff but a call against the integrated variance's density on 4,096 points of spacing √(2π)/64, about one standard
/// deviation of it, so its put case prices 0.0466796 (QuantLib's <c>testIntegralHeston</c>); nanoql prices a put by
/// parity on the call's integral, 0.0465915, and refuses other payoffs, which QuantLib's grid prices 13% to 24% off.
/// Checks: an independent price, the integrated CIR variance's characteristic function (the CIR bond formula at λ =
/// −iu, averaged over the start variance's non-central χ² law) inverted by Gil-Pelaez's distribution function
/// integrated over [0, K], which gives QuantLib's call case to 1e-10. QuantLib's engine measures the variance from the
/// evaluation date whatever the option's start, so a start 180 days ahead prices as one starting today, 0.9104618670
/// (QuantLib 1.44); nanoql prices a forward start on the variance integrated from its start, each integrand's
/// expectation over the start variance's non-central χ² law the CIR transform, and refuses a seasoned option. QuantLib
/// refuses a process whose dividend handle isn't empty; nanoql ignores the dividend curve, which the integrated
/// variance's law doesn't read. Checks: a Monte Carlo of the integrated variance (the exact mean-reverting step with a
/// truncated diffusion and trapezoid integration, four steps a day, 100,000 antithetic pairs on QuantLib's Mersenne
/// twister) within three standard errors, for a call (the one-dimensional integral) and a put (the two-dimensional
/// one); the start tending to the evaluation date.
/// </summary>
public class IntegralHestonVarianceOptionTests
{
    private static readonly Date Today = new(15, Month.May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();

    [Theory]
    [InlineData(OptionType.Call, 0.05)]
    [InlineData(OptionType.Put, 0.35)]
    public void AForwardStartIsTheMonteCarloOfItsIntegratedVariance(OptionType type, double strike)
    {
        // QuantLib's case's process (v0 2, κ 2, θ 0.01, σ 0.1, ρ −0.5, r 0, Actual360): a start in 180 days, a maturity
        // in 540
        Payoff payoff = new PlainVanillaPayoff(type, strike);
        double forward = IntegralHestonVarianceOptionEngine.Calculate(
            new VarianceOption(payoff, 1.0, Today + 180, Today + 540), Context, Process(0.0)).NPV!.Value;
        double today = IntegralHestonVarianceOptionEngine.Calculate(
            new VarianceOption(payoff, 1.0, Today, Today + 540), Context, Process(0.0)).NPV!.Value;
        (double mean, double error) = MonteCarlo(payoff, 180, 540);
        Assert.True(Math.Abs(forward - mean) <= 3 * error, $"{forward} against {mean} ± {error}");
        Assert.True(Math.Abs(today - mean) > 3 * error, $"QuantLib's {today} against {mean} ± {error}");
    }

    [Theory]
    [InlineData(1.5, 0.7, 0, 360, 0.04659152382092219)] // QuantLib's put case; its grid 0.0466796
    [InlineData(2.0, 0.95, 0, 540, 0.011941125997822655)] // its call case's process; its grid 0.0121635
    [InlineData(2.0, 0.35, 180, 540, 0.027124931423930965)] // a forward start; the grid 0.0264296
    public void APutIsItsIndependentPrice(double v0, double strike, int startDays, int maturityDays, double independent)
    {
        // the independent price: the characteristic function inverted (varopt_cf.py, on scipy's adaptive quadrature at
        // 1e-14, its error estimates at most 1e-13)
        var option = new VarianceOption(new PlainVanillaPayoff(OptionType.Put, strike), 1.0, Today + startDays, Today + maturityDays);
        double put = IntegralHestonVarianceOptionEngine.Calculate(option, Context, Process(0.0, v0)).NPV!.Value;
        Assert.Equal(independent, put, 1e-10);
    }

    [Fact]
    public void OtherPayoffsAreRefused()
    {
        // QuantLib's grid prices this cash-or-nothing put at 0.39152 against the independent 0.44858, and at a strike of
        // 0.75 1.00176, above 1 (varopt_digital.py)
        var option = new VarianceOption(new CashOrNothingPayoff(OptionType.Put, 0.65, 1.0), 1.0, Today, Today + 360);
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => IntegralHestonVarianceOptionEngine.Calculate(option, Context, Process(0.0, 1.5)));
        Assert.Equal("this engine prices plain-vanilla calls and puts only", e.Message);
    }

    [Fact]
    public void AStartTendingToTheEvaluationDateTendsToQuantLibsValue()
    {
        // a day's start removes about a day's variance, v0/360
        Payoff payoff = new PlainVanillaPayoff(OptionType.Call, 0.05);
        double Price(int startDays) => IntegralHestonVarianceOptionEngine.Calculate(
            new VarianceOption(payoff, 1.0, Today + startDays, Today + 540), Context, Process(0.0)).NPV!.Value;
        double today = Price(0), oneDay = Price(1), twoDays = Price(2);
        Assert.Equal(0.9104618670, today, 1e-10);
        Assert.True(today > oneDay && oneDay > twoDays);
        Assert.True(today - oneDay < 1.1 * 2.0 / 360.0, $"{today} against {oneDay}");
    }

    [Fact]
    public void TheDividendCurveIsntRead()
    {
        // QuantLib refuses a process whose dividend handle isn't empty
        Payoff payoff = new PlainVanillaPayoff(OptionType.Call, 0.05);
        var option = new VarianceOption(payoff, 1.0, Today, Today + 540);
        Assert.Equal(
            IntegralHestonVarianceOptionEngine.Calculate(option, Context, Process(0.0)).NPV!.Value,
            IntegralHestonVarianceOptionEngine.Calculate(option, Context, Process(0.05)).NPV!.Value);
    }

    // QuantLib's case's process, its dividend yield and its v₀ given
    private static HestonProcess Process(double dividendYield, double v0 = 2.0) =>
        new(new FlatForward(Today, 0.0, A360), new FlatForward(Today, dividendYield, A360), 1.0, v0, 2.0, 0.01, 0.1, -0.5);

    // The variance integrated from the start to the maturity by the trapezoid rule on quarter-day steps, each the exact
    // mean reversion plus the truncated diffusion over the step, the
    // payoff's mean and standard error over antithetic pairs; the rate is 0
    private static (double Mean, double Error) MonteCarlo(Payoff payoff, int startDays, int maturityDays)
    {
        const int pairs = 100000, stepsPerDay = 4;
        const double v0 = 2.0, kappa = 2.0, theta = 0.01, sigma = 0.1;
        double dt = 1.0 / (360.0 * stepsPerDay);
        int startStep = startDays * stepsPerDay, steps = maturityDays * stepsPerDay;
        double decay = Math.Exp(-kappa * dt), spread = (1.0 - Math.Exp(-2.0 * kappa * dt)) / (2.0 * kappa);
        var rng = new MersenneTwisterUniformRng(42);
        double sum = 0.0, sumSquares = 0.0;
        double[] z = new double[steps];
        for (int p = 0; p < pairs; ++p)
        {
            for (int k = 0; k < steps; ++k)
            {
                z[k] = InverseCumulativeNormal.StandardValue(rng.NextReal());
            }

            double pair = 0.0;
            foreach (double sign in new[] { 1.0, -1.0 })
            {
                double v = v0, integrated = 0.0;
                for (int k = 0; k < steps; ++k)
                {
                    double vPlus = Math.Max(v, 0.0);
                    double next = theta + ((v - theta) * decay) + (sigma * Math.Sqrt(vPlus * spread) * sign * z[k]);
                    if (k >= startStep)
                    {
                        integrated += 0.5 * (vPlus + Math.Max(next, 0.0)) * dt;
                    }

                    v = next;
                }

                pair += 0.5 * payoff.Evaluate(integrated);
            }

            sum += pair;
            sumSquares += pair * pair;
        }

        double mean = sum / pairs;
        double variance = (sumSquares / pairs) - (mean * mean);
        return (mean, Math.Sqrt(variance / pairs));
    }
}
