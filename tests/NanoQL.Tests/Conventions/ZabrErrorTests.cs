// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of ZABR's refusals, which the vectors record only as throws, and of the Runge-Kutta integrator
/// its model integrates with. The model, the sections and the interpolation refuse their arguments with
/// <see cref="ArgumentException"/> naming them, with QuantLib's texts: a refusal of the model's raised inside a section
/// or a fit reaches the caller as the model raises it; fewer than five parameters, which QuantLib reads past, are
/// refused naming <c>zabrParameters</c>, and an empty strike vector, which QuantLib reads past too, naming the
/// member's parameter. A negative count is <see cref="ArgumentOutOfRangeException"/>, a null context
/// <see cref="ArgumentNullException"/>, and the factory's zero value <see cref="InvalidOperationException"/>. What the
/// interpolation and the integrator can't do throws <see cref="NanoQLException"/>, with QuantLib's texts. The ZABR
/// cube's guesses and fixed flags are refused as <see cref="ArgumentException"/>s, the cube's check and the model's each
/// naming its own parameter, and a shifted at-the-money structure as an <see cref="ArgumentException"/> naming the
/// model's <c>shift</c>, reaching the cube's caller as the model raises it, where the model builds a section: at an
/// at-the-money-calibrated cube's construction, and at the first query of a cube that isn't.
/// </summary>
public class ZabrErrorTests
{
    private static readonly double[] Parameters = [0.08, 0.7, 0.2, -0.3, 1.0];

    [Fact]
    public void TheModelSidesRefusals()
    {
        // the model's arguments, in QuantLib's order of checks
        AssertArgument("alpha", "alpha must be positive: 0 not allowed", () => new ZabrModel(1.0, 0.03, 0.0, 0.7, 0.2, -0.3, 1.0));
        AssertArgument("beta", "beta must be in (0.0, 1.0): 1.5 not allowed", () => new ZabrModel(1.0, 0.03, 0.08, 1.5, 0.2, -0.3, 1.0));
        AssertArgument("nu", "nu must be non negative: -0.2 not allowed", () => new ZabrModel(1.0, 0.03, 0.08, 0.7, -0.2, -0.3, 1.0));
        AssertArgument("rho", "rho square must be less than one: 1 not allowed", () => new ZabrModel(1.0, 0.03, 0.08, 0.7, 0.2, 1.0, 1.0));
        AssertArgument("gamma", "gamma must be non negative: -0.5 not allowed", () => new ZabrModel(1.0, 0.03, 0.08, 0.7, 0.2, -0.3, -0.5));
        AssertArgument("forward", "forward must be non negative: -0.01 not allowed", () => new ZabrModel(1.0, -0.01, 0.08, 0.7, 0.2, -0.3, 1.0));
        AssertArgument("expiryTime", "expiry time must be positive: 0 not allowed", () => new ZabrModel(0.0, 0.03, 0.08, 0.7, 0.2, -0.3, 1.0));
        AssertArgument("alpha", "alpha must be positive: -1 not allowed", () => new ZabrModel(1.0, 0.03, -1.0, 0.7, 0.2, -0.3, -1.0));

        // its strikes
        var model = new ZabrModel(1.0, 0.03, 0.08, 0.7, 0.2, -0.3, 0.75);
        var lognormal = new ZabrModel(1.0, 0.03, 0.25, 1.0, 0.3, -0.2, 1.0);
        AssertArgument("strikes", "strikes must be strictly ascending (0.03,0.03)", () => model.LognormalVolatility([0.02, 0.03, 0.03]));
        AssertArgument("strikes", "strikes must be strictly ascending (0.04,0.03)", () => model.NormalVolatility([0.04, 0.03]));
        AssertArgument("f", "strikes must be strictly ascending (0.02,NaN)", () => model.LocalVolatility([0.02, double.NaN]));
        AssertArgument("strikes", "strikes must be positive (0) if beta = 1", () => lognormal.LognormalVolatility([0.0, 0.03]));
        AssertArgument("strike", "strikes must be positive (-0.01) if beta = 1", () => lognormal.NormalVolatility(-0.01));
        AssertArgument("f", "strikes must be positive (0) if beta = 1", () => lognormal.LocalVolatility(0.0));
        AssertArgument("strikes", "no strikes given", () => model.LognormalVolatility([]));
        AssertArgument("strikes", "no strikes given", () => model.NormalVolatility([]));
        AssertArgument("f", "no strikes given", () => model.LocalVolatility([]));

        // the sections
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        AssertArgument(
            "zabrParameters",
            "zabr expects 5 parameters (alpha,beta,nu,rho,gamma) but (4) given",
            () => new ZabrShortMaturityNormalSmileSection(1.0, 0.03, [0.08, 0.7, 0.2, -0.3]));
        AssertArgument("exerciseTime", "expiry time must be positive: -1 not allowed", () => new ZabrShortMaturityLognormalSmileSection(-1.0, 0.03, Parameters));
        AssertArgument("expiryTime", "expiry time must be positive: 0 not allowed", () => new ZabrShortMaturityLognormalSmileSection(0.0, 0.03, Parameters));
        AssertArgument("gamma", "gamma must be non negative: -0.5 not allowed", () => new ZabrShortMaturityNormalSmileSection(1.0, 0.03, [0.08, 0.7, 0.2, -0.3, -0.5]));
        AssertArgument(
            "d",
            "expiry date (January 14th, 2025) must be greater than reference date (January 15th, 2025)",
            () => ZabrShortMaturityNormalSmileSection.Create(new Date(14, Month.January, 2025), 0.03, Parameters, context));
        Assert.Equal(
            "fdRefinement",
            Assert.Throws<ArgumentOutOfRangeException>(() => new ZabrShortMaturityLognormalSmileSection(1.0, 0.03, Parameters, [], -1)).ParamName);
        Assert.Equal(
            "context",
            Assert.Throws<ArgumentNullException>(() => ZabrShortMaturityLognormalSmileSection.Create(new Date(15, Month.January, 2026), 0.03, Parameters, null!)).ParamName);
        Assert.Equal(
            "context",
            Assert.Throws<ArgumentNullException>(() => ZabrSmileSection<ZabrShortMaturityNormal>.Create(new Date(15, Month.January, 2026), 0.03, Parameters, null!)).ParamName);
        var section = new ZabrShortMaturityNormalSmileSection(1.0, 0.03, Parameters);
        Assert.Equal("type", Assert.Throws<ArgumentOutOfRangeException>(() => section.OptionPrice(0.03, (OptionType)0)).ParamName);
        AssertArgument(
            "strike",
            "strike + displacement (-0.01 + 0) must be non-negative",
            () => new ZabrShortMaturityLognormalSmileSection(1.0, 0.03, Parameters).OptionPrice(-0.01));

        // the interpolation and its factory
        double[] strikes = [0.02, 0.025, 0.03, 0.035, 0.04], vols = [0.25, 0.22, 0.2, 0.19, 0.195];
        AssertArgument("t", "expiry time must be positive: 0 not allowed", () => Fit(strikes, vols, 0.0, 0.03, 0.5));
        // α's default, 0.2·F^(1−β), is a NaN, which the model refuses first
        AssertArgument("alpha", "alpha must be positive: NaN not allowed", () => Fit(strikes, vols, 1.0, -0.01, 0.5));
        AssertArgument("beta", "beta must be in (0.0, 1.0): 1.5 not allowed", () => Fit(strikes, vols, 1.0, 0.03, 1.5));
        AssertArgument("x", "not enough points to interpolate: at least 1 required, 0 provided", () => Fit([], [], 1.0, 0.03, 0.5));
        Assert.Equal(
            "maxGuesses",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ZabrInterpolation<ZabrShortMaturityLognormal>(strikes, vols, 1.0, 0.03, null, 0.5, null, null, 1.0, false, true, false, false, true, maxGuesses: -1)).ParamName);
        Assert.Equal(
            "maxGuesses",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Zabr<ZabrShortMaturityNormal>(1.0, 0.03, null, 0.5, null, null, 1.0, false, true, false, false, true, maxGuesses: -1)).ParamName);
        ZabrInterpolation<ZabrShortMaturityLognormal> fit = Fit(strikes, vols, 1.0, 0.03, 0.5);
        Assert.Equal("XABR primitive not implemented", Assert.Throws<NanoQLException>(() => fit.Primitive(0.03)).Message);
        Assert.Equal("XABR derivative not implemented", Assert.Throws<NanoQLException>(() => fit.Derivative(0.03)).Message);
        Assert.Equal("XABR secondDerivative not implemented", Assert.Throws<NanoQLException>(() => fit.SecondDerivative(0.03)).Message);
        Zabr<ZabrShortMaturityLognormal> zero = default;
        Assert.Throws<InvalidOperationException>(() => zero.Interpolate(strikes, vols));
        Assert.Throws<InvalidOperationException>(() => zero.CheckPoints(5));
        var factory = new Zabr<ZabrShortMaturityLognormal>(1.0, 0.03, null, 0.5, null, null, 1.0, false, true, false, false, true, true);
        factory.CheckPoints(1);
        AssertArgument("x", "not enough points to interpolate: at least 1 required, 0 provided", () => factory.CheckPoints(0));
        Assert.Equal(fit.Evaluate(0.03), factory.Interpolate(strikes, vols).Evaluate(0.03));

        // the integrator
        var rk = new RungeKutta(1e-10, 1e-4, 0.5);
        Assert.Equal(
            "Step size (0.0005) too small (0.5 min) in AdaptiveRungeKutta",
            Assert.Throws<NanoQLException>(() => rk.Evaluate((x, y) => y, 1.0, 0.0, 5.0)).Message);
        Assert.Equal(
            "Too many steps (10000) in AdaptiveRungeKutta",
            Assert.Throws<NanoQLException>(() => new RungeKutta(1e-12).Evaluate((x, y) => y, 1.0, 0.0, 1000.0)).Message);
        Assert.StartsWith(
            "Stepsize underflow (",
            Assert.Throws<NanoQLException>(() => new RungeKutta().Evaluate((x, y) => y * y, 1.0, 0.0, 2.0)).Message,
            StringComparison.Ordinal);
        Assert.Equal(
            "the ODE function returned 1 values for 2",
            Assert.Throws<NanoQLException>(() => new RungeKutta().Evaluate((x, y) => [y[0]], [0.0, 1.0], 0.0, 1.0)).Message);
        Assert.Equal("ode", Assert.Throws<ArgumentNullException>(() => new RungeKutta().Evaluate(null!, 1.0, 0.0, 1.0)).ParamName);
    }

    [Fact]
    public void TheCubesRefusals()
    {
        // A guess row shorter than the model's five parameters: the cube's own check, before the fits.
        AssertArgument(
            "parametersGuess",
            "mismatch between number of parameters (5) and number of columns (4) in the 1st row",
            () => Cube(CubeFixture.AtmMatrix(), CubeFixture.Guesses(), [false, true, false, false, true], true));

        // Fewer than five fixed flags: the model's check, QuantLib's, naming its own parameter.
        AssertArgument(
            "paramIsFixed",
            "ZABR model requires 5 fixed flags, but 4 provided",
            () => Cube(CubeFixture.AtmMatrix(), Guesses(), [false, true, false, false], true));

        // A shifted at-the-money structure: an at-the-money-calibrated cube builds its dense sections when it is built,
        // so the model's refusal comes at construction, QuantLib's at the first query.
        AssertArgument(
            "shift",
            "ZABR model does not support shifted volatilities; shift = 0.02 was provided",
            () => Cube(CubeFixture.ShiftedAtmMatrix(0.02), Guesses(), [false, true, false, false, true], true));
    }

    // A cube that isn't at-the-money calibrated fits its sparse smiles at construction, which don't read the shift, and
    // builds a section at each query: on the wheel the shifted cube serves its 9 × 11 sparse parameters and refuses its
    // first query.
    [Fact]
    public void AShiftedCubeThatIsntAtmCalibratedRefusesAtItsFirstQuery()
    {
        ZabrSwaptionVolatilityCube cube = Cube(CubeFixture.ShiftedAtmMatrix(0.02), Guesses(), [false, true, false, false, true], false);
        Matrix sparse = cube.SparseSabrParameters();
        Assert.Equal((9, 11), (sparse.Rows, sparse.Columns));

        Period option = CubeFixture.Options[0], swap = CubeFixture.Swaps[0];
        double strike = cube.AtmStrike(option, swap, CubeFixture.Context);
        AssertArgument(
            "shift",
            "ZABR model does not support shifted volatilities; shift = 0.02 was provided",
            () => cube.Volatility(option, swap, strike, true));
        AssertArgument(
            "shift",
            "ZABR model does not support shifted volatilities; shift = 0.02 was provided",
            () => cube.SmileSection(option, swap));
    }

    private static double[][] Guesses() => [.. Enumerable.Range(0, 9).Select(_ => new[] { 0.2, 0.5, 0.4, 0.0, 1.0 })];

    private static ZabrSwaptionVolatilityCube Cube(
        SwaptionVolatilityStructure atm, IEnumerable<IEnumerable<double>> guesses, IEnumerable<bool> fixedFlags, bool isAtmCalibrated) => new(
            atm,
            CubeFixture.Options,
            CubeFixture.Swaps,
            CubeFixture.StrikeSpreads,
            CubeFixture.VolSpreads,
            CubeFixture.Index(2),
            CubeFixture.Index(1),
            false,
            guesses,
            fixedFlags,
            isAtmCalibrated,
            CubeFixture.Context);

    private static ZabrInterpolation<ZabrShortMaturityLognormal> Fit(double[] strikes, double[] vols, double t, double forward, double beta) =>
        new(strikes, vols, t, forward, null, beta, null, null, 1.0, false, true, false, false, true);

    private static void AssertArgument(string paramName, string message, Action action)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
