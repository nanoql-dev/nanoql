// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The frozen-coefficient swaption volatility on an evolution that doesn't hold its expiry (the S-mm plan's MR4 (a), a
/// refusal the user ruled). QuantLib's <c>SwapForwardMappings::swaptionImpliedVolatility</c> sums every step starting
/// before the expiry (<c>swapforwardmappings.cpp:194</c>, <c>:216-217</c>), so a step straddling it adds the later
/// variance: FlatVol at 20% on eleven semi-annual rates at 4%, yearly steps, swaption (0, 2) 0.221024 against 0.197607
/// on every fixing (+11.9%), (0, 10) +34.3%, (2, 10) +12.0% (the planner's MP26, <c>probes\round1.out.txt</c>); and it
/// reads the first displacement alone (<c>:183</c>). QuantLib's frozen-coefficient adapters refuse both ("skipping …
/// rate time", "not equal to the previous ones", <c>fwdtocotswapadapter.cpp:44</c>, <c>:50-57</c>), and so does
/// nanoql here. A grid that holds the expiry keeps QuantLib's value: (1, 4) on yearly steps is the every-fixing value,
/// 0.195828.
/// </summary>
public class SwaptionImpliedVolatilityGridTests
{
    private static readonly double[] RateTimes = [.. Enumerable.Range(1, 12).Select(i => 0.5 * i)];

    [Fact]
    public void AnExpiryOffTheEvolutionIsRefused()
    {
        MarketModel yearly = Model([1.0, 2.0, 3.0, 4.0, 5.0], 0.0);
        ArgumentException e = Assert.Throws<ArgumentException>(() => SwapForwardMappings.SwaptionImpliedVolatility(yearly, 0, 2));
        Assert.StartsWith("skipping 1st rate time", e.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => SwapForwardMappings.SwaptionImpliedVolatility(yearly, 2, 10));
    }

    [Fact]
    public void AnExpiryOnTheEvolutionKeepsTheEveryFixingValue()
    {
        double yearly = SwapForwardMappings.SwaptionImpliedVolatility(Model([1.0, 2.0, 3.0, 4.0, 5.0], 0.0), 1, 4);
        double everyFixing = SwapForwardMappings.SwaptionImpliedVolatility(Model(RateTimes[..^1], 0.0), 1, 4);
        Assert.Equal(0.195828, yearly, 1e-6);
        Assert.Equal(everyFixing, yearly, 1e-12);
    }

    [Fact]
    public void UnequalDisplacementsAreRefused()
    {
        MarketModel m = Model(RateTimes[..^1], 0.01, rising: true);
        ArgumentException e = Assert.Throws<ArgumentException>(() => SwapForwardMappings.SwaptionImpliedVolatility(m, 0, 2));
        Assert.StartsWith("2nd displacement (", e.Message, StringComparison.Ordinal);
        Assert.Contains(") not equal to the previous ones (0.01)", e.Message, StringComparison.Ordinal);
    }

    private static MarketModel Model(double[] evolutionTimes, double d, bool rising = false)
    {
        const int n = 11;
        var corr = new TimeHomogeneousForwardCorrelation(ExpCorrelationsFunctions.ExponentialCorrelations(RateTimes, 0.5, 0.2, 1.0, 0.0), RateTimes);
        double[] displacements = [.. Enumerable.Range(0, n).Select(i => rising ? d + (0.001 * i) : d)];
        return new FlatVol(
            Enumerable.Repeat(0.2, n).ToArray(),
            corr,
            new EvolutionDescription(RateTimes, evolutionTimes),
            n,
            Enumerable.Repeat(0.04, n).ToArray(),
            displacements);
    }
}
