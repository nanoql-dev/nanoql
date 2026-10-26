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
/// The quanto engine on a skew. QuantLib reads the underlying's volatility for the drift at the payoff's strike, but for
/// <c>qvega</c> and <c>qlambda</c> at the spot (<c>quantoengine.hpp:155-166</c>), so on a skew of 0.30 at strike 60 to
/// 0.10 at 140 (FX volatility 10%, correlation 0.3, a year) they miss its own price bumped in the FX volatility and the
/// correlation by −27.6% at strike 70 and +56.9% at 130, calls and puts alike, exact at 100; and for a quanto
/// forward-start option it reads the drift at the payoff's strike, which the forward engine never uses, where it
/// strikes at the moneyness times the spot (3.1098 at payoff strike 0, 3.2384 at 100, 3.2452 at 105, the moneyness
/// times the spot). nanoql reads both at the drift's strike, the forward's at the moneyness times the spot (the S-exo
/// plan's ER5 (xxxvi), ED30). The checks: the price bumped in each quote, the prices QuantLib's bits; QuantLib's quanto
/// forward with its payoff struck at the moneyness times the spot, bit for bit, from any payoff strike.
/// </summary>
public class QuantoSmileTests
{
    private const double FxVolatility = 0.10, Correlation = 0.3, Bump = 1e-5;
    private static readonly Date Expiry = Today + 365;
    private static readonly YieldTermStructure Foreign = new FlatForward(Today, 0.02, A365);

    // QuantLib's prices on the skew: strike, call, put
    private static readonly (double Strike, double Call, double Put)[] Prices =
    [
        (70.0, 29.699550899176415, 0.9992990410141039),
        (100.0, 7.784406889599728, 7.403153120599646),
        (130.0, 0.1145407525645667, 28.062151647307733),
    ];

    [Fact]
    public void QvegaIsThePriceBumpedInTheExchangeRateVolatility()
    {
        foreach ((double strike, double call, double put) in Prices)
        {
            foreach ((OptionType type, double price) in new[] { (OptionType.Call, call), (OptionType.Put, put) })
            {
                QuantoOptionResults r = Price(type, strike, FxVolatility, Correlation);
                Assert.Equal(price, r.NPV!.Value);
                double bumped = (Price(type, strike, FxVolatility + Bump, Correlation).NPV!.Value
                    - Price(type, strike, FxVolatility - Bump, Correlation).NPV!.Value) / (2.0 * Bump);
                Assert.Equal(bumped, r.Qvega!.Value, 1e-6);
            }
        }
    }

    [Fact]
    public void QlambdaIsThePriceBumpedInTheCorrelation()
    {
        foreach ((double strike, double call, double put) in Prices)
        {
            foreach ((OptionType type, double price) in new[] { (OptionType.Call, call), (OptionType.Put, put) })
            {
                QuantoOptionResults r = Price(type, strike, FxVolatility, Correlation);
                Assert.Equal(price, r.NPV!.Value);
                double bumped = (Price(type, strike, FxVolatility, Correlation + Bump).NPV!.Value
                    - Price(type, strike, FxVolatility, Correlation - Bump).NPV!.Value) / (2.0 * Bump);
                Assert.Equal(bumped, r.Qlambda!.Value, 1e-6);
            }
        }
    }

    [Fact]
    public void TheQuantoForwardDriftIsReadAtTheForwardEnginesStrike()
    {
        // QuantLib's quanto forward with its payoff struck at 1.05 × 100
        const double expected = 3.2452252051196298;
        foreach (double payoffStrike in new[] { 0.0, 100.0, 105.0 })
        {
            var option = new QuantoForwardVanillaOption(
                1.05, Today + 182, new PlainVanillaPayoff(OptionType.Call, payoffStrike), new EuropeanExercise(Expiry));
            double npv = QuantoEngine.Calculate(
                option, Context, Skew(), Foreign, Fx(FxVolatility), Correlation,
                (o, c, p) => ForwardVanillaEngine.Calculate(o, c, p, AnalyticEuropeanEngine.Calculate)).NPV!.Value;
            Assert.Equal(expected, npv);
        }
    }

    private static QuantoOptionResults Price(OptionType type, double strike, double fxVolatility, double correlation) =>
        QuantoEngine.Calculate(
            new QuantoVanillaOption(new PlainVanillaPayoff(type, strike), new EuropeanExercise(Expiry)),
            Context,
            Skew(),
            Foreign,
            Fx(fxVolatility),
            correlation,
            AnalyticEuropeanEngine.Calculate);

    private static BlackConstantVol Fx(double v) => new(Today, new NullCalendar(), v, A365);

    // The skew, 0.20 + 0.10·(100 − K)/40 at every date: 0.30 at strike 60 to 0.10 at 140.
    private static GeneralizedBlackScholesProcess Skew()
    {
        double[] strikes = [60.0, 80.0, 100.0, 120.0, 140.0];
        var vols = new double[5, 3];
        for (int i = 0; i < 5; ++i)
        {
            for (int j = 0; j < 3; ++j)
            {
                vols[i, j] = 0.20 + (0.10 * (100.0 - strikes[i]) / 40.0);
            }
        }

        var surface = new BlackVarianceSurface(
            Today, new NullCalendar(), [Today + 182, Today + 365, Today + 730], strikes, new Matrix(vols), A365);
        return new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.01, A365), new FlatForward(Today, 0.05, A365), surface);
    }
}
