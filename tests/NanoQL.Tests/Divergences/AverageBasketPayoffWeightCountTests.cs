// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using static NanoQL.Tests.Divergences.Fd2dMismatchedTimesTests;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The average basket payoff on a number of prices other than its weights'. QuantLib's <c>inner_product</c> runs over
/// the weights and reads the prices unchecked (<c>basketoption.hpp:77-81</c>): on two assets, three weights read past
/// the shorter array, so its Monte Carlo and finite-difference engines price a run-dependent value (the 2-D engine
/// 2.4177 on one run, the Monte Carlo 11.902 and 11.896 on two), and one weight prices asset 1 alone (the 2-D engine
/// 13.1679), where its Choi, Deng-Li-Zhou and single-factor engines refuse ("wrong number of weights
/// arguments in payoff", <c>choibasketengine.cpp:97-98</c>). nanoql's payoff refuses a price count other than its
/// weights' with that text: QuantLib's own check, everywhere.
/// </summary>
public class AverageBasketPayoffWeightCountTests
{
    [Fact]
    public void MoreWeightsThanAssetsAreRefused()
    {
        var payoff = new AverageBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0), [0.5, 0.3, 0.2]);
        ArgumentException e = Assert.Throws<ArgumentException>(() => Price(payoff));
        Assert.Equal("wrong number of weights arguments in payoff (Parameter 'a')", e.Message);
        Assert.Equal("a", e.ParamName);
    }

    [Fact]
    public void FewerWeightsThanAssetsAreRefused()
    {
        var payoff = new AverageBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0), [1.0]);
        ArgumentException e = Assert.Throws<ArgumentException>(() => Price(payoff));
        Assert.Equal("a", e.ParamName);
        Assert.Throws<ArgumentException>(() => payoff.Evaluate([100.0, 95.0]));
    }

    private static double Price(BasketPayoff payoff) =>
        Fd2dBlackScholesVanillaEngine.Calculate(
            new BasketOption(payoff, European()),
            new EvaluationContext { EvaluationDate = new Time.Date(15, Time.Month.October, 2026) },
            Process(100.0, Flat(0.0), Flat(0.03), Vol(0.3)),
            Process(100.0, Flat(0.0), Flat(0.03), Vol(0.2)),
            0.5,
            21,
            21,
            20).NPV!.Value;
}
