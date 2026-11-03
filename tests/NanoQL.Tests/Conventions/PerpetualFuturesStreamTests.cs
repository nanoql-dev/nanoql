// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The perpetual futures' enums and defaults: QuantLib's stream operators print each payoff and funding type's member
/// name (<c>perpetualfutures.cpp:71-95</c>), which the enums' own strings give, so the operators aren't ported; the
/// members keep QuantLib's values; and the shorter constructors take QuantLib's defaults, funding on the current spot
/// every eight hours on a null calendar and Actual/Actual (ISDA).
/// </summary>
public class PerpetualFuturesStreamTests
{
    [Fact]
    public void TheEnumsPrintQuantLibsNamesAndKeepItsValues()
    {
        Assert.Equal(["Linear", "Inverse", "Quanto"], Enum.GetNames<PerpetualFuturesPayoffType>());
        Assert.Equal([0, 1, 2], Enum.GetValues<PerpetualFuturesPayoffType>().Select(x => (int)x));
        Assert.Equal(["FundingWithPreviousSpot", "FundingWithCurrentSpot"], Enum.GetNames<PerpetualFuturesFundingType>());
        Assert.Equal([0, 1], Enum.GetValues<PerpetualFuturesFundingType>().Select(x => (int)x));
        Assert.Equal(
            ["PiecewiseConstant", "Linear", "CubicSpline"], Enum.GetNames<DiscountingPerpetualFuturesEngineInterpolationType>());
    }

    [Fact]
    public void TheShorterConstructorsTakeQuantLibsDefaults()
    {
        var futures = new PerpetualFutures(PerpetualFuturesPayoffType.Inverse);
        Assert.Equal(PerpetualFuturesFundingType.FundingWithCurrentSpot, futures.FundingType);
        Assert.Equal(new Period(8, TimeUnit.Hours), futures.FundingFrequency);
        Assert.Equal(new NullCalendar(), futures.Calendar);
        Assert.Equal(new ActualActual(ActualActualConvention.ISDA), futures.DayCounter);
        Assert.False(futures.IsExpired(new EvaluationContext { EvaluationDate = new Date(15, October, 2026) }));
    }
}
