// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the swaption volatility cubes' refusals, which the vectors record only as throws: a null
/// argument is <see cref="ArgumentNullException"/>, an argument out of its range <see cref="ArgumentException"/> naming
/// it, with QuantLib's text, a negative <c>maxGuesses</c> <see cref="ArgumentOutOfRangeException"/>, and a calibration
/// that fails <see cref="NanoQLException"/> with QuantLib's text, at construction where QuantLib refuses at the first
/// query. QuantLib's calculation checks a cube's strike count; here the constructor checks it.
/// </summary>
public class CubeErrorTests
{
    [Fact]
    public void TheCubesRefusals()
    {
        var atm = CubeFixture.AtmMatrix();
        Assert.Equal(
            "atmVolStructure",
            Assert.Throws<ArgumentNullException>(() => new SabrSwaptionVolatilityCube(
                null!, CubeFixture.Options, CubeFixture.Swaps, CubeFixture.StrikeSpreads, CubeFixture.VolSpreads,
                CubeFixture.Index(2), CubeFixture.Index(1), false, CubeFixture.Guesses(), [false, false, false, false],
                false, CubeFixture.Context)).ParamName);
        Assert.Equal(
            "swapIndexBase",
            Assert.Throws<ArgumentNullException>(() => new InterpolatedSwaptionVolatilityCube(
                atm, CubeFixture.Options, CubeFixture.Swaps, CubeFixture.StrikeSpreads, CubeFixture.VolSpreads, null!,
                CubeFixture.Index(1), false, CubeFixture.Context)).ParamName);
        Assert.Equal(
            "shortSwapIndexBase",
            Assert.Throws<ArgumentNullException>(() => new InterpolatedSwaptionVolatilityCube(
                atm, CubeFixture.Options, CubeFixture.Swaps, CubeFixture.StrikeSpreads, CubeFixture.VolSpreads,
                CubeFixture.Index(2), null!, false, CubeFixture.Context)).ParamName);
        Assert.Equal(
            "context",
            Assert.Throws<ArgumentNullException>(() => new InterpolatedSwaptionVolatilityCube(
                atm, CubeFixture.Options, CubeFixture.Swaps, CubeFixture.StrikeSpreads, CubeFixture.VolSpreads,
                CubeFixture.Index(2), CubeFixture.Index(1), false, null!)).ParamName);

        AssertArgument(
            "strikeSpreads",
            "non increasing strike spreads: 2nd is -0.005, 3rd is -0.005",
            () => Interpolated(strikeSpreads: [-0.020, -0.005, -0.005, 0.005, 0.020]));
        AssertArgument("volSpreads", "empty vol spreads matrix", () => Interpolated(volSpreads: []));
        AssertArgument(
            "volSpreads",
            "mismatch between number of option tenors * swap tenors (9) and number of rows (8)",
            () => Interpolated(volSpreads: CubeFixture.VolSpreads[..8]));
        AssertArgument(
            "volSpreads",
            "mismatch between number of strikes (5) and number of columns (4) in the 2nd row",
            () => Interpolated(volSpreads: [CubeFixture.VolSpreads[0], CubeFixture.VolSpreads[1][..4], .. CubeFixture.VolSpreads[2..]]));
        AssertArgument(
            "shortSwapIndexBase",
            "short index tenor (2Y) is not less or equal than index tenor (1Y)",
            () => new InterpolatedSwaptionVolatilityCube(
                atm, CubeFixture.Options, CubeFixture.Swaps, CubeFixture.StrikeSpreads, CubeFixture.VolSpreads,
                CubeFixture.Index(1), CubeFixture.Index(2), false, CubeFixture.Context));
        Assert.Equal(
            "maxGuesses",
            Assert.Throws<ArgumentOutOfRangeException>(() => new SabrSwaptionVolatilityCube(
                atm, CubeFixture.Options, CubeFixture.Swaps, CubeFixture.StrikeSpreads, CubeFixture.VolSpreads,
                CubeFixture.Index(2), CubeFixture.Index(1), false, CubeFixture.Guesses(), [false, false, false, false],
                false, CubeFixture.Context, maxGuesses: -1)).ParamName);

        // An at-the-money-calibrated cube on a structure that isn't discrete.
        var constant = new ConstantSwaptionVolatility(
            CubeFixture.Today, new TARGET(), BusinessDayConvention.ModifiedFollowing, 0.20, new Actual365Fixed());
        AssertArgument(
            "atmVolStructure",
            "isAtmCalibrated requires an ATM volatility structure derived from SwaptionVolatilityDiscrete",
            () => CubeFixture.Sabr(atm: constant));

        // A calibration beyond the tolerance, QuantLib's text, at construction.
        NanoQLException failed = Assert.Throws<NanoQLException>(() => new SabrSwaptionVolatilityCube(
            atm, CubeFixture.Options, CubeFixture.Swaps, CubeFixture.StrikeSpreads, CubeFixture.VolSpreads,
            CubeFixture.Index(2), CubeFixture.Index(1), false, CubeFixture.Guesses(), [false, false, false, false],
            false, CubeFixture.Context, maxErrorTolerance: 1e-12));
        Assert.StartsWith(
            "global swaptions calibration failed: error tolerance exceeded: \nusing rmsError tolerance 1e-12, \n",
            failed.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheRequiredNumberOfStrikesIsCheckedAtConstruction()
    {
        // QuantLib checks it in performCalculations, at the first query; the interpolated cube requires 2, the SABR cube 1.
        AssertArgument(
            "strikeSpreads",
            "too few strikes (1) required are at least 2",
            () => Interpolated(strikeSpreads: [0.0], volSpreads: [.. CubeFixture.VolSpreads.Select(r => new[] { r[2] })]));
        var oneStrike = new SabrSwaptionVolatilityCube(
            CubeFixture.AtmMatrix(), CubeFixture.Options, CubeFixture.Swaps, [0.0], [.. CubeFixture.VolSpreads.Select(r => new[] { r[2] })],
            CubeFixture.Index(2), CubeFixture.Index(1), false, CubeFixture.Guesses(), [false, true, true, true], false,
            CubeFixture.Context);
        Assert.Equal(9, oneStrike.SparseSabrParameters().Rows);
    }

    private static InterpolatedSwaptionVolatilityCube Interpolated(
        double[]? strikeSpreads = null, double[][]? volSpreads = null) => new(
            CubeFixture.AtmMatrix(),
            CubeFixture.Options,
            CubeFixture.Swaps,
            strikeSpreads ?? CubeFixture.StrikeSpreads,
            volSpreads ?? CubeFixture.VolSpreads,
            CubeFixture.Index(2),
            CubeFixture.Index(1),
            false,
            CubeFixture.Context);

    private static void AssertArgument(string paramName, string message, Func<object> call)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => { _ = call(); });
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
