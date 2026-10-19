// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's swaption volatility cube test fixture (<c>test-suite/swaptionvolatilitycube.cpp</c>'s <c>CommonVars</c>
/// on <c>swaptionvolstructuresutilities.hpp</c>'s data) on Monday 16 June 2025, the date the cube vectors use, for the
/// cubes' divergence and convention tests: the at-the-money matrix floating on the evaluation date, the 3 x 3 cube's
/// tenors, strike spreads and volatility spreads, and EuriborSwapIsdaFixA 2Y and 1Y on a flat 5% curve.
/// </summary>
internal static class CubeFixture
{
    public static readonly Date Today = new(16, Month.June, 2025);

    public static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    public static readonly Period[] AtmOptions =
    [
        new(1, TimeUnit.Months), new(6, TimeUnit.Months), new(1, TimeUnit.Years), new(5, TimeUnit.Years),
        new(10, TimeUnit.Years), new(30, TimeUnit.Years),
    ];

    public static readonly Period[] AtmSwaps =
    [
        new(1, TimeUnit.Years), new(5, TimeUnit.Years), new(10, TimeUnit.Years), new(30, TimeUnit.Years),
    ];

    public static readonly Period[] Options = [new(1, TimeUnit.Years), new(10, TimeUnit.Years), new(30, TimeUnit.Years)];

    public static readonly Period[] Swaps = [new(2, TimeUnit.Years), new(10, TimeUnit.Years), new(30, TimeUnit.Years)];

    public static readonly double[] StrikeSpreads = [-0.020, -0.005, +0.000, +0.005, +0.020];

    public static readonly double[][] VolSpreads =
    [
        [0.0599, 0.0049, 0.0000, -0.0001, 0.0127],
        [0.0729, 0.0086, 0.0000, -0.0024, 0.0098],
        [0.0738, 0.0102, 0.0000, -0.0039, 0.0065],
        [0.0465, 0.0063, 0.0000, -0.0032, -0.0010],
        [0.0558, 0.0084, 0.0000, -0.0050, -0.0057],
        [0.0576, 0.0083, 0.0000, -0.0043, -0.0014],
        [0.0437, 0.0059, 0.0000, -0.0030, -0.0006],
        [0.0533, 0.0078, 0.0000, -0.0045, -0.0046],
        [0.0545, 0.0079, 0.0000, -0.0042, -0.0020],
    ];

    public static readonly bool[] BetaFixed = [false, true, false, false];

    private static readonly Matrix AtmVols = new(new[,]
    {
        { 0.1300, 0.1560, 0.1390, 0.1220 },
        { 0.1440, 0.1580, 0.1460, 0.1260 },
        { 0.1600, 0.1590, 0.1470, 0.1290 },
        { 0.1640, 0.1470, 0.1370, 0.1220 },
        { 0.1400, 0.1300, 0.1250, 0.1100 },
        { 0.1130, 0.1090, 0.1070, 0.0930 },
    });

    /// <summary>The at-the-money matrix floating on the context's evaluation date.</summary>
    /// <param name="context">The context, by default the fixture's.</param>
    /// <returns>The matrix.</returns>
    public static SwaptionVolatilityMatrix AtmMatrix(EvaluationContext? context = null) => SwaptionVolatilityMatrix.Create(
        new TARGET(), BusinessDayConvention.ModifiedFollowing, AtmOptions, AtmSwaps, AtmVols, new Actual365Fixed(), context ?? Context);

    /// <summary>The at-the-money matrix with a fixed reference date.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <returns>The matrix.</returns>
    public static SwaptionVolatilityMatrix AtmMatrix(Date referenceDate) => new(
        referenceDate, new TARGET(), BusinessDayConvention.ModifiedFollowing, AtmOptions, AtmSwaps, AtmVols, new Actual365Fixed());

    /// <summary>The swap index of a tenor on the flat 5% curve of the context's evaluation date.</summary>
    /// <param name="years">The tenor, in years.</param>
    /// <param name="context">The context, by default the fixture's.</param>
    /// <returns>The index.</returns>
    public static SwapIndex Index(int years, EvaluationContext? context = null) =>
        new EuriborSwapIsdaFixA(years * TimeUnit.Years, FlatForward.Create(0, new NullCalendar(), 0.05, new Actual365Fixed(), context ?? Context));

    /// <summary>The fixture's guesses, a row of α, β, ν and ρ per option and swap tenor pair.</summary>
    /// <param name="rows">The number of rows.</param>
    /// <returns>The guesses.</returns>
    public static double[][] Guesses(int rows = 9) => [.. Enumerable.Range(0, rows).Select(_ => new[] { 0.2, 0.5, 0.4, 0.0 })];

    /// <summary>A SABR cube on the fixture.</summary>
    /// <param name="atm">The at-the-money structure, by default the floating matrix.</param>
    /// <param name="guesses">The guesses, by default the fixture's.</param>
    /// <param name="fixedFlags">The fixed flags, by default every parameter free.</param>
    /// <param name="isAtmCalibrated">Whether the cube is at-the-money calibrated.</param>
    /// <returns>The cube.</returns>
    public static SabrSwaptionVolatilityCube Sabr(
        SwaptionVolatilityStructure? atm = null,
        IEnumerable<IEnumerable<double>>? guesses = null,
        IEnumerable<bool>? fixedFlags = null,
        bool isAtmCalibrated = true) => new(
            atm ?? AtmMatrix(),
            Options,
            Swaps,
            StrikeSpreads,
            VolSpreads,
            Index(2),
            Index(1),
            false,
            guesses ?? Guesses(),
            fixedFlags ?? [false, false, false, false],
            isAtmCalibrated,
            Context);

    /// <summary>An interpolated cube on the fixture.</summary>
    /// <param name="atm">The at-the-money structure, by default the floating matrix.</param>
    /// <param name="context">The context it is built under, by default the fixture's.</param>
    /// <returns>The cube.</returns>
    public static InterpolatedSwaptionVolatilityCube Interpolated(SwaptionVolatilityStructure? atm = null, EvaluationContext? context = null) =>
        new(atm ?? AtmMatrix(), Options, Swaps, StrikeSpreads, VolSpreads, Index(2), Index(1), false, context ?? Context);
}
