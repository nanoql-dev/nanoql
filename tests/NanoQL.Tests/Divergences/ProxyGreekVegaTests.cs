// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The proxy Greeks (the S-mmp plan's MMPR3, ruled by the user, and SMD13). QuantLib's constrained evolver weighs a path
/// by the shifted Gaussians' density ratio alone (<c>lognormalfwdrateeulerconstrained.cpp:200-212</c>), which misses the
/// transition densities' ratio of step deviations a volatility bump changes: <c>testGreeks</c>' proxy vegas, which it
/// prints and doesn't assert, take the wrong sign, 81 to 131 standard errors off Black's (the planner's MPP13). nanoql's
/// evolver takes the unbumped model as its reference and carries √(var_reference / var_own). On six semi-annual rates,
/// FlatVol 20%, 3 factors, digital caplets paying 1% at the forwards, the money-market measure, 8,191 Sobol paths
/// (seed 42), each step's forward constrained, forwards bumped 1e-6 and volatilities 1e-4: nanoql's deltas, gammas and
/// vegas within 4 standard errors of Black's; QuantLib's weight, the evolvers' own models as references, its vegas far
/// off. Checks: Black's formula on each caplet's total variance; QuantLib's accounting engine on the volatility-bumped
/// models, the same paths, on plain caplets; QuantLib's weight bit for bit where the reference is the evolver's own model
/// (the evolvers' and the proxy engine's vectors).
/// </summary>
public class ProxyGreekVegaTests
{
    private const int Paths = 8191;
    private const double ForwardBump = 1e-6;
    private const double VolBump = 1e-4;
    private static readonly MarketModelProductMarket Market = new(6);
    private static readonly EvolutionDescription Evolution = new(Market.RateTimes, Market.RateTimes[..^1]);

    [Fact]
    public void TheProxyGreeksAreBlacksWithinTheirErrors()
    {
        int n = Market.N;
        MarketModel model = Market.FlatVol(Evolution, 3);
        (SequenceStatisticsInc[] nanoql, SequenceStatisticsInc values) = Greeks(model, own: false);
        (SequenceStatisticsInc[] quantLib, _) = Greeks(model, own: true);

        Matrix total = model.TotalCovariance(model.NumberOfSteps - 1);
        ImmutableArray<double> price = values.Mean(), priceErrors = values.ErrorEstimate();
        ImmutableArray<double> delta = nanoql[0].Mean(), deltaErrors = nanoql[0].ErrorEstimate();
        ImmutableArray<double> gamma = nanoql[1].Mean(), gammaErrors = nanoql[1].ErrorEstimate();
        ImmutableArray<double> vega = nanoql[2].Mean(), vegaErrors = nanoql[2].ErrorEstimate();
        ImmutableArray<double> quantLibVega = quantLib[2].Mean(), quantLibVegaErrors = quantLib[2].ErrorEstimate();

        for (int i = 0; i < n; ++i)
        {
            double sd = Math.Sqrt(total[i, i]);
            double p0 = Black(i, Market.Forwards, sd);
            double pUp = Black(i, Bumped(ForwardBump), sd), pDown = Black(i, Bumped(-ForwardBump), sd);
            double blackDelta = (pUp - pDown) / (2.0 * ForwardBump);
            double blackGamma = (pUp - (2 * p0) + pDown) / (ForwardBump * ForwardBump);
            double blackVega = (Black(i, Market.Forwards, sd * (0.20 + VolBump) / 0.20) - Black(i, Market.Forwards, sd * (0.20 - VolBump) / 0.20)) / (2.0 * VolBump);

            Within(price[i], p0, priceErrors[i], $"caplet {i}'s value");
            Within(delta[i], blackDelta, deltaErrors[i], $"caplet {i}'s delta");
            Within(gamma[i], blackGamma, gammaErrors[i], $"caplet {i}'s gamma");
            Within(vega[i], blackVega, vegaErrors[i], $"caplet {i}'s vega");

            // QuantLib's weight: the vega far off Black's
            Assert.True(
                Math.Abs(quantLibVega[i] - blackVega) > 4 * quantLibVegaErrors[i],
                $"QuantLib's caplet {i}'s vega {quantLibVega[i]} ± {quantLibVegaErrors[i]} against {blackVega}");
        }
    }

    [Fact]
    public void TheProxyVegasAreTheAccountingEnginesSamePathDifferences()
    {
        // plain caplets at the forwards, the accounting engine on the models bumped down and up, the same Sobol paths
        int n = Market.N;
        MarketModel model = Market.FlatVol(Evolution, 3);
        (SequenceStatisticsInc[] nanoql, _) = Greeks(model, own: false, digital: false);
        (SequenceStatisticsInc[] quantLib, _) = Greeks(model, own: true, digital: false);
        ImmutableArray<double> up = Values(FlatVol(Market.Forwards, 0.20 + VolBump)), down = Values(FlatVol(Market.Forwards, 0.20 - VolBump));
        ImmutableArray<double> vega = nanoql[2].Mean(), vegaErrors = nanoql[2].ErrorEstimate();
        ImmutableArray<double> quantLibVega = quantLib[2].Mean(), quantLibVegaErrors = quantLib[2].ErrorEstimate();
        int far = 0;
        for (int i = 0; i < n; ++i)
        {
            double difference = (up[i] - down[i]) / (2.0 * VolBump);
            Within(vega[i], difference, vegaErrors[i], $"caplet {i}'s vega");
            far += Math.Abs(quantLibVega[i] - difference) > 4 * quantLibVegaErrors[i] ? 1 : 0;
        }

        // QuantLib's weight: the vegas off the differences
        Assert.True(far >= n - 1, $"QuantLib's vegas off on {far} caplets of {n}");
    }

    private static ImmutableArray<double> Values(MarketModel model)
    {
        var product = new MultiStepOptionlets(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Payoffs(digital: false));
        int[] numeraires = [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(Evolution)];
        var factory = new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, 42);
        (ImmutableArray<double> means, _) = MarketModelProductMarket.Simulate(
            new LogNormalFwdRateEuler(model, factory, numeraires), product, Market.Discounts[0], Paths);
        return means;
    }

    private static Payoff[] Payoffs(bool digital) =>
        [.. Market.Forwards.Select(f => digital ? (Payoff)new CashOrNothingPayoff(OptionType.Call, f, 0.01) : new PlainVanillaPayoff(OptionType.Call, f))];

    private static void Within(double value, double expected, double error, string what) =>
        Assert.True(Math.Abs(value - expected) <= 4 * error, $"{what} {value} ± {error} against {expected}");

    // The proxy engine on the digital caplets: the original evolver, forwards bumped down and up, volatilities bumped
    // down and up; the references the original model, or each evolver's own model (QuantLib's weight).
    private static (SequenceStatisticsInc[] Greeks, SequenceStatisticsInc Values) Greeks(MarketModel model, bool own, bool digital = true)
    {
        int n = Market.N;
        var product = new MultiStepOptionlets(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Payoffs(digital));
        int[] numeraires = [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(Evolution)];
        var factory = new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, 42);
        int[] start = [.. Enumerable.Range(0, n)], end = [.. Enumerable.Range(1, n)];

        ConstrainedEvolver Constrained(MarketModel bumped)
        {
            var e = new LogNormalFwdRateEulerConstrained(bumped, own ? bumped : model, factory, numeraires);
            e.SetConstraintType(start, end);
            return e;
        }

        ConstrainedEvolver[] deltaGamma = [Constrained(FlatVol(Bumped(-ForwardBump), 0.20)), Constrained(FlatVol(Bumped(ForwardBump), 0.20))];
        ConstrainedEvolver[] vegas = [Constrained(FlatVol(Market.Forwards, 0.20 - VolBump)), Constrained(FlatVol(Market.Forwards, 0.20 + VolBump))];
        double[][][] weights =
        [
            [
                [0.0, -1.0 / (2.0 * ForwardBump), 1.0 / (2.0 * ForwardBump)],
                [-2.0 / (ForwardBump * ForwardBump), 1.0 / (ForwardBump * ForwardBump), 1.0 / (ForwardBump * ForwardBump)],
            ],
            [[0.0, -1.0 / (2.0 * VolBump), 1.0 / (2.0 * VolBump)]],
        ];
        var engine = new ProxyGreekEngine(
            new LogNormalFwdRateEuler(model, factory, numeraires), [deltaGamma, vegas], weights, start, end, product, Market.Discounts[0]);
        var values = new SequenceStatisticsInc(n);
        SequenceStatisticsInc[][] stats = [[new(n), new(n)], [new(n)]];
        engine.MultiplePathValues(values, stats, Paths);
        return ([stats[0][0], stats[0][1], stats[1][0]], values);
    }

    // testGreeks' Black values: the digital caplet on the forward, its discount from the first rate time's.
    private static double Black(int i, double[] forwards, double sd)
    {
        double discount = Market.Discounts[0];
        for (int k = 0; k <= i; ++k)
        {
            discount /= 1.0 + (forwards[k] * Market.Accruals[k]);
        }

        return new BlackCalculator(new CashOrNothingPayoff(OptionType.Call, Market.Forwards[i], 0.01), forwards[i], sd, discount * Market.Accruals[i]).Value;
    }

    private static double[] Bumped(double bump) => [.. Market.Forwards.Select(f => f + bump)];

    private static FlatVol FlatVol(double[] forwards, double vol)
    {
        Matrix corr = ExpCorrelationsFunctions.ExponentialCorrelations(Evolution.RateTimes.AsSpan(), 0.5, 0.2);
        var c = new TimeHomogeneousForwardCorrelation(corr, Evolution.RateTimes.AsSpan());
        return new FlatVol([.. Enumerable.Repeat(vol, forwards.Length)], c, Evolution, 3, forwards, new double[forwards.Length]);
    }
}
