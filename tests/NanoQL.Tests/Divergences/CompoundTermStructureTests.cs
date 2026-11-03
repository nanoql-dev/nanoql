// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.ExoticTermStructureMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The compound engine on term structures. QuantLib solves its critical spot on the volatility at a date as far from
/// the curves' start as the daughter's maturity from the mother's and on the discounts over that span, then reads the
/// daughter's volatility at its maturity and the mother's at the mother's strike
/// (<c>analyticcompoundoptionengine.cpp:73-95</c>, <c>:185-188</c>, <c>:258-262</c>), so on a rising market a put on a
/// call is +1,090% off and its Greeks miss its own bumped values by −45% (delta) to +2,908% (vega); nanoql reads
/// Geske's formula with the variances to each maturity at the daughter's strike, the correlation √(V_M/V_D) and the
/// discounts by date, its critical spot solving the daughter's forward Black value over the mother's period, on West's
/// bivariate normal, and takes the form's own Greeks. The checks: the option's exact value at the mother's maturity,
/// the mother's payoff on the daughter's Black value over the lognormal spot, within Brent's accuracy, 1e-6; on a flat
/// market, QuantLib's value, delta and theta within Drezner's accuracy, the value nearer the exact one, and its gamma
/// and vega, which read no bivariate normal, within its last bits; and on rising and falling markets, the delta and
/// gamma against bumps of the spot, the vega against a parallel shift of the volatilities and the theta against the
/// evaluation date moved with each date's zero rate and volatility held.
/// </summary>
public class CompoundTermStructureTests
{
    private const double KM = 4.0, KD = 100.0;
    private static readonly Date MotherMaturity = Today + 90, DaughterMaturity = Today + 365;

    private static readonly (OptionType Mother, OptionType Daughter)[] Types =
    [
        (OptionType.Call, OptionType.Call), (OptionType.Put, OptionType.Call),
        (OptionType.Call, OptionType.Put), (OptionType.Put, OptionType.Put),
    ];

    [Fact]
    public void ARisingMarketMeetsTheExactValue()
    {
        CheckValues(Rising(100.0));
    }

    [Fact]
    public void AFallingMarketMeetsTheExactValue()
    {
        CheckValues(Falling(100.0));
    }

    [Fact]
    public void AFlatMarketIsQuantLibsWithinDreznersAccuracy()
    {
        GeneralizedBlackScholesProcess p = Flat(100.0);
        CheckValues(p);

        // QuantLib's engine on the flat market (r 3%, q 0, σ 25%): value, delta, gamma, vega and theta
        double[][] quantLib =
        [
            [7.589462435876751, 0.5653834689817602, 0.019403278041753107, 37.80356014456242, -7.531990921916828],
            [0.21150573920538407, -0.031388315338764294, 0.003917401450051266, -0.9111313346921855, -1.1236778349485663],
            [4.720859034140798, -0.3629112307265126, 0.01909961093960718, 35.822220532585675, -4.738268955423479],
            [0.29834898261861753, 0.040316984952962855, 0.0036137343479053403, -2.89247094666892, -1.241292469100749],
        ];
        double dreznerValue = 6e-7 * (100.0 + KD + KM);
        for (int i = 0; i < Types.Length; ++i)
        {
            OneAssetOptionResults r = Compound(Types[i].Mother, Types[i].Daughter, p);
            double[] q = quantLib[i];
            Assert.Equal(q[0], r.NPV!.Value, dreznerValue);
            double exact = ExactValue(p, Types[i].Mother, Types[i].Daughter);
            Assert.True(Math.Abs(r.NPV!.Value - exact) <= Math.Abs(q[0] - exact));
            Assert.Equal(q[1], r.Delta!.Value, 6e-7);
            Assert.Equal(q[2], r.Gamma!.Value, 1e-12 * Math.Abs(q[2]));
            Assert.Equal(q[3], r.Vega!.Value, 1e-12 * Math.Abs(q[3]));
            Assert.Equal(q[4], r.Theta!.Value, 0.03 * dreznerValue);
        }
    }

    [Fact]
    public void TheGreeksOnARisingMarketMeetBumps()
    {
        CheckGreeks(Rising, Rising);
    }

    [Fact]
    public void TheGreeksOnAFallingMarketMeetBumps()
    {
        CheckGreeks(Falling, Falling);
    }

    private static void CheckValues(GeneralizedBlackScholesProcess p)
    {
        foreach ((OptionType mother, OptionType daughter) in Types)
        {
            double exact = ExactValue(p, mother, daughter);
            Assert.Equal(exact, Compound(mother, daughter, p).NPV!.Value, 1e-6 * exact);
        }
    }

    private static void CheckGreeks(
        Func<Real, GeneralizedBlackScholesProcess> market,
        Func<Real, Date, double, GeneralizedBlackScholesProcess> shifted)
    {
        const double spot = 100.0;
        foreach ((OptionType mother, OptionType daughter) in Types)
        {
            OneAssetOptionResults r = Compound(mother, daughter, market(spot));

            double du = spot * 1.0e-4;
            OneAssetOptionResults up = Compound(mother, daughter, market(spot + du));
            OneAssetOptionResults down = Compound(mother, daughter, market(spot - du));
            double delta = (up.NPV!.Value - down.NPV!.Value) / (2 * du);
            double gamma = (up.Delta!.Value - down.Delta!.Value) / (2 * du);

            const double dv = 1.0e-4;
            double vega = (Compound(mother, daughter, shifted(spot, Today, dv)).NPV!.Value
                - Compound(mother, daughter, shifted(spot, Today, -dv)).NPV!.Value) / (2 * dv);

            var yesterday = new EvaluationContext { EvaluationDate = Today - 1 };
            var tomorrow = new EvaluationContext { EvaluationDate = Today + 1 };
            double dT = A365.YearFraction(Today - 1, Today + 1);
            double theta = (Compound(mother, daughter, shifted(spot, Today + 1, 0.0), tomorrow).NPV!.Value
                - Compound(mother, daughter, shifted(spot, Today - 1, 0.0), yesterday).NPV!.Value) / dT;

            Assert.Equal(delta, r.Delta!.Value, 1e-5);
            Assert.Equal(gamma, r.Gamma!.Value, 1e-5);
            Assert.Equal(vega, r.Vega!.Value, 1e-3);
            Assert.Equal(theta, r.Theta!.Value, 1e-3);
        }
    }

    private static double ExactValue(GeneralizedBlackScholesProcess p, OptionType mother, OptionType daughter)
    {
        double Daughter(double s) => ForwardBlack(p, daughter, KD, MotherMaturity, DaughterMaturity, s);
        double kink = Kink(s => Daughter(s) - KM, 1e-3, 1e5);
        return Exact(p, MotherMaturity, s => Math.Max((double)mother * (Daughter(s) - KM), 0.0), kink);
    }

    private static OneAssetOptionResults Compound(
        OptionType mother, OptionType daughter, GeneralizedBlackScholesProcess p, EvaluationContext? context = null) =>
        AnalyticCompoundOptionEngine.Calculate(
            new CompoundOption(
                new PlainVanillaPayoff(mother, KM), new EuropeanExercise(MotherMaturity), new PlainVanillaPayoff(daughter, KD),
                new EuropeanExercise(DaughterMaturity)),
            context ?? Context,
            p);
}
