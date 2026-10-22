// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Quotes;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The premium-adjusted puts beyond the non-adjusted delta's range (the S-rx plan's SRXR3 (xi), fixed under R8).
/// <c>deltaFromStrike</c> returns a premium-adjusted put delta whose size exceeds the foreign discount factor (spot) or
/// 1 (forward), and <c>strikeFromDelta</c> refuses it through its non-adjusted guess
/// (<c>blackdeltacalculator.cpp:102-111</c>,
/// <c>:143-147</c>): with spot 1, discount factors 0.99 and 0.97 and a standard deviation of 0.2, the strikes 1.4, 2.0
/// and 3.0 give deltas QuantLib refuses as "Spot delta out of range." and "Forward delta out of range." [executed on
/// the wheel]. nanoql starts QuantLib's Brent from the forward on QuantLib's own bracket, <c>[0, 100·spot]</c>
/// (<c>:150</c>), where the guess is out of range. QuantLib's check: its own <c>deltaFromStrike</c> at the solved
/// strike, and the strikes the deltas came from.
/// </summary>
public class PremiumAdjustedPutStrikeTests
{
    [Fact]
    public void ASpotPremiumAdjustedPutsStrikeIsSolved() => Solved(
        DeltaVolQuoteDeltaType.PaSpot,
        [(1.4, -1.3447594723726055), (2.0, -1.9797577813454117), (3.0, -2.969999981690627)]);

    [Fact]
    public void AForwardPremiumAdjustedPutsStrikeIsSolved() => Solved(
        DeltaVolQuoteDeltaType.PaFwd,
        [(1.4, -1.3863499715181502), (2.0, -2.040987403448878), (3.0, -3.0618556512274506)]);

    // (strike, QuantLib's deltaFromStrike there): the delta is QuantLib's, and the solved strike gives it back.
    private static void Solved(DeltaVolQuoteDeltaType deltaType, (double Strike, double Delta)[] cases)
    {
        var calculator = new BlackDeltaCalculator(OptionType.Put, deltaType, 1.0, 0.99, 0.97, 0.2);
        foreach (var c in cases)
        {
            Assert.Equal(c.Delta, calculator.DeltaFromStrike(c.Strike));
            double strike = calculator.StrikeFromDelta(c.Delta);
            Assert.Equal(c.Strike, strike, 1e-8);
            Assert.Equal(c.Delta, calculator.DeltaFromStrike(strike), 1e-10);
        }
    }
}
