// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.ExoticTermStructureMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// C3's engines on structures counted or dated apart (the playbook's dates row; the S-exo plan's ER5 (xxiv)). QuantLib's
/// closed forms read each curve's rate on its own day counter and every time on the risk-free curve's, so with the
/// dividend curve on Act/360 (its rate restated, the same discounts by date) the compound option moves +0.21%, the
/// complex chooser +0.02%, the holder- and writer-extensible options +0.13% and +0.12%, and the simple chooser refuses
/// ("Risk-free rate and dividend yield must have the same day counter"); with the risk-free curve settling two days
/// on, against its restatement from the evaluation date, −0.36%, −0.41%, −0.24%, −0.23% and +0.01%. Its forward
/// engine's theta reads the dividend rate on that curve's own day counter (−1.4% on Act/360) and its dividend rho the
/// reset on the risk-free curve's time (−0.10% at T+2); its Monte Carlo grids run on the risk-free curve's time, the
/// forward engine's +0.16% on Act/360 dividends and +0.01% at T+2, the performance engine's +0.10% on Act/360. nanoql's
/// forms read discounts and variances by date and every time on the process's clock, and the forward engines' reset
/// terms in the dividend rho and the performance rho on the shifted curve's own time (C3's review, B3-1). The checks:
/// nanoql on the market restated onto one clock, which on structures dated and counted alike keeps QuantLib's bits;
/// for the reset terms, the cliquet and performance engines on the same one-period option and the prices bumped in the
/// curve's own zero rate.
/// </summary>
public class ExoticMismatchedTimesTests
{
    [Fact]
    public void TheClosedFormsReadADividendCurveOnItsOwnDays()
    {
        GeneralizedBlackScholesProcess restated = Market(Flat365(0.02), Flat365(0.05));
        GeneralizedBlackScholesProcess onAct360 = Market(new FlatForward(Today, 0.02 * 360.0 / 365.0, new Actual360()), Flat365(0.05));
        foreach ((string name, Func<GeneralizedBlackScholesProcess, OneAssetOptionResults> price) in ClosedForms())
        {
            double expected = price(restated).NPV!.Value;
            Assert.True(Math.Abs(price(onAct360).NPV!.Value - expected) <= 1e-12 * expected, name);
        }
    }

    [Fact]
    public void TheClosedFormsPriceARiskFreeCurveSettlingLaterFromTheEvaluationDate()
    {
        GeneralizedBlackScholesProcess restated = Market(Flat365(0.02), Settled());
        GeneralizedBlackScholesProcess settling = Market(Flat365(0.02), new FlatForward(Today + 2, 0.05, A365));
        foreach ((string name, Func<GeneralizedBlackScholesProcess, OneAssetOptionResults> price) in ClosedForms())
        {
            double expected = price(restated).NPV!.Value;
            Assert.True(Math.Abs(price(settling).NPV!.Value - expected) <= 1e-12 * expected, name);
        }
    }

    [Fact]
    public void TheForwardAndCliquetEnginesReadEveryTimeOnTheClock()
    {
        // the Greeks a restatement leaves alone: the risk-free rho where only the dividend curve is restated, the
        // dividend rho where only the risk-free curve is
        GeneralizedBlackScholesProcess oneClock = Market(Flat365(0.02), Flat365(0.05));
        GeneralizedBlackScholesProcess onAct360 = Market(new FlatForward(Today, 0.02 * 360.0 / 365.0, new Actual360()), Flat365(0.05));
        GeneralizedBlackScholesProcess restated = Market(Flat365(0.02), Settled());
        GeneralizedBlackScholesProcess settling = Market(Flat365(0.02), new FlatForward(Today + 2, 0.05, A365));
        foreach ((string name, Func<GeneralizedBlackScholesProcess, OneAssetOptionResults> price) in Periodic())
        {
            Same(name, price(oneClock), price(onAct360), r => r.Rho);
            Same(name, price(restated), price(settling), r => r.DividendRho);
        }
    }

    [Fact]
    public void TheMonteCarloGridsRunOnTheClock()
    {
        GeneralizedBlackScholesProcess oneClock = Market(Flat365(0.02), Flat365(0.05));
        GeneralizedBlackScholesProcess onAct360 = Market(new FlatForward(Today, 0.02 * 360.0 / 365.0, new Actual360()), Flat365(0.05));
        GeneralizedBlackScholesProcess restated = Market(Flat365(0.02), Settled());
        GeneralizedBlackScholesProcess settling = Market(Flat365(0.02), new FlatForward(Today + 2, 0.05, A365));
        foreach ((GeneralizedBlackScholesProcess expected, GeneralizedBlackScholesProcess actual) in new[]
        {
            (oneClock, onAct360), (restated, settling),
        })
        {
            double e = McPerformance(expected), a = McPerformance(actual);
            Assert.True(Math.Abs(a - e) <= 1e-12 * e, $"performance: {e} and {a}");
            e = McForward(expected);
            a = McForward(actual);
            Assert.True(Math.Abs(a - e) <= 1e-12 * e, $"forward: {e} and {a}");
        }
    }

    [Fact]
    public void TheForwardEnginesResetTermsReadTheShiftedCurvesOwnTime()
    {
        // a forward-start call at the money, reset at a year, expiry at two; the one-period cliquet and performance
        // option on it read each curve's own time to the reset, and the bumps shift each curve's own zero rate
        var forward = new ForwardVanillaOption(
            1.0, Today + 365, new PlainVanillaPayoff(OptionType.Call, 0.0), new EuropeanExercise(Today + 730));
        var onePeriod = new CliquetOption(
            new PercentageStrikePayoff(OptionType.Call, 1.0), new EuropeanExercise(Today + 730), [Today + 365]);
        const double h = 1e-5;
        foreach (Func<double, YieldTermStructure> dividends in new Func<double, YieldTermStructure>[]
        {
            q => new FlatForward(Today, 0.03 + q, new Actual360()),
            q => new FlatForward(Today + 2, 0.03 + q, A365),
        })
        {
            GeneralizedBlackScholesProcess P(double q) => Market(dividends(q), Flat365(0.05));
            double dividendRho = ForwardVanillaEngine.Calculate(forward, Context, P(0.0), AnalyticEuropeanEngine.Calculate)
                .DividendRho!.Value;
            double bumped = (ForwardVanillaEngine.Calculate(forward, Context, P(h), AnalyticEuropeanEngine.Calculate).NPV!.Value
                - ForwardVanillaEngine.Calculate(forward, Context, P(-h), AnalyticEuropeanEngine.Calculate).NPV!.Value) / (2.0 * h);
            Assert.Equal(AnalyticCliquetEngine.Calculate(onePeriod, Context, P(0.0)).DividendRho!.Value, dividendRho, 1e-10);
            Assert.Equal(bumped, dividendRho, 1e-5);
        }

        GeneralizedBlackScholesProcess R(double r) => Market(Flat365(0.03), new FlatForward(Today + 2, 0.05 + r, A365));
        double rho = ForwardPerformanceVanillaEngine.Calculate(forward, Context, R(0.0), AnalyticEuropeanEngine.Calculate).Rho!.Value;
        double rhoBumped = (ForwardPerformanceVanillaEngine.Calculate(forward, Context, R(h), AnalyticEuropeanEngine.Calculate).NPV!.Value
            - ForwardPerformanceVanillaEngine.Calculate(forward, Context, R(-h), AnalyticEuropeanEngine.Calculate).NPV!.Value) / (2.0 * h);
        Assert.Equal(AnalyticPerformanceEngine.Calculate(onePeriod, Context, R(0.0)).Rho!.Value, rho, 1e-12);
        Assert.Equal(rhoBumped, rho, 1e-7);
    }

    private static FlatForward Flat365(double rate) => new(Today, rate, A365);

    // The risk-free curve settling two days on, restated from today: its discount 1 to its date and its rate beyond,
    // which a log-linear discount curve holds exactly.
    private static DiscountCurve Settled() =>
        new([Today, Today + 2, Today + 2 + 3650], [1.0, 1.0, Math.Exp(-0.05 * 10.0)], A365);

    private static GeneralizedBlackScholesProcess Market(YieldTermStructure dividends, YieldTermStructure riskFree) =>
        new BlackScholesMertonProcess(
            100.0, dividends, riskFree, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static IEnumerable<(string, Func<GeneralizedBlackScholesProcess, OneAssetOptionResults>)> ClosedForms()
    {
        var compound = new CompoundOption(
            new PlainVanillaPayoff(OptionType.Call, 4.0), new EuropeanExercise(Today + 90),
            new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        var complexChooser = new ComplexChooserOption(
            Today + 90, 100.0, 95.0, new EuropeanExercise(Today + 270), new EuropeanExercise(Today + 365));
        var holder = new HolderExtensibleOption(
            OptionType.Call, 1.0, Today + 365, 105.0, new PlainVanillaPayoff(OptionType.Call, 100.0),
            new EuropeanExercise(Today + 270));
        var writer = new WriterExtensibleOption(
            new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 270),
            new PlainVanillaPayoff(OptionType.Call, 105.0), new EuropeanExercise(Today + 365));
        var simpleChooser = new SimpleChooserOption(Today + 90, 100.0, new EuropeanExercise(Today + 365));
        yield return ("compound", p => AnalyticCompoundOptionEngine.Calculate(compound, Context, p));
        yield return ("complex chooser", p => AnalyticComplexChooserEngine.Calculate(complexChooser, Context, p));
        yield return ("holder-extensible", p => AnalyticHolderExtensibleOptionEngine.Calculate(holder, Context, p));
        yield return ("writer-extensible", p => AnalyticWriterExtensibleOptionEngine.Calculate(writer, Context, p));
        yield return ("simple chooser", p => AnalyticSimpleChooserEngine.Calculate(simpleChooser, Context, p));
    }

    private static IEnumerable<(string, Func<GeneralizedBlackScholesProcess, OneAssetOptionResults>)> Periodic()
    {
        ForwardVanillaOption forward = ForwardCall();
        CliquetOption cliquet = Cliquet();
        yield return ("forward", p => ForwardVanillaEngine.Calculate(forward, Context, p, AnalyticEuropeanEngine.Calculate));
        yield return (
            "forward performance",
            p => ForwardPerformanceVanillaEngine.Calculate(forward, Context, p, AnalyticEuropeanEngine.Calculate));
        yield return ("cliquet", p => AnalyticCliquetEngine.Calculate(cliquet, Context, p));
        yield return ("performance", p => AnalyticPerformanceEngine.Calculate(cliquet, Context, p));
    }

    private static ForwardVanillaOption ForwardCall() =>
        new(1.1, Today + 90, new PlainVanillaPayoff(OptionType.Call, 0.0), new EuropeanExercise(Today + 365));

    private static CliquetOption Cliquet() =>
        new(new PercentageStrikePayoff(OptionType.Call, 1.1), new EuropeanExercise(Today + 365),
            [Today + 90, Today + 180, Today + 270]);

    private static double McPerformance(GeneralizedBlackScholesProcess p) =>
        new MakeMCPerformanceEngine<PseudoRandom, GeneralStatistics>(p, 42).WithSamples(16384).ToEngine()(
            Cliquet(), Context).NPV!.Value;

    private static double McForward(GeneralizedBlackScholesProcess p) =>
        new MakeMCForwardEuropeanBSEngine<PseudoRandom, GeneralStatistics>(p, 42).WithSteps(4).WithSamples(16384)
            .ToEngine()(ForwardCall(), Context).NPV!.Value;

    // The value and the Greeks two markets share, each within 1e-12 of the larger magnitude (or absolutely, under 1).
    private static void Same(
        string name, OneAssetOptionResults expected, OneAssetOptionResults actual, Func<OneAssetOptionResults, double?> rho)
    {
        foreach ((string greek, Func<OneAssetOptionResults, double?> read) in new (string, Func<OneAssetOptionResults, double?>)[]
        {
            ("NPV", r => r.NPV), ("delta", r => r.Delta), ("theta", r => r.Theta), ("vega", r => r.Vega), ("rho", rho),
        })
        {
            double e = read(expected)!.Value, a = read(actual)!.Value;
            Assert.True(
                Math.Abs(a - e) <= 1e-12 * Math.Max(1.0, Math.Abs(e)), $"{name} {greek}: expected {e}, calculated {a}");
        }
    }
}
