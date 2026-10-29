// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The variance-gamma and FFT engines where QuantLib's fail (the S-xeq plan's XER5 (xvi), (xviii), (xix); the user's
/// rulings of 2026-10-28 at about 10:22, 10:55 and 11:27 and at the S-xeq run's stops 6 and 9; the run's R2-4). QuantLib's
/// analytic engine takes the gamma density as Γ(t/ν)·ν^(t/ν), an overflow times an underflow past t/ν of about 170, so
/// at ν 0.002 over a year it throws "Interval contains no more machine number", and starts its bound at 15√(νt), below
/// the gamma time's mean once ν &lt; t/225; nanoql takes the density in logs there and starts the bound beyond the mean
/// where ν &lt; t/100, and the price rises toward Black-Scholes as ν falls, within the FFT engine's ordinary accuracy of
/// it. QuantLib's FFT engine damps at α 1.25 even past the moment E[S^(α+1)], pricing 0.494 against the analytic 48.34
/// at ν 2, θ 0.3, σ 0.5 [executed: the planner's <c>xp_c2.cpp</c>]; nanoql damps inside the strip, on the put side near
/// its boundary, matching the planner's independent quadratures (<c>xp_fft_putside.py</c>, this run's
/// <c>probes\vgref\refs.out</c>) within 1e-5. QuantLib sizes the grid from the largest strike's log alone, undefined below
/// e^(−λ) ("vector too long" on an FX call at 0.85) and too coarse in frequency wherever its size falls below 2^14 (any
/// strike between 1 and about 60); nanoql sizes it from the largest |log K| and at least 2^14, QuantLib's size on every
/// QuantLib case. QuantLib's grid at α 1.25, period λn, aliases the damped call's tail once σ√T passes about 1.2, an
/// at-the-money call pricing −298.8926 against 66.80 at σ√T 2 [executed: the code review's <c>cr2_fft.cpp</c> on QuantLib
/// 1.44]; nanoql doubles the grid until two successive grids agree within 5e-5 of the spot, QuantLib's grid where it
/// already agrees. Checks: the analytic engines, Black-Scholes's limit, the independent quadratures.
/// </summary>
public class VarianceGammaFftTests
{
    private static readonly Date Today = new(15, Month.May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AtSmallNuTheAnalyticPriceRisesTowardBlackScholes()
    {
        // σ 0.2, θ 0, a year at the money: Black-Scholes 10.45058357; QuantLib throws at ν 0.005 and below
        double blackScholes = AnalyticEuropeanEngine.Calculate(Call(100.0, 365), Context, Bs(100.0, 0.2, 0.0, 0.05)).NPV!.Value;
        double previous = 0.0;
        foreach (double nu in new[] { 0.01, 0.005, 0.002, 0.001 })
        {
            VarianceGammaProcess process = Vg(0.2, nu, 0.0);
            double analytic = VarianceGammaEngine.Calculate(Call(100.0, 365), Context, process).NPV!.Value;
            double fft = FFTVarianceGammaEngine.Calculate(Call(100.0, 365), Context, process).NPV!.Value;
            Assert.True(analytic > previous && analytic < blackScholes, $"ν {nu}: {analytic}");
            Assert.True(Math.Abs(analytic - fft) < 2e-3, $"ν {nu}: {analytic} against the FFT's {fft}");
            previous = analytic;
        }

        Assert.True(blackScholes - previous < 1.5e-3, $"{previous} against {blackScholes}");
    }

    [Fact]
    public void PastTheDampingsMomentTheFftIsTheAnalyticPrice()
    {
        // ν 2, θ 0.3, σ 0.5: E[S^2.25] infinite; QuantLib's FFT 0.494
        VarianceGammaProcess process = Vg(0.5, 2.0, 0.3);
        double analytic = VarianceGammaEngine.Calculate(Call(100.0, 365), Context, process).NPV!.Value;
        double fft = FFTVarianceGammaEngine.Calculate(Call(100.0, 365), Context, process).NPV!.Value;
        Assert.Equal(48.33833738, analytic, 1e-8);
        Assert.Equal(analytic, fft, 1e-5);
    }

    [Theory]
    [InlineData(0.5, 2.0, 0.03, 91, 32.391883237377, 30.221182333812, 28.573476902513)] // the call side
    [InlineData(0.5, 2.0, 0.003, 365, 88.379320821636, 88.018530725147, 87.669090680444)] // the put side
    [InlineData(0.2, 3.0, 0.019, 365, 63.190895543607, 61.794023929066, 60.511322182007)]
    [InlineData(0.35, 1.0, 0.01, 1095, 99.954100882419, 99.952044425238, 99.949929509136)]
    [InlineData(0.5, 0.5, 0.001, 91, 92.679674755840, 92.484907238678, 92.295315054119)]
    public void NearTheMomentBoundaryTheFftIsTheIndependentQuadrature(
        double sigma, double nu, double gap, int days, double k80, double k100, double k125)
    {
        // the strip's upper bound 1 + gap: θ from 1 − θν(1 + gap) − σ²ν(1 + gap)²/2 = 0; the analytic engine throws here
        double hi = 1.0 + gap;
        double theta = (1.0 - (0.5 * sigma * sigma * nu * hi * hi)) / (nu * hi);
        ImmutableArray<OneAssetOptionResults> fft = FFTVarianceGammaEngine.Calculate(
            [Call(80.0, days), Call(100.0, days), Call(125.0, days)], Context, Vg(sigma, nu, theta));
        Assert.Equal(k80, fft[0].NPV!.Value, 1e-5);
        Assert.Equal(k100, fft[1].NPV!.Value, 1e-5);
        Assert.Equal(k125, fft[2].NPV!.Value, 1e-5);
    }

    [Theory]
    [InlineData(0.86, 0.85)] // QuantLib: "vector too long"
    [InlineData(1.1, 1.2)] // QuantLib's 2^9 points, its frequency spacing 12.3
    [InlineData(20.0, 25.0)]
    public void AtStrikesNearOneTheFftIsTheAnalyticPrice(double spot, double strike)
    {
        // an FX market: σ 10%, rates 3% and 2%, a year; the FFT's ordinary accuracy, about 1.1e-5 of the spot (QuantLib's
        // own at the money at 100: 1.1e-3)
        GeneralizedBlackScholesProcess process = Bs(spot, 0.1, 0.02, 0.03);
        double fft = FFTVanillaEngine.Calculate(Call(strike, 365), Context, process).NPV!.Value;
        double analytic = AnalyticEuropeanEngine.Calculate(Call(strike, 365), Context, process).NPV!.Value;
        Assert.True(Math.Abs(fft - analytic) <= 2e-5 * spot, $"{fft} against {analytic}");
    }

    [Fact]
    public void AListsLargestStrikeSetsItsExpirysGrid()
    {
        // QuantLib's precalculate (XER1): the at-the-money call alone and listed with a strike of 2,000 share a grid;
        // listed with 5,000 it moves to the next power of two
        GeneralizedBlackScholesProcess process = Bs(100.0, 0.2, 0.0, 0.05);
        double alone = FFTVanillaEngine.Calculate(Call(100.0, 365), Context, process).NPV!.Value;
        ImmutableArray<OneAssetOptionResults> with2000 = FFTVanillaEngine.Calculate([Call(100.0, 365), Call(2000.0, 365)], Context, process);
        ImmutableArray<OneAssetOptionResults> with5000 = FFTVanillaEngine.Calculate([Call(5000.0, 365), Call(100.0, 365)], Context, process);
        Assert.Equal(alone, with2000[0].NPV!.Value);
        Assert.NotEqual(alone, with5000[1].NPV!.Value);
        Assert.True(with5000[0].NPV!.Value < 1e-10, $"{with5000[0].NPV}");
        Assert.Equal(alone, with5000[1].NPV!.Value, 2e-3);
    }

    [Theory]
    [InlineData(1.3, 365)]
    [InlineData(1.5, 365)]
    [InlineData(2.0, 365)]
    [InlineData(3.0, 365)]
    [InlineData(3.5, 365)] // the damped tail's growth 17.2, below the bound
    [InlineData(0.8, 1460)] // σ√T 1.6
    public void PastSigmaRootTOf12TheGridIsDoubledToTheAnalyticPrice(double sigma, int days)
    {
        // the stop-9 ruling: calls and puts at 0.25 to 4 times the spot on one expiry's grid, each within 5e-5 of the spot
        // of the analytic price, where QuantLib's grid prices the at-the-money call at −298.89 at σ√T 2
        GeneralizedBlackScholesProcess process = Bs(100.0, sigma, 0.01, 0.03);
        double[] moneyness = [0.25, 0.5, 1.0, 2.0, 4.0];
        VanillaOption[] options =
        [
            .. moneyness.Select(m => Call(100.0 * m, days)),
            .. moneyness.Select(m => new VanillaOption(
                new PlainVanillaPayoff(OptionType.Put, 100.0 * m), new EuropeanExercise(Today + days))),
        ];
        ImmutableArray<OneAssetOptionResults> fft = FFTVanillaEngine.Calculate(options, Context, process);
        for (int k = 0; k < options.Length; k++)
        {
            double analytic = AnalyticEuropeanEngine.Calculate(options[k], Context, process).NPV!.Value;
            Assert.True(Math.Abs(fft[k].NPV!.Value - analytic) <= 5e-5 * 100.0, $"option {k}: {fft[k].NPV} against {analytic}");
        }
    }

    [Fact]
    public void PastTheTailGrowthBoundTheFftRefuses()
    {
        // σ√T 4, the damped tail's growth 22.5: two grids agree there on a rounding floor while both are off, an
        // at-the-money call at σ√T 4.5 pricing 90.1243 against 89.3522 (the code review's O2-1)
        GeneralizedBlackScholesProcess process = Bs(100.0, 4.0, 0.01, 0.03);
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => FFTVanillaEngine.Calculate(Call(100.0, 365), Context, process));
        Assert.Equal("the FFT grid doesn't converge within 2^22 points at the damping 1.25: use the analytic engine", e.Message);
    }

    [Fact]
    public void AVarianceGammaCallAtALargeVarianceIsTheAnalyticPrice()
    {
        // σ 1.2, ν 0.2, θ −0.1 over two years: E[S^2.25] finite (the strip's upper bound 2.71), so QuantLib's damping at
        // 1.25, at an effective σ√T of 1.7, where its grid aliases
        var process = new VarianceGammaProcess(
            100.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.05, A365), 1.2, 0.2, -0.1);
        foreach (double strike in new[] { 50.0, 100.0, 200.0 })
        {
            VanillaOption option = Call(strike, 730);
            double analytic = VarianceGammaEngine.Calculate(option, Context, process).NPV!.Value;
            double fft = FFTVarianceGammaEngine.Calculate(option, Context, process).NPV!.Value;
            Assert.True(Math.Abs(fft - analytic) <= 5e-5 * 100.0, $"strike {strike}: {fft} against {analytic}");
        }
    }

    private static VanillaOption Call(double strike, int days) =>
        new(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Today + days));

    [Theory]
    [InlineData(0.5, 2.0, 0.003, 80.2502360632259, 51.5316342191307, 45.8193442741808, 43.0694978037821, 41.084603657554)]
    [InlineData(0.2, 3.0, 0.019, 80.2477675553914, 50.619476916876, 24.7157579374618, 21.2651877301676, 19.122805024221)]
    public void ANearBoundaryListFromStrike20To400IsTheIndependentQuadrature(
        double sigma, double nu, double gap, double k20, double k50, double k100, double k200, double k400)
    {
        // the plan's B3-1 grid at three months, on the put side: its puts at a strike of 20, below 1e-6, keep changing
        // relatively at the grid's rounding, so a convergence test on the put alone refuses the list; the references
        // the Black-Scholes call and put on the gamma time g = u^(t/ν) integrated against its density at 30 digits,
        // the call and the put with parity agreeing to 15 digits (the run's vg_two_routes.py, which gives the
        // near-boundary fact's (0.5, 2, 0.03) and (0.2, 3, 0.019) rows to 13 digits)
        double hi = 1.0 + gap;
        double theta = (1.0 - (0.5 * sigma * sigma * nu * hi * hi)) / (nu * hi);
        double[] strikes = [20.0, 50.0, 100.0, 200.0, 400.0];
        double[] independent = [k20, k50, k100, k200, k400];
        ImmutableArray<OneAssetOptionResults> fft = FFTVarianceGammaEngine.Calculate(
            [.. strikes.Select(k => Call(k, 91))], Context, Vg(sigma, nu, theta));
        for (int m = 0; m < strikes.Length; m++)
        {
            Assert.True(
                Math.Abs(fft[m].NPV!.Value - independent[m]) <= 1e-5,
                $"strike {strikes[m]}: {fft[m].NPV} against {independent[m]}");
        }
    }

    private static VarianceGammaProcess Vg(double sigma, double nu, double theta) =>
        new(100.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.05, A365), sigma, nu, theta);

    private static GeneralizedBlackScholesProcess Bs(double spot, double sigma, double q, double r) =>
        new BlackScholesMertonProcess(
            spot, new FlatForward(Today, q, A365), new FlatForward(Today, r, A365),
            new BlackConstantVol(Today, new NullCalendar(), sigma, A365));
}
