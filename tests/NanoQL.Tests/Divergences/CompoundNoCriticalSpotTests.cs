// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.ExoticTermStructureMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The compound engine where the daughter is a put whose largest value, K_D·D(T_M, T_D) (95.123 here), doesn't exceed
/// the mother's strike. QuantLib's Brent bracket [1e-6, 1000·K_D] holds no root and it refuses ("root not bracketed",
/// <c>analyticcompoundoptionengine.cpp:91-95</c>), where the mother is never exercised (a call on the put is worth 0)
/// or always (a put on the put is worth K_M·D(T_M) less the put today). nanoql prices those limits. The checks: the
/// limits, exact; and just inside the domain (K_M 95), QuantLib's values within Drezner's accuracy, the put on the put
/// within 0.06 of its limit at that strike.
/// </summary>
public class CompoundNoCriticalSpotTests
{
    private static readonly Date MotherMaturity = Today + 182, DaughterMaturity = Today + 547;
    private static readonly GeneralizedBlackScholesProcess Market = Flat(100.0, 0.05, 0.0, 0.25);

    [Fact]
    public void ACallOnThePutIsWorthNothing()
    {
        OneAssetOptionResults r = Compound(OptionType.Call, 99.0);
        Assert.Equal(0.0, r.NPV);
        Assert.Equal(0.0, r.Delta);
        Assert.Equal(0.0, r.Gamma);
        Assert.Equal(0.0, r.Vega);
        Assert.Equal(0.0, r.Theta);

        // just inside the domain, QuantLib's 0.0
        Assert.Equal(0.0, Compound(OptionType.Call, 95.0).NPV!.Value, 1e-12);
    }

    [Fact]
    public void APutOnThePutIsTheDiscountedStrikeLessThePut()
    {
        double put = AnalyticEuropeanEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(DaughterMaturity)),
            Context,
            Market).NPV!.Value;
        double discount = Market.RiskFreeRate.Discount(MotherMaturity);
        Assert.Equal((99.0 * discount) - put, Compound(OptionType.Put, 99.0).NPV!.Value, 1e-12);

        // just inside the domain, QuantLib's 84.2007400410897, near the limit at that strike
        double inside = Compound(OptionType.Put, 95.0).NPV!.Value;
        Assert.Equal(84.2007400410897, inside, 6e-7 * (100.0 + 100.0 + 95.0));
        Assert.Equal((95.0 * discount) - put, inside, 0.06);
    }

    private static OneAssetOptionResults Compound(OptionType mother, double strikeMother) =>
        AnalyticCompoundOptionEngine.Calculate(
            new CompoundOption(
                new PlainVanillaPayoff(mother, strikeMother), new EuropeanExercise(MotherMaturity),
                new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(DaughterMaturity)),
            Context,
            Market);
}
