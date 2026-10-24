// SPDX-FileCopyrightText: Copyright (C) 2014, 2016 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/lineartsrpricer.hpp ql/cashflows/lineartsrpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Coupons;

/// <summary>
/// The linear TSR pricer's integration bounds: QuantLib's <c>LinearTsrPricer::Settings</c>, whose <c>with…</c> members
/// return a copy here.
/// </summary>
/// <remarks>
/// The bounds are applied in every strategy. On a shifted lognormal smile they apply to the strike plus the shift, so a
/// zero lower bound is the lowest rate of the shifted model; on a normal smile the lower bound is min(−upper, lower),
/// unless the bounds are given explicitly.
/// </remarks>
public sealed record LinearTsrPricerSettings
{
    /// <summary>The default lower rate bound, QuantLib's <c>defaultLowerBound</c>.</summary>
    public const double DefaultLowerBound = 0.0001;

    /// <summary>The default upper rate bound, QuantLib's <c>defaultUpperBound</c>.</summary>
    public const double DefaultUpperBound = 2.0000;

    /// <summary>Gets the strategy.</summary>
    public LinearTsrPricerSettingsStrategy Strategy { get; init; } = LinearTsrPricerSettingsStrategy.RateBound;

    /// <summary>Gets the vega ratio of <see cref="LinearTsrPricerSettingsStrategy.VegaRatio"/>.</summary>
    public double VegaRatio { get; init; } = 0.01;

    /// <summary>Gets the price threshold of <see cref="LinearTsrPricerSettingsStrategy.PriceThreshold"/>.</summary>
    public double PriceThreshold { get; init; } = 1.0E-8;

    /// <summary>Gets the standard deviations of <see cref="LinearTsrPricerSettingsStrategy.BSStdDevs"/>.</summary>
    public double StdDevs { get; init; } = 3.0;

    /// <summary>Gets the lower rate bound.</summary>
    public double LowerRateBound { get; init; } = DefaultLowerBound;

    /// <summary>Gets the upper rate bound.</summary>
    public double UpperRateBound { get; init; } = DefaultUpperBound;

    /// <summary>Gets a value indicating whether the bounds are the defaults, not given explicitly.</summary>
    public bool DefaultBounds { get; init; } = true;

    /// <summary>The rate-bound strategy on the given bounds.</summary>
    /// <param name="lowerRateBound">The lower rate bound.</param>
    /// <param name="upperRateBound">The upper rate bound.</param>
    /// <returns>The settings.</returns>
    public LinearTsrPricerSettings WithRateBound(
        double lowerRateBound = DefaultLowerBound, double upperRateBound = DefaultUpperBound) =>
        this with
        {
            Strategy = LinearTsrPricerSettingsStrategy.RateBound,
            LowerRateBound = lowerRateBound,
            UpperRateBound = upperRateBound,
            DefaultBounds = false,
        };

    /// <summary>The vega-ratio strategy on the default bounds.</summary>
    /// <param name="vegaRatio">The vega ratio.</param>
    /// <returns>The settings.</returns>
    public LinearTsrPricerSettings WithVegaRatio(double vegaRatio = 0.01) =>
        this with
        {
            Strategy = LinearTsrPricerSettingsStrategy.VegaRatio,
            VegaRatio = vegaRatio,
            LowerRateBound = DefaultLowerBound,
            UpperRateBound = DefaultUpperBound,
            DefaultBounds = true,
        };

    /// <summary>The vega-ratio strategy on the given bounds.</summary>
    /// <param name="vegaRatio">The vega ratio.</param>
    /// <param name="lowerRateBound">The lower rate bound.</param>
    /// <param name="upperRateBound">The upper rate bound.</param>
    /// <returns>The settings.</returns>
    public LinearTsrPricerSettings WithVegaRatio(double vegaRatio, double lowerRateBound, double upperRateBound) =>
        this with
        {
            Strategy = LinearTsrPricerSettingsStrategy.VegaRatio,
            VegaRatio = vegaRatio,
            LowerRateBound = lowerRateBound,
            UpperRateBound = upperRateBound,
            DefaultBounds = false,
        };

    /// <summary>The price-threshold strategy on the default bounds.</summary>
    /// <param name="priceThreshold">The price threshold.</param>
    /// <returns>The settings.</returns>
    public LinearTsrPricerSettings WithPriceThreshold(double priceThreshold = 1.0E-8) =>
        this with
        {
            Strategy = LinearTsrPricerSettingsStrategy.PriceThreshold,
            PriceThreshold = priceThreshold,
            LowerRateBound = DefaultLowerBound,
            UpperRateBound = DefaultUpperBound,
            DefaultBounds = true,
        };

    /// <summary>The price-threshold strategy on the given bounds.</summary>
    /// <param name="priceThreshold">The price threshold.</param>
    /// <param name="lowerRateBound">The lower rate bound.</param>
    /// <param name="upperRateBound">The upper rate bound.</param>
    /// <returns>The settings.</returns>
    public LinearTsrPricerSettings WithPriceThreshold(double priceThreshold, double lowerRateBound, double upperRateBound) =>
        this with
        {
            Strategy = LinearTsrPricerSettingsStrategy.PriceThreshold,
            PriceThreshold = priceThreshold,
            LowerRateBound = lowerRateBound,
            UpperRateBound = upperRateBound,
            DefaultBounds = false,
        };

    /// <summary>The standard-deviations strategy on the default bounds.</summary>
    /// <param name="stdDevs">The standard deviations.</param>
    /// <returns>The settings.</returns>
    public LinearTsrPricerSettings WithBSStdDevs(double stdDevs = 3.0) =>
        this with
        {
            Strategy = LinearTsrPricerSettingsStrategy.BSStdDevs,
            StdDevs = stdDevs,
            LowerRateBound = DefaultLowerBound,
            UpperRateBound = DefaultUpperBound,
            DefaultBounds = true,
        };

    /// <summary>The standard-deviations strategy on the given bounds.</summary>
    /// <param name="stdDevs">The standard deviations.</param>
    /// <param name="lowerRateBound">The lower rate bound.</param>
    /// <param name="upperRateBound">The upper rate bound.</param>
    /// <returns>The settings.</returns>
    public LinearTsrPricerSettings WithBSStdDevs(double stdDevs, double lowerRateBound, double upperRateBound) =>
        this with
        {
            Strategy = LinearTsrPricerSettingsStrategy.BSStdDevs,
            StdDevs = stdDevs,
            LowerRateBound = lowerRateBound,
            UpperRateBound = upperRateBound,
            DefaultBounds = false,
        };
}
