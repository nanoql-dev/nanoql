// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Bjerksund and Stensland's and the operator-splitting spread engines where the second forward and the strike sum to
/// zero or less. QuantLib takes the log of a negative ratio and returns NaN
/// (<c>bjerksundstenslandspreadengine.cpp:48</c>, <c>operatorsplittingspreadengine.cpp:53</c>), where Kirk's engine,
/// their sibling, refuses through Black's check ("forward (−2.19458) must be positive"). nanoql refuses before the log,
/// "f2 + strike must be positive for this approximation": a refusal, the approximations having no value there.
/// </summary>
public class SpreadApproximationStrikeTests
{
    private const string Message = "f2 + strike must be positive for this approximation";

    [Fact]
    public void BjerksundStenslandRefuses()
    {
        var p1 = Process(110.0, 0.0, 0.05, 0.3);
        var p2 = Process(90.0, 0.0, 0.05, 0.2);
        foreach (Real strike in new[] { -200.0, -100.0 })
        {
            NanoQLException e = Assert.Throws<NanoQLException>(
                () => BjerksundStenslandSpreadEngine.Calculate(Spread(OptionType.Call, strike), Context, p1, p2, 0.5));
            Assert.Equal(Message, e.Message);
        }
    }

    [Fact]
    public void OperatorSplittingRefusesAtEitherOrder()
    {
        var p1 = Process(110.0, 0.0, 0.05, 0.3);
        var p2 = Process(90.0, 0.0, 0.05, 0.2);
        foreach (OperatorSplittingSpreadEngineOrder order in new[]
            { OperatorSplittingSpreadEngineOrder.First, OperatorSplittingSpreadEngineOrder.Second })
        {
            NanoQLException e = Assert.Throws<NanoQLException>(
                () => OperatorSplittingSpreadEngine.Calculate(Spread(OptionType.Put, -200.0), Context, p1, p2, 0.5, order));
            Assert.Equal(Message, e.Message);
        }
    }
}
