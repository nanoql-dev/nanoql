// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Levy's engine on term structures and at its closed form's singular points (fixed). QuantLib reads one volatility and
/// the zero rates at maturity (<c>continuousarithmeticasianlevyengine.cpp:81-90</c>), +38.5% and −17.2% off a Monte
/// Carlo on rising and falling markets; and its closed form, taken wherever |b| &gt; 1000·QL_EPSILON, cancels near the
/// carries b = 0, −σ²/2 and −σ² and at small volatility: on spot 100, the call at 100 over a year from today (r 5%, σ
/// 20%, Act/365 Fixed), it prices 1.4982854223412403 at b = 3e-13 (−65.8% off its own formula in 50 digits) and NaN at
/// b = −σ²/2 and −σ², and NaN at σ 1e-8 and b 3%. nanoql integrates the two moments under the structures by date, the
/// covariance on QuantLib's adaptive Gauss-Lobatto rule, the log-variance through log1p of the normalized covariance,
/// so nothing cancels, and at zero variance takes the average as its forward. A Kronrod rule for the covariance missed
/// the exact value by −8.9e-9 and +4.3e-9 on those markets, fooled by the kinks at the structures' nodes, so it runs on
/// Gauss-Lobatto. Checks: the exact value, Levy's formula on the moments in 30 digits (5.7995941086251922 rising,
/// 9.6602716681694337 falling), within the tolerance, and the same formula on the moments computed here by
/// Gauss-Lobatto split at the nodes, and a Monte Carlo, on those markets; QuantLib's formula in 50 digits at the
/// singular points and at small volatility, within the tolerance; the forward's payoff at σ = 0. QuantLib's master
/// fixes the same class after v1.44 by another route (#2926: a cancellation-free series for the second moment, log1p,
/// the intrinsic forward at zero variance), whose three cases <c>AsianOptionsTests</c> ports as checks of this engine,
/// its mean taken as spot·t₂ plus the integral of F/spot − 1 so it is exact at zero carry; it stays until the pin moves
/// to a release carrying the fix.
/// </summary>
public class LevyTermStructureTests
{
    private static readonly Date Today = new(1, Month.January, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void ARisingMarketIsLevysFormulaOnItsMoments() => CheckShaped(true, 5.7995941086251922);

    [Fact]
    public void AFallingMarketIsLevysFormulaOnItsMoments() => CheckShaped(false, 9.6602716681694337);

    [Fact]
    public void ACarryOfZeroIsTheFormulaInFiftyDigits() => Assert.Equal(4.3867873590552396768, Flat(0.05 - 3e-13, 0.2, 100.0), 1e-10 * 4.39);

    [Fact]
    public void ACarryOfMinusHalfTheVarianceIsTheFormulaInFiftyDigits() => Assert.Equal(3.8979193402025997152, Flat(0.07, 0.2, 100.0), 1e-10 * 3.9);

    [Fact]
    public void ACarryOfMinusTheVarianceIsTheFormulaInFiftyDigits() => Assert.Equal(3.4477433605728323674, Flat(0.09, 0.2, 100.0), 1e-10 * 3.45);

    [Fact]
    public void ASmallVolatilityIsTheFormulaInFiftyDigits() => Assert.Equal(1.4412202367329096326, Flat(0.02, 1e-8, 100.0), 1e-10 * 1.44);

    [Fact]
    public void ZeroVolatilityIsTheForwardsPayoff()
    {
        // E[A] = S(e^{bT} − 1)/(bT) at b 3%: the covariance's first estimate is 0
        double average = 100.0 * (Math.Exp(0.03) - 1.0) / 0.03;
        Assert.Equal(Math.Exp(-0.05) * (average - 100.0), Flat(0.02, 0.0, 100.0), 1e-12);
    }

    // a year from today on spot 100, r 5%, the call at the strike
    private static double Flat(double q, double v, double strike)
    {
        var p = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, q, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), v, A365));
        return Levy(p, strike, Today + 365);
    }

    private static double Levy(GeneralizedBlackScholesProcess p, double strike, Date expiry) =>
        ContinuousArithmeticAsianLevyEngine.Calculate(
            new ContinuousAveragingAsianOption(AverageType.Arithmetic, Today, new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(expiry)),
            Context,
            p,
            100.0).NPV!.Value;

    private static void CheckShaped(bool rising, double exact)
    {
        GeneralizedBlackScholesProcess p = P5(rising);
        Date expiry = Today + 548;
        double t = A365.YearFraction(Today, expiry);
        double levy = Levy(p, 100.0, expiry);

        // E[A] = (1/T)∫F and E[A²] = (2/T²)∫∫_{s<u} F(s)F(u)e^{V(s)}, Gauss-Lobatto split at the nodes
        double[] cuts = [0.0, 182.0 / 365.0, 1.0, t];
        var rule = new GaussLobattoIntegral(1000000, 1e-13);
        double Split(Func<double, double> f, double upper)
        {
            double sum = 0.0;
            for (int i = 1; i < cuts.Length && cuts[i - 1] < upper; i++)
            {
                sum += rule.Integrate(f, cuts[i - 1], Math.Min(cuts[i], upper));
            }

            return sum;
        }

        double F(double u) => 100.0 * p.DividendYield.Discount(u) / p.RiskFreeRate.Discount(u);
        double V(double u) => u > 0.0 ? p.BlackVolatility.BlackVariance(u, 100.0) : 0.0;
        double mean = Split(F, t) / t;
        double second = 2.0 / (t * t) * Split(u => F(u) * Split(s => F(s) * Math.Exp(V(s)), u), t);
        double logVariance = Math.Log(second) - (2.0 * Math.Log(mean));
        double d1 = (Math.Log(mean / 100.0) + (logVariance / 2.0)) / Math.Sqrt(logVariance);
        var n = new CumulativeNormalDistribution();
        double discount = p.RiskFreeRate.Discount(expiry);
        double formula = discount * ((mean * n.Evaluate(d1)) - (100.0 * n.Evaluate(d1 - Math.Sqrt(logVariance))));
        Assert.Equal(exact, levy, 1e-10 * levy);
        Assert.Equal(formula, levy, 1e-10 * levy);

        // the average of the spot over every day, exact lognormal steps on QuantLib's Mersenne twister, trapezoid
        (double mc, double error) = DailyMc(p, 548, 50000);
        Assert.True(Math.Abs(levy - mc) <= (3.0 * error) + (0.01 * mc), $"{levy} against {mc} +- {error}");
    }

    private static (double Mean, double Error) DailyMc(GeneralizedBlackScholesProcess p, int days, int pairs)
    {
        var rng = new MersenneTwisterUniformRng(42);
        var inv = new InverseCumulativeNormal();
        double[] drift = new double[days], sd = new double[days];
        for (int i = 0; i < days; i++)
        {
            Date a = Today + i, b = Today + i + 1;
            double variance = p.BlackVolatility.BlackVariance(b, 100.0) - (i == 0 ? 0.0 : p.BlackVolatility.BlackVariance(a, 100.0));
            drift[i] = Math.Log(p.DividendYield.Discount(b) / p.DividendYield.Discount(a) * p.RiskFreeRate.Discount(a) / p.RiskFreeRate.Discount(b)) - (0.5 * variance);
            sd[i] = Math.Sqrt(variance);
        }

        double discount = p.RiskFreeRate.Discount(Today + days);
        double sum = 0.0, sum2 = 0.0;
        var z = new double[days];
        for (int n = 0; n < pairs; n++)
        {
            for (int i = 0; i < days; i++)
            {
                z[i] = inv.Evaluate(rng.Next().Value);
            }

            double pair = 0.0;
            foreach (double sign in (double[])[1.0, -1.0])
            {
                double ln = Math.Log(100.0), previous = 100.0, area = 0.0;
                for (int i = 0; i < days; i++)
                {
                    ln += drift[i] + (sd[i] * sign * z[i]);
                    double spot = Math.Exp(ln);
                    area += 0.5 * (previous + spot);
                    previous = spot;
                }

                pair += 0.5 * Math.Max((area / days) - 100.0, 0.0) * discount;
            }

            sum += pair;
            sum2 += pair * pair;
        }

        double mean = sum / pairs;
        return (mean, Math.Sqrt(((sum2 / pairs) - (mean * mean)) / pairs));
    }

    // The rising and falling markets (GeometricAsianTermStructureTests' builder and its nodes)
    private static GeneralizedBlackScholesProcess P5(bool rising)
    {
        double[] fr = rising ? [0.01, 0.03, 0.06] : [0.06, 0.03, 0.01];
        double[] fq = rising ? [0.0, 0.01, 0.03] : [0.03, 0.01, 0.0];
        double[] fv = rising ? [0.15, 0.25, 0.35] : [0.35, 0.25, 0.15];
        Date[] pillars = [Today, Today + 182, Today + 365, Today + 730];
        double[] years = [0.0, 0.5, 1.0, 2.0];
        double Piece(double t) => Enumerable.Range(0, 3).Sum(i => fv[i] * fv[i] * Math.Max(0.0, Math.Min(t, years[i + 1]) - years[i]));
        return new BlackScholesMertonProcess(
            100.0,
            new ForwardCurve(pillars, [fq[0], fq[0], fq[1], fq[2]], A365),
            new ForwardCurve(pillars, [fr[0], fr[0], fr[1], fr[2]], A365),
            new BlackVarianceCurve(Today, pillars[1..], [.. years[1..].Select(t => Math.Sqrt(Piece(t) / t))], A365, false));
    }
}
