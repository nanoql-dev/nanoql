// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using static NanoQL.Models.MarkovFunctionalModelSettingsAdjustments;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Markov-functional model's settings: QuantLib's chained setters return copies, and
/// <see cref="MarkovFunctionalModelSettings.Validate"/> completes the flags (Kahale's interpolation implies Kahale's
/// smile; Kahale's smile with the deletion of arbitrage points implies Kahale's interpolation) and refuses QuantLib's
/// cases, each with QuantLib's text (<c>markovfunctional.hpp:152-199</c>), as <see cref="NanoQLException"/>: the
/// settings' own state, as a model's constructor raises it; the grid's minimum and its text QuantLib's master's, two
/// points (#2912, <c>Divergences/MarkovFunctionalUpstreamFixesTests.cs</c>).
/// </summary>
public class MarkovFunctionalSettingsTests
{
    [Fact]
    public void ValidateRefusesQuantLibsCases()
    {
        var s = new MarkovFunctionalModelSettings();
        (MarkovFunctionalModelSettings Settings, string Message)[] cases =
        [
            (s.WithYGridPoints(1), "At least two grid points (1) for the state process discretization must be given"),
            (s.WithYStdDevs(0.0), "Multiple of standard deviations covered by state process discretization (0) must be positive"),
            (s.WithGaussHermitePoints(0), "Number of gauss hermite integration points (0) must be positive"),
            (s.WithDigitalGap(-1e-5), "Digital gap (-1E-05) must be positive"),
            (s.WithMarketRateAccuracy(0.0), "Market rate accuracy (0) must be positive"),
            (s.WithLowerRateBound(0.01), "If Kahale extrapolation is used, the lower rate bound (0.01) must be zero."),
            (s.WithAdjustments(AdjustNone).WithLowerRateBound(0.5).WithUpperRateBound(0.5), "Lower rate bound (0.5) must be strictly less than upper rate bound (0.5)"),
            (s.WithAdjustments(CustomSmile), "missing CustomSmileFactoy"),
        ];
        foreach ((MarkovFunctionalModelSettings settings, string message) in cases)
        {
            NanoQLException e = Assert.Throws<NanoQLException>(() => settings.Validate());
            Assert.Equal(message, e.Message);
        }
    }

    [Fact]
    public void ValidateCompletesTheFlagsAndTheSettersCopy()
    {
        var s = new MarkovFunctionalModelSettings();
        Assert.Equal(KahaleSmile | SmileExponentialExtrapolation, s.Adjustments);
        Assert.Equal(KahaleSmile | KahaleInterpolation, s.WithAdjustments(KahaleInterpolation).Validate().Adjustments);
        Assert.Equal(
            KahaleSmile | KahaleInterpolation | SmileDeleteArbitragePoints,
            s.WithAdjustments(KahaleSmile | SmileDeleteArbitragePoints).Validate().Adjustments);

        MarkovFunctionalModelSettings changed = s.WithYGridPoints(16).AddAdjustment(AdjustYts).RemoveAdjustment(SmileExponentialExtrapolation);
        Assert.Equal(64, s.YGridPoints);
        Assert.Equal(16, changed.YGridPoints);
        Assert.Equal(KahaleSmile | AdjustYts, changed.Adjustments);
    }
}
