// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The geometric Asian engines on term structures. QuantLib's discrete and continuous average-price engines read one
/// Black volatility at the exercise date and the zero rates to it for every fixing
/// (<c>analytic_discr_geom_av_price.cpp:92-108</c>, <c>analytic_cont_geom_av_price.cpp:57-78</c>), and the
/// average-strike engine one volatility at the spot: on a rising market (forward rates 1%, 3%, 6%, dividends 0%, 1%,
/// 3%, forward volatilities 15%, 25%, 35% to half a year, a year and two) a call at 100 on twelve fixings every 46 days
/// prices 7.937526857004053, continuously over 548 days 7.38232785034412 and on the average strike 7.943289362148803,
/// and on the falling market (each reversed) 7.896230538510976, 7.377847985865606 and 7.895540021985099: +37.4% and
/// −18.9% off the exact discrete price, +39.4% and −19.2% off the continuous one. nanoql prices the average exactly
/// under deterministic structures, each fixing's forward and variance read by date, and takes the form's own Greeks.
/// Checks: the exact law of ln G (and of ln S_T beside it) computed here, the continuous one's integrals by
/// Gauss-Lobatto split at the structures' nodes; the Greeks against bumps of the spot, of the strike, of every
/// volatility in parallel and of each curve's zero rates; and the continuous form at a variance integral below 2.2e-4
/// and at a mean whose integrand is rounding noise, each against the flat closed form.
/// </summary>
public class GeometricAsianTermStructureTests
{
    private static readonly Date Today = new(1, Month.January, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly Date[] Fixings = [.. Enumerable.Range(1, 12).Select(i => Today + (46 * i))];
    private static readonly Date Expiry = Today + 548;

    [Fact]
    public void TheDiscreteFormOnARisingMarketIsExact() => Assert.Equal(DiscreteExact(P5(true), 100.0), Discrete(P5(true), 100.0).NPV!.Value, 1e-12);

    [Fact]
    public void TheDiscreteFormOnAFallingMarketIsExact() => Assert.Equal(DiscreteExact(P5(false), 100.0), Discrete(P5(false), 100.0).NPV!.Value, 1e-12);

    [Fact]
    public void TheContinuousFormOnARisingMarketIsExact() => Assert.Equal(ContinuousExact(P5(true), 100.0), Continuous(P5(true), 100.0).NPV!.Value, 1e-10);

    [Fact]
    public void TheContinuousFormOnAFallingMarketIsExact() => Assert.Equal(ContinuousExact(P5(false), 100.0), Continuous(P5(false), 100.0).NPV!.Value, 1e-10);

    [Fact]
    public void TheAverageStrikeOnARisingMarketIsExact() => Assert.Equal(StrikeExact(P5(true)), Strike(P5(true)), 1e-12);

    [Fact]
    public void TheAverageStrikeOnAFallingMarketIsExact() => Assert.Equal(StrikeExact(P5(false)), Strike(P5(false)), 1e-12);

    [Fact]
    public void TheDiscreteGreeksOnARisingMarketMeetBumps() => CheckGreeks(true, Discrete, true);

    [Fact]
    public void TheDiscreteGreeksOnAFallingMarketMeetBumps() => CheckGreeks(false, Discrete, true);

    [Fact]
    public void TheContinuousGreeksOnARisingMarketMeetBumps() => CheckGreeks(true, Continuous, false);

    [Fact]
    public void TheContinuousGreeksOnAFallingMarketMeetBumps() => CheckGreeks(false, Continuous, false);

    [Fact]
    public void AShortAveragingAtLowVolatilityIsTheClosedForm()
    {
        // ten days at σ 1%: the variance integral σ²T³/3 is 6.9e-10, below which an unscaled 1e-12 is refused
        GeneralizedBlackScholesProcess p = Flat(100.0, 0.05, 0.02, 0.01);
        var option = new ContinuousAveragingAsianOption(AverageType.Geometric, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 10));
        double price = AnalyticContinuousGeometricAveragePriceAsianEngine.Calculate(option, Context, p).NPV!.Value;
        double expected = FlatClosedForm(100.0, 0.05, 0.02, 0.01, 10.0 / 365.0, 100.0);
        Assert.Equal(expected, price, 1e-10 * Math.Max(expected, 1.0));
    }

    [Fact]
    public void AMeanOfRoundingNoiseIsTheClosedForm()
    {
        // spot 1 and r − q = σ²/2: ln F(t) − V(t)/2 is 0 but for rounding
        GeneralizedBlackScholesProcess p = Flat(1.0, 0.05, 0.03, 0.2);
        var option = new ContinuousAveragingAsianOption(AverageType.Geometric, new PlainVanillaPayoff(OptionType.Call, 1.0), new EuropeanExercise(Today + 365));
        double price = AnalyticContinuousGeometricAveragePriceAsianEngine.Calculate(option, Context, p).NPV!.Value;
        double expected = FlatClosedForm(1.0, 0.05, 0.03, 0.2, 1.0, 1.0);
        Assert.Equal(expected, price, 1e-10 * Math.Max(expected, 1.0));
    }

    private static OneAssetOptionResults Discrete(GeneralizedBlackScholesProcess p, double strike) =>
        AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(
            new DiscreteAveragingAsianOption(AverageType.Geometric, 1.0, 0, Fixings, new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Fixings[^1])),
            Context,
            p);

    private static OneAssetOptionResults Continuous(GeneralizedBlackScholesProcess p, double strike) =>
        AnalyticContinuousGeometricAveragePriceAsianEngine.Calculate(
            new ContinuousAveragingAsianOption(AverageType.Geometric, new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Expiry)), Context, p);

    private static double Strike(GeneralizedBlackScholesProcess p) =>
        AnalyticDiscreteGeometricAverageStrikeAsianEngine.Calculate(
            new DiscreteAveragingAsianOption(AverageType.Geometric, 1.0, 0, Fixings, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Fixings[^1])),
            Context,
            p).NPV!.Value;

    // ln G normal: mean (1/N)Σ[ln F(tᵢ) − V(tᵢ)/2], variance (1/N²)ΣΣV(min(tᵢ, tⱼ)), V at the strike
    private static double DiscreteExact(GeneralizedBlackScholesProcess p, double strike)
    {
        (double mean, double variance) = Law(p, strike);
        return BlackFormula(OptionType.Call, strike, Math.Exp(mean + (variance / 2.0)), Math.Sqrt(variance), p.RiskFreeRate.Discount(Fixings[^1]));
    }

    // the exchange of G for S_T, jointly normal in logs, cov(ln S_T, ln G) = (1/N)ΣV(tᵢ), V at the spot
    private static double StrikeExact(GeneralizedBlackScholesProcess p)
    {
        (double mean, double variance) = Law(p, p.X0);
        Date t = Fixings[^1];
        double forwardS = p.X0 * p.DividendYield.Discount(t) / p.RiskFreeRate.Discount(t);
        double forwardG = Math.Exp(mean + (variance / 2.0));
        double covariance = Fixings.Average(d => p.BlackVolatility.BlackVariance(d, p.X0));
        double sd = Math.Sqrt(p.BlackVolatility.BlackVariance(t, p.X0) + variance - (2.0 * covariance));
        var n = new CumulativeNormalDistribution();
        double d1 = (Math.Log(forwardS / forwardG) / sd) + (sd / 2.0);
        return p.RiskFreeRate.Discount(t) * ((forwardS * n.Evaluate(d1)) - (forwardG * n.Evaluate(d1 - sd)));
    }

    private static (double Mean, double Variance) Law(GeneralizedBlackScholesProcess p, double strike)
    {
        double[] logForwards = [.. Fixings.Select(d => Math.Log(p.X0 * p.DividendYield.Discount(d) / p.RiskFreeRate.Discount(d)))];
        double[] variances = [.. Fixings.Select(d => p.BlackVolatility.BlackVariance(d, strike))];
        int n = Fixings.Length;
        double mean = Enumerable.Range(0, n).Sum(i => logForwards[i] - (variances[i] / 2.0)) / n;
        double variance = Enumerable.Range(0, n).Sum(i => Enumerable.Range(0, n).Sum(j => variances[Math.Min(i, j)])) / n / n;
        return (mean, variance);
    }

    // mean (1/T)∫[ln F(t) − V(t)/2]dt, variance (2/T²)∫V(t)(T − t)dt, by Gauss-Lobatto split at the nodes
    private static double ContinuousExact(GeneralizedBlackScholesProcess p, double strike)
    {
        double t = A365.YearFraction(Today, Expiry);
        double[] cuts = [0.0, 182.0 / 365.0, 1.0, t];
        var rule = new GaussLobattoIntegral(100000, 1e-15);
        double Integral(Func<double, double> f) => Enumerable.Range(1, cuts.Length - 1).Sum(i => rule.Integrate(f, cuts[i - 1], cuts[i]));
        double V(double u) => u > 0.0 ? p.BlackVolatility.BlackVariance(u, strike) : 0.0;
        double mean = Integral(u => Math.Log(p.X0 * p.DividendYield.Discount(u) / p.RiskFreeRate.Discount(u)) - (V(u) / 2.0)) / t;
        double variance = 2.0 / (t * t) * Integral(u => V(u) * (t - u));
        return BlackFormula(OptionType.Call, strike, Math.Exp(mean + (variance / 2.0)), Math.Sqrt(variance), p.RiskFreeRate.Discount(Expiry));
    }

    // the delta, gamma, vega, rho, dividend rho and (for the discrete form) strike sensitivity against central bumps
    private static void CheckGreeks(bool rising, Func<GeneralizedBlackScholesProcess, double, OneAssetOptionResults> engine, bool strikeSensitivity)
    {
        OneAssetOptionResults r = engine(P5(rising), 100.0);
        const double ds = 1e-2, dv = 1e-5, dr = 1e-5, dk = 1e-2;
        double Npv(GeneralizedBlackScholesProcess p, double k = 100.0) => engine(p, k).NPV!.Value;
        double delta = (Npv(P5(rising, spot: 100.0 + ds)) - Npv(P5(rising, spot: 100.0 - ds))) / (2 * ds);
        double gamma = (engine(P5(rising, spot: 100.0 + ds), 100.0).Delta!.Value - engine(P5(rising, spot: 100.0 - ds), 100.0).Delta!.Value) / (2 * ds);
        double vega = (Npv(P5(rising, volShift: dv)) - Npv(P5(rising, volShift: -dv))) / (2 * dv);
        double rho = (Npv(P5(rising, rateShift: dr)) - Npv(P5(rising, rateShift: -dr))) / (2 * dr);
        double dividendRho = (Npv(P5(rising, dividendShift: dr)) - Npv(P5(rising, dividendShift: -dr))) / (2 * dr);
        Assert.Equal(delta, r.Delta!.Value, 1e-8);
        Assert.Equal(gamma, r.Gamma!.Value, 1e-8);
        Assert.Equal(vega, r.Vega!.Value, 1e-5);
        Assert.Equal(rho, r.Rho!.Value, 1e-5);
        Assert.Equal(dividendRho, r.DividendRho!.Value, 1e-5);
        if (strikeSensitivity)
        {
            double dPdK = (Npv(P5(rising), 100.0 + dk) - Npv(P5(rising), 100.0 - dk)) / (2 * dk);
            Assert.Equal(dPdK, r.StrikeSensitivity!.Value, 1e-8);
        }
    }

    // The rising and falling markets: forward rates, dividends and forward volatilities piecewise flat to half a year,
    // a year and two; the volatilities on daily nodes of the same variance, each shifted in parallel by volShift, each
    // curve's zero rates by its shift
    private static GeneralizedBlackScholesProcess P5(
        bool rising, double spot = 100.0, double volShift = 0.0, double rateShift = 0.0, double dividendShift = 0.0)
    {
        double[] fr = rising ? [0.01, 0.03, 0.06] : [0.06, 0.03, 0.01];
        double[] fq = rising ? [0.0, 0.01, 0.03] : [0.03, 0.01, 0.0];
        double[] fv = rising ? [0.15, 0.25, 0.35] : [0.35, 0.25, 0.15];
        Date[] pillars = [Today, Today + 182, Today + 365, Today + 730];
        YieldTermStructure r = new ForwardCurve(pillars, [fr[0], fr[0], fr[1], fr[2]], A365);
        YieldTermStructure q = new ForwardCurve(pillars, [fq[0], fq[0], fq[1], fq[2]], A365);
        double[] years = [0.0, 0.5, 1.0, 2.0];
        double Piece(double t) => Enumerable.Range(0, 3).Sum(i => fv[i] * fv[i] * Math.Max(0.0, Math.Min(t, years[i + 1]) - years[i]));
        var nodes = new BlackVarianceCurve(Today, pillars[1..], [.. years[1..].Select(t => Math.Sqrt(Piece(t) / t))], A365, false);
        Date[] days = [.. Enumerable.Range(1, 730).Select(i => Today + i)];
        var v = new BlackVarianceCurve(Today, days, [.. days.Select(d => nodes.BlackVol(d, 100.0) + volShift)], A365, false);
        if (rateShift != 0.0)
        {
            r = new ZeroSpreadedTermStructure(r, rateShift);
        }

        if (dividendShift != 0.0)
        {
            q = new ZeroSpreadedTermStructure(q, dividendShift);
        }

        return new BlackScholesMertonProcess(spot, q, r, v);
    }

    private static GeneralizedBlackScholesProcess Flat(double spot, double r, double q, double v) =>
        new BlackScholesMertonProcess(
            spot, new FlatForward(Today, q, A365), new FlatForward(Today, r, A365), new BlackConstantVol(Today, new NullCalendar(), v, A365));

    // the continuous geometric average from today on a flat market: ln G normal, mean ln S + (r − q − σ²/2)T/2,
    // variance σ²T/3
    private static double FlatClosedForm(double spot, double r, double q, double v, double t, double strike)
    {
        double mean = Math.Log(spot) + ((r - q - (0.5 * v * v)) * t / 2.0);
        double variance = v * v * t / 3.0;
        return BlackFormula(OptionType.Call, strike, Math.Exp(mean + (variance / 2.0)), Math.Sqrt(variance), Math.Exp(-r * t));
    }
}
