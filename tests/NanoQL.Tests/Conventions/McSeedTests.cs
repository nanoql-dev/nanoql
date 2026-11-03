// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Monte Carlo vanilla engines' seeds: each seed explicit, in [1, 4294967295], QuantLib's 0, a seed drawn from the
/// clock, refused, naming the parameter; and the Longstaff-Schwartz calibration seed's default, (seed + 1768237423) mod
/// 2³², refused where it wraps to 0, at the pricing seed 2,526,729,873.
/// </summary>
public class McSeedTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        36.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.06, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));

    private static readonly VanillaOption Put = new(new PlainVanillaPayoff(OptionType.Put, 40.0), new AmericanExercise(Today, Today + 365));

    [Fact]
    public void SeedZeroIsRefused()
    {
        AssertSeedRefused("seed", () => new MakeMCEuropeanEngine<PseudoRandom, GeneralStatistics>(Process, 0));
        AssertSeedRefused("seed", () => new MakeMCEuropeanEngine<PseudoRandom, GeneralStatistics>(Process, 1).WithSeed(0));
        AssertSeedRefused("seed", () => new MakeMCDigitalEngine<PseudoRandom, GeneralStatistics>(Process, 0));
        AssertSeedRefused("seed", () => new MakeMCDigitalEngine<PseudoRandom, GeneralStatistics>(Process, 1).WithSeed(0));
        AssertSeedRefused("seed", () => new MakeMCAmericanEngine<PseudoRandom, GeneralStatistics, PseudoRandom>(Process, 0));
        AssertSeedRefused("seed", () => new MakeMCAmericanEngine<PseudoRandom, GeneralStatistics, PseudoRandom>(Process, 1).WithSeed(0));
        AssertSeedRefused(
            "seed", () => new MakeMCAmericanEngine<PseudoRandom, GeneralStatistics, PseudoRandom>(Process, 1).WithSeedCalibration(0));
        AssertSeedRefused("seed", () => MCEuropeanEngine.Calculate<PseudoRandom, GeneralStatistics>(
            Put, Context, Process, 10, null, false, false, 100, null, null, 0));
        AssertSeedRefused("seedCalibration", () => American(1, 0));
    }

    [Fact]
    public void TheWrappedDefaultCalibrationSeedIsRefused()
    {
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => American(2526729873, null));
        Assert.Equal("seedCalibration", e.ParamName);
        Assert.StartsWith(
            "the default calibration seed, (seed + 1768237423) mod 2^32, is 0 for seed 2526729873, which QuantLib reads as a "
            + "seed drawn from the clock: give the calibration seed",
            e.Message);

        // the seed beside it wraps to 1, and a given calibration seed is used as given
        Assert.NotNull(American(2526729874, null).NPV);
        Assert.NotNull(American(2526729873, 7).NPV);
    }

    private static OneAssetOptionResults American(long seed, long? seedCalibration) =>
        MCAmericanEngine.Calculate<PseudoRandom, GeneralStatistics, PseudoRandom>(
            Put, Context, Process, 10, null, false, false, 100, null, null, seed, 2, LsmBasisSystemPolynomialType.Monomial,
            seedCalibration: seedCalibration);

    private static void AssertSeedRefused(string paramName, Action act)
    {
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith("seed 0, which QuantLib reads as a seed drawn from the clock, is refused", e.Message);
    }
}
