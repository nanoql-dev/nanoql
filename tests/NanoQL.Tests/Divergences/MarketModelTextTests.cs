// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Refusals whose QuantLib text names another value than its check reads (the S-mm plan's MR3 (xv), fixed under R8).
/// <c>EvolutionDescription</c> checks the last evolution time against the last fixing time,
/// <c>rateTimes[rateTimes.size()-2]</c>, and prints <c>rateTimes[numberOfRates_-2]</c>, the time before it, a read
/// before the vector at one rate (<c>evolutiondescription.cpp:51-54</c>; the planner's MP5: −1.29e-231 at one rate,
/// 0.5 for a check of 1.0 at two). <c>LMMCurveState::setOnDiscountRatios</c> checks the first valid index against
/// the number of rates and prints one more as its bound (<c>lmmcurvestate.cpp:76-79</c>), found at this run's C1. The
/// exponential correlation's text is in <c>ExponentialCorrelationTests</c>.
/// </summary>
public class MarketModelTextTests
{
    [Fact]
    public void TheLastEvolutionTimesRefusalNamesTheLastFixingTime()
    {
        // QuantLib: "The last evolution time (1.2) is past the last fixing time (0.5)" (this run's probes\c1_facts.cpp).
        ArgumentException e = Assert.Throws<ArgumentException>(() => new EvolutionDescription([0.5, 1.0, 1.5], [1.2]));
        Assert.StartsWith("The last evolution time (1.2) is past the last fixing time (1)", e.Message, StringComparison.Ordinal);

        // at one rate, where QuantLib's read is before its vector
        e = Assert.Throws<ArgumentException>(() => new EvolutionDescription([1.0, 2.0], [1.5]));
        Assert.StartsWith("The last evolution time (1.5) is past the last fixing time (1)", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDiscountRatiosFirstIndexRefusalNamesItsBound()
    {
        // QuantLib: "first valid index must be less than 3: 2 not allowed" (this run's probes\c1_facts.cpp).
        var cs = new LMMCurveState([0.5, 1.0, 1.5]);
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => cs.SetOnDiscountRatios([1.0, 0.99, 0.98], 2));
        Assert.StartsWith("first valid index must be less than 2: 2 not allowed", e.Message, StringComparison.Ordinal);
        cs.SetOnDiscountRatios([1.0, 0.99, 0.98], 1);
        Assert.Equal(0.99 / 0.98, cs.DiscountRatio(1, 2));
    }
}
