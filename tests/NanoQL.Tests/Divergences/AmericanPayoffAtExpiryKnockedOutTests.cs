// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The knock-out at expiry with the strike on the spot, under QuantLib's master's low-volatility fix (#2855, taken
/// under R8 by the S-van plan's VR5 (i); this defect found by the S-van run). With the strike on the spot, the
/// knock-out is already knocked out, and QuantLib sets both probabilities to 0 (<c>americanpayoffatexpiry.cpp:126-160</c>);
/// #2855 then reads N(D₂) = 0 as an underflow and replaces it by e^(2μ ln(H/S))·N(D₂) = N(D₂) with a power of 1, so the
/// knock-out prices −D·K·N(D₂): a call paying 10, S = H = 100, r 5%, vol 20%, one year, −4.19, where QuantLib 1.44
/// prices 0 [executed on the wheel and by hand: D₂ = −μσ = −0.15]. nanoql takes #2855's form only where N(D₂) is the
/// formula's. QuantLib's check: its own 1.44 value, 0, a knocked-out option's.
/// </summary>
public class AmericanPayoffAtExpiryKnockedOutTests
{
    [Fact]
    public void AKnockOutWithTheStrikeOnTheSpotIsWorthNothing()
    {
        double discount = Math.Exp(-0.05), variance = 0.04;
        var cash = new CashOrNothingPayoff(OptionType.Call, 100.0, 10.0);
        var asset = new AssetOrNothingPayoff(OptionType.Put, 100.0);
        Assert.Equal(0.0, new AmericanPayoffAtExpiry(100.0, discount, 1.0, variance, cash, knockIn: false).Value());
        Assert.Equal(0.0, new AmericanPayoffAtExpiry(100.0, discount, 1.0, variance, asset, knockIn: false).Value());

        // the knock-in there is paid for sure, at expiry: QuantLib's D·K
        Assert.Equal(discount * 10.0, new AmericanPayoffAtExpiry(100.0, discount, 1.0, variance, cash).Value(), 1e-15);
    }
}
