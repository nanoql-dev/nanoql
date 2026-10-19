// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the SABR functions', the SABR fit's and the smile sections' refusals, which the vectors
/// record only as throws: an argument out of its range is <see cref="ArgumentException"/> naming it, with QuantLib's
/// text; a volatility type outside its enum and a negative <c>maxGuesses</c> are
/// <see cref="ArgumentOutOfRangeException"/>; a null section is <see cref="ArgumentNullException"/>; and what an XABR
/// interpolation can't give, a primitive or a derivative, is <see cref="NanoQLException"/>. The refusals of QuantLib's
/// undefined inputs (fewer than four SABR parameters, more strikes than standard deviations, a non-finite guess's
/// argument, an all-zero smile with vega weights) take the same types.
/// </summary>
public class SabrErrorTests
{
    private static readonly double[] Strikes = [0.03, 0.035, 0.04];
    private static readonly double[] Vols = [0.21, 0.19, 0.2];

    [Fact]
    public void TheSabrRefusals()
    {
        AssertArgument("alpha", "alpha must be positive: -0.1 not allowed", () => SabrFunctions.ValidateSabrParameters(-0.1, 0.5, 0.3, 0.0));
        AssertArgument("beta", "beta must be in (0.0, 1.0): 1.5 not allowed", () => SabrFunctions.ValidateSabrParameters(0.1, 1.5, 0.3, 0.0));
        AssertArgument("nu", "nu must be non negative: -0.2 not allowed", () => SabrFunctions.ValidateSabrParameters(0.1, 0.5, -0.2, 0.0));
        AssertArgument("rho", "rho square must be less than one: 1 not allowed", () => SabrFunctions.ValidateSabrParameters(0.1, 0.5, 0.3, 1.0));
        AssertArgument("rho", "rho square must be less than one: NaN not allowed", () => SabrFunctions.ValidateSabrParameters(0.1, 0.5, 0.3, double.NaN));
        AssertArgument(
            "strike",
            "strike must be positive: -1.000000 % not allowed",
            () => SabrFunctions.SabrVolatility(-0.01, 0.03, 1.0, 0.1, 0.5, 0.3, 0.0));
        AssertArgument(
            "forward",
            "at the money forward rate must be positive: 0.000000 % not allowed",
            () => SabrFunctions.SabrVolatility(0.01, 0.0, 1.0, 0.1, 0.5, 0.3, 0.0));
        AssertArgument(
            "expiryTime",
            "expiry time must be non-negative: -1 not allowed",
            () => SabrFunctions.SabrVolatility(0.01, 0.03, -1.0, 0.1, 0.5, 0.3, 0.0));
        AssertArgument(
            "strike",
            "strike+shift must be positive: -2.000000 %+1.000000 % not allowed",
            () => SabrFunctions.ShiftedSabrVolatility(-0.02, 0.03, 1.0, 0.1, 0.5, 0.3, 0.0, 0.01));
        AssertArgument(
            "forward",
            "at the money forward rate + shift must be positive: -3.000000 % 1.000000 % not allowed",
            () => SabrFunctions.ShiftedSabrVolatility(0.01, -0.03, 1.0, 0.1, 0.5, 0.3, 0.0, 0.01));
        ArgumentOutOfRangeException type = Assert.Throws<ArgumentOutOfRangeException>(() => SabrFunctions.SabrGuess(
            0.03, 0.21, 0.035, 0.19, 0.04, 0.2, 0.035, 1.0, 0.5, 0.0, (VolatilityType)2));
        Assert.Equal("volatilityType", type.ParamName);
        Assert.StartsWith("unknown volatility type: 2", type.Message, StringComparison.Ordinal);
        AssertArgument(
            "k_0", "k_0 must be finite: NaN not allowed",
            () => SabrFunctions.SabrGuess(0.03, 0.21, double.NaN, 0.19, 0.04, 0.2, 0.035, 1.0, 0.5, 0.0, VolatilityType.Normal));

        // The fit.
        AssertArgument("t", "expiry time must be positive: 0 not allowed", () => Fit(t: 0.0));
        AssertArgument("forward", "forward+shift must be positive: -0.01 with shift 0 not allowed", () => Fit(forward: -0.01));
        AssertArgument("alpha", "alpha must be positive: -0.1 not allowed", () => Fit(alpha: -0.1));
        AssertArgument(
            "strike", "strike+shift must be positive: -1.000000 %+0.000000 % not allowed", () => Fit().Evaluate(-0.01, true));
        Assert.Equal("maxGuesses", Assert.Throws<ArgumentOutOfRangeException>(() => Fit(maxGuesses: -1)).ParamName);
        AssertArgument("y", "vega weights are all zero", () => new SABRInterpolation(
            Strikes, [0.0, 0.0, 0.0], 1.0, 0.035, null, 0.5, null, null, false, true, false, false));
        SABRInterpolation fit = Fit();
        Assert.Equal("XABR primitive not implemented", Assert.Throws<NanoQLException>(() => fit.Primitive(0.035)).Message);
        Assert.Equal("XABR derivative not implemented", Assert.Throws<NanoQLException>(() => fit.Derivative(0.035)).Message);
        Assert.Equal(
            "XABR secondDerivative not implemented", Assert.Throws<NanoQLException>(() => fit.SecondDerivative(0.035)).Message);

        // The sections.
        AssertArgument(
            "sabrParameters",
            "sabr expects 4 parameters (alpha,beta,nu,rho) but (2) given",
            () => new SabrSmileSection(1.0, 0.035, [0.1, 0.5]));
        AssertArgument(
            "forward",
            "at the money forward rate + shift must be positive: -1.000000 % with shift 0.000000 % not allowed",
            () => new SabrSmileSection(1.0, -0.01, [0.1, 0.5, 0.3, 0.0]));
        AssertArgument("nu", "nu must be non negative: -0.3 not allowed", () => new SabrSmileSection(1.0, 0.035, [0.1, 0.5, -0.3, 0.0]));
        Assert.Equal(
            "context",
            Assert.Throws<ArgumentNullException>(
                () => SabrSmileSection.Create(new Time.Date(1, Time.Month.June, 2026), 0.035, [0.1, 0.5, 0.3, 0.0], null!)).ParamName);
        AssertArgument(
            "strikes",
            "Strikes have to be sorted in ascending order",
            () => new LinearInterpolatedSmileSection(1.0, [0.03, 0.02], [0.2, 0.2], 0.025));
        AssertArgument(
            "strikes",
            "3 strikes given for 2 standard deviations",
            () => new LinearInterpolatedSmileSection(1.0, [0.01, 0.02, 0.03], [0.2, 0.2], 0.025));
        Assert.Equal(
            "underlyingSection", Assert.Throws<ArgumentNullException>(() => new SpreadedSmileSection(null!, 0.01)).ParamName);
    }

    [Fact]
    public void FewerVolatilitiesThanStrikesReachTheInterpolationsCheck()
    {
        // QuantLib's fit reads one volatility per strike, past the given ones (the plan's XD5); S2's base refuses.
        AssertArgument(
            "y",
            "2 y values given for 3 x values",
            () => new SABRInterpolation(Strikes, [0.21, 0.19], 1.0, 0.035, null, 0.5, null, null, false, true, false, false));
    }

    private static SABRInterpolation Fit(
        double t = 1.0, double forward = 0.035, double? alpha = null, int maxGuesses = 50) =>
        new(Strikes, Vols, t, forward, alpha, 0.5, null, null, false, true, false, false, maxGuesses: maxGuesses);

    private static void AssertArgument(string paramName, string message, Action call)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(call);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }

    private static void AssertArgument(string paramName, string message, Func<object> call) =>
        AssertArgument(paramName, message, () => { _ = call(); });
}
