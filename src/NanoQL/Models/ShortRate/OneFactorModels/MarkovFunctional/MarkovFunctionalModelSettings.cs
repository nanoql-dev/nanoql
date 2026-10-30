// SPDX-FileCopyrightText: Copyright (C) 2013, 2018 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/markovfunctional.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using static NanoQL.Models.MarkovFunctionalModelSettingsAdjustments;

namespace NanoQL.Models;

/// <summary>
/// The Markov-functional model's numerical settings: the state grid, the Gauss-Hermite integration, the digitals' gap,
/// the market rates' accuracy and bounds, and the adjustments and smile pretreatments.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: QuantLib's chained setters (<c>withYGridPoints</c> and the others, <c>addAdjustment</c>,
/// <c>removeAdjustment</c>) return modified copies. By default 64 grid points on each side over 7 standard
/// deviations, 32 Gauss-Hermite points, a digital gap of 1e-5, a market rate accuracy of 1e-7, rates between 0 and 2,
/// and Kahale's smile with exponential extrapolation.
/// </para>
/// <para>
/// <see cref="Validate"/> refuses any two of Kahale's, SABR's and a custom smile, as its text says. QuantLib's refuses
/// only all three (<c>markovfunctional.hpp:162-167</c>), so Kahale's and SABR's together are accepted and SABR's is
/// ignored.
/// </para>
/// </remarks>
public sealed class MarkovFunctionalModelSettings
{
    /// <summary>Initializes a new instance of the <see cref="MarkovFunctionalModelSettings"/> class, with QuantLib's
    /// defaults.</summary>
    public MarkovFunctionalModelSettings()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MarkovFunctionalModelSettings"/> class.</summary>
    /// <param name="yGridPoints">The state grid's points on each side.</param>
    /// <param name="yStdDevs">The state grid's width, in standard deviations.</param>
    /// <param name="gaussHermitePoints">The Gauss-Hermite integration's points.</param>
    /// <param name="digitalGap">The strikes' distance of the digitals.</param>
    /// <param name="marketRateAccuracy">The accuracy of the market rates the digitals are inverted to.</param>
    /// <param name="lowerRateBound">The market rates' lower bound, before the smile's shift.</param>
    /// <param name="upperRateBound">The market rates' upper bound.</param>
    /// <param name="adjustments">The adjustments and smile pretreatments.</param>
    /// <param name="smileMoneyCheckpoints">The moneyness grid of the smile pretreatments and the outputs; empty, the
    /// default, for the smile utilities' own.</param>
    /// <param name="customSmileFactory">The custom smile's factory, or <see langword="null"/>, the default, for
    /// none.</param>
    public MarkovFunctionalModelSettings(
        int yGridPoints,
        double yStdDevs,
        int gaussHermitePoints,
        double digitalGap,
        double marketRateAccuracy,
        double lowerRateBound,
        double upperRateBound,
        MarkovFunctionalModelSettingsAdjustments adjustments,
        ReadOnlySpan<double> smileMoneyCheckpoints = default,
        MarkovFunctionalCustomSmileFactory? customSmileFactory = null)
    {
        YGridPoints = yGridPoints;
        YStdDevs = yStdDevs;
        GaussHermitePoints = gaussHermitePoints;
        DigitalGap = digitalGap;
        MarketRateAccuracy = marketRateAccuracy;
        LowerRateBound = lowerRateBound;
        UpperRateBound = upperRateBound;
        Adjustments = adjustments;
        SmileMoneynessCheckpoints = [.. smileMoneyCheckpoints];
        CustomSmileFactory = customSmileFactory;
    }

    private MarkovFunctionalModelSettings(MarkovFunctionalModelSettings other)
    {
        YGridPoints = other.YGridPoints;
        YStdDevs = other.YStdDevs;
        GaussHermitePoints = other.GaussHermitePoints;
        DigitalGap = other.DigitalGap;
        MarketRateAccuracy = other.MarketRateAccuracy;
        LowerRateBound = other.LowerRateBound;
        UpperRateBound = other.UpperRateBound;
        Adjustments = other.Adjustments;
        SmileMoneynessCheckpoints = other.SmileMoneynessCheckpoints;
        CustomSmileFactory = other.CustomSmileFactory;
    }

    /// <summary>Gets the state grid's points on each side.</summary>
    public int YGridPoints { get; private init; } = 64;

    /// <summary>Gets the state grid's width, in standard deviations.</summary>
    public double YStdDevs { get; private init; } = 7.0;

    /// <summary>Gets the Gauss-Hermite integration's points.</summary>
    public int GaussHermitePoints { get; private init; } = 32;

    /// <summary>Gets the strikes' distance of the digitals.</summary>
    public double DigitalGap { get; private init; } = 1E-5;

    /// <summary>Gets the accuracy of the market rates the digitals are inverted to.</summary>
    public double MarketRateAccuracy { get; private init; } = 1E-7;

    /// <summary>Gets the market rates' lower bound, before the smile's shift.</summary>
    public double LowerRateBound { get; private init; }

    /// <summary>Gets the market rates' upper bound.</summary>
    public double UpperRateBound { get; private init; } = 2.0;

    /// <summary>Gets the adjustments and smile pretreatments.</summary>
    public MarkovFunctionalModelSettingsAdjustments Adjustments { get; private init; } = KahaleSmile | SmileExponentialExtrapolation;

    /// <summary>Gets the moneyness grid of the smile pretreatments and the outputs; empty for the smile utilities'
    /// own.</summary>
    public ImmutableArray<double> SmileMoneynessCheckpoints { get; private init; } = [];

    /// <summary>Gets the custom smile's factory, or <see langword="null"/>.</summary>
    public MarkovFunctionalCustomSmileFactory? CustomSmileFactory { get; private init; }

    /// <summary>
    /// The settings completed and checked: Kahale's interpolation implies Kahale's smile, and Kahale's smile with the
    /// deletion of arbitrage points implies Kahale's interpolation.
    /// </summary>
    /// <returns>The completed copy; the receiver doesn't change.</returns>
    /// <exception cref="NanoQLException">More than one of Kahale's, SABR's and a custom smile is set; there are fewer
    /// than two grid points; the integration count, the grid's width, the digital gap or the accuracy isn't positive;
    /// Kahale's smile is set with a lower rate bound other than 0; the lower rate bound isn't below the upper; or a
    /// custom smile has no factory.</exception>
    public MarkovFunctionalModelSettings Validate()
    {
        MarkovFunctionalModelSettings s = this;
        if ((s.Adjustments & KahaleInterpolation) != 0)
        {
            s = s.AddAdjustment(KahaleSmile);
        }

        if ((s.Adjustments & KahaleSmile) != 0 && (s.Adjustments & SmileDeleteArbitragePoints) != 0)
        {
            s = s.AddAdjustment(KahaleInterpolation);
        }

        int smiles = ((s.Adjustments & SabrSmile) != 0 ? 1 : 0) + ((s.Adjustments & KahaleSmile) != 0 ? 1 : 0)
            + ((s.Adjustments & CustomSmile) != 0 ? 1 : 0);
        // QuantLib's master's texts and grid minimum (#2912): on one grid point the grid's spacing divides by zero
        Require(smiles <= 1, "At most one of KahaleSmile, SabrSmile and CustomSmile can be specified at the same time");
        Require(
            s.YGridPoints >= 2,
            $"At least two grid points ({s.YGridPoints}) for the state process discretization must be given");
        Require(
            s.YStdDevs > 0.0,
            $"Multiple of standard deviations covered by state process discretization ({Show(s.YStdDevs)}) must be positive");
        Require(
            s.GaussHermitePoints > 0, $"Number of gauss hermite integration points ({s.GaussHermitePoints}) must be positive");
        Require(s.DigitalGap > 0.0, $"Digital gap ({Show(s.DigitalGap)}) must be positive");
        Require(s.MarketRateAccuracy > 0.0, $"Market rate accuracy ({Show(s.MarketRateAccuracy)}) must be positive");
        Require(
            (s.Adjustments & KahaleSmile) == 0 || s.LowerRateBound == 0.0,
            $"If Kahale extrapolation is used, the lower rate bound ({Show(s.LowerRateBound)}) must be zero.");
        Require(
            s.LowerRateBound < s.UpperRateBound,
            $"Lower rate bound ({Show(s.LowerRateBound)}) must be strictly less than upper rate bound ({Show(s.UpperRateBound)})");
        Require((s.Adjustments & CustomSmile) == 0 || s.CustomSmileFactory is not null, "missing CustomSmileFactoy");
        return s;
    }

    /// <summary>A copy with another number of state grid points on each side.</summary>
    /// <param name="n">The number.</param>
    /// <returns>The copy.</returns>
    public MarkovFunctionalModelSettings WithYGridPoints(int n) => new(this) { YGridPoints = n };

    /// <summary>A copy with another state grid width.</summary>
    /// <param name="s">The width, in standard deviations.</param>
    /// <returns>The copy.</returns>
    public MarkovFunctionalModelSettings WithYStdDevs(double s) => new(this) { YStdDevs = s };

    /// <summary>A copy with another number of Gauss-Hermite points.</summary>
    /// <param name="n">The number.</param>
    /// <returns>The copy.</returns>
    public MarkovFunctionalModelSettings WithGaussHermitePoints(int n) => new(this) { GaussHermitePoints = n };

    /// <summary>A copy with another digital gap.</summary>
    /// <param name="d">The gap.</param>
    /// <returns>The copy.</returns>
    public MarkovFunctionalModelSettings WithDigitalGap(double d) => new(this) { DigitalGap = d };

    /// <summary>A copy with another market rate accuracy.</summary>
    /// <param name="a">The accuracy.</param>
    /// <returns>The copy.</returns>
    public MarkovFunctionalModelSettings WithMarketRateAccuracy(double a) => new(this) { MarketRateAccuracy = a };

    /// <summary>A copy with another upper rate bound.</summary>
    /// <param name="u">The bound.</param>
    /// <returns>The copy.</returns>
    public MarkovFunctionalModelSettings WithUpperRateBound(double u) => new(this) { UpperRateBound = u };

    /// <summary>A copy with another lower rate bound.</summary>
    /// <param name="l">The bound.</param>
    /// <returns>The copy.</returns>
    public MarkovFunctionalModelSettings WithLowerRateBound(double l) => new(this) { LowerRateBound = l };

    /// <summary>A copy with other adjustments.</summary>
    /// <param name="a">The adjustments.</param>
    /// <returns>The copy.</returns>
    public MarkovFunctionalModelSettings WithAdjustments(MarkovFunctionalModelSettingsAdjustments a) => new(this) { Adjustments = a };

    /// <summary>A copy with adjustments added.</summary>
    /// <param name="a">The adjustments to add.</param>
    /// <returns>The copy.</returns>
    public MarkovFunctionalModelSettings AddAdjustment(MarkovFunctionalModelSettingsAdjustments a) => new(this) { Adjustments = Adjustments | a };

    /// <summary>A copy with adjustments removed.</summary>
    /// <param name="a">The adjustments to remove.</param>
    /// <returns>The copy.</returns>
    public MarkovFunctionalModelSettings RemoveAdjustment(MarkovFunctionalModelSettingsAdjustments a) => new(this) { Adjustments = Adjustments & ~a };

    /// <summary>A copy with another moneyness grid.</summary>
    /// <param name="m">The grid.</param>
    /// <returns>The copy.</returns>
    public MarkovFunctionalModelSettings WithSmileMoneynessCheckpoints(ReadOnlySpan<double> m) => new(this) { SmileMoneynessCheckpoints = [.. m] };

    /// <summary>A copy with another custom smile factory.</summary>
    /// <param name="f">The factory, or <see langword="null"/>.</param>
    /// <returns>The copy.</returns>
    public MarkovFunctionalModelSettings WithCustomSmileFactory(MarkovFunctionalCustomSmileFactory? f) => new(this) { CustomSmileFactory = f };

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new NanoQLException(message);
        }
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
