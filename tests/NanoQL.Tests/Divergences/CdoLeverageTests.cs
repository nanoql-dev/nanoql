// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using static NanoQL.Tests.Conventions.CdoBuilders;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The CDO engines' leverage, additional fair premium and fair upfront (the S-xcr plan's XR8 (ix)): every leg and the
/// upfront levered, the fair rates unlevered, the additional fair premium the instrument's, the fair upfront the
/// side-invariant rate that zeroes the value over the first accrual's discount. QuantLib's values on the eight-name
/// [3%, 7%] tranche at 2% running are the S-xcr run's <c>c2_facts.cpp</c>: unlevered, the mid-point fair premium
/// 0.10676171982795336 and NPV −0.94866792850998161, the integral 0.10608680303646907 and −0.94135991232162608; at
/// twice the notional, the mid-point's 0.053380859913976682 and −0.72998440531768671 (its premium leg alone levered),
/// the integral's unchanged; the additional fair premium the instrument's negated; the buyer's fair upfront
/// −0.2964587276593692 where the seller's is 0.2964587276593692, its NPV at it 1.8972059114097959; a forward start's
/// NPV at its fair upfront −0.020592559070216399.
/// </summary>
public class CdoLeverageTests
{
    [Fact]
    public void TheLeveredLegsKeepTheUnleveredRates()
    {
        Basket basket = EightNames(0.03, 0.07).SetLossModel(Pool(8, 0.3));
        SyntheticCDOResults mid1 = Mid(Cdo(basket, ProtectionSide.Seller, 0.0, 0.02));
        SyntheticCDOResults mid2 = Mid(Cdo(basket, ProtectionSide.Seller, 0.0, 0.02, 2 * basket.TrancheNotional));
        SyntheticCDOResults int1 = Integral(Cdo(basket, ProtectionSide.Seller, 0.0, 0.02));
        SyntheticCDOResults int2 = Integral(Cdo(basket, ProtectionSide.Seller, 0.0, 0.02, 2 * basket.TrancheNotional));

        Assert.Equal(0.10676171982795336, mid1.FairPremium!.Value, 1e-14);
        Assert.Equal(-0.94866792850998161, mid1.NPV!.Value, 1e-14);
        Assert.Equal(0.10608680303646907, int1.FairPremium!.Value, 1e-14);
        Assert.Equal(-0.94135991232162608, int1.NPV!.Value, 1e-14);
        Assert.Equal(mid1.FairPremium!.Value, mid2.FairPremium!.Value, 1e-14);
        Assert.Equal(2 * mid1.NPV!.Value, mid2.NPV!.Value, 1e-13);
        Assert.Equal(int1.FairPremium!.Value, int2.FairPremium!.Value, 1e-14);
        Assert.Equal(2 * int1.NPV!.Value, int2.NPV!.Value, 1e-13);
        Assert.Equal(2 * mid1.RemainingNotional!.Value, mid2.RemainingNotional!.Value, 1e-13);
    }

    [Fact]
    public void TheAdditionalFairPremiumIsTheInstruments()
    {
        Basket basket = EightNames(0.03, 0.07).SetLossModel(Pool(8, 0.3));
        SyntheticCDOResults r = Mid(Cdo(basket, ProtectionSide.Buyer, 0.05, 0.02));

        Assert.Equal(r.FairPremium!.Value, (double)r.AdditionalResults["fairPremium"], 1e-15);
        Assert.True(r.FairPremium!.Value > 0.0);
    }

    [Fact]
    public void TheFairUpfrontZeroesTheValueOnEitherSide()
    {
        Basket basket = EightNames(0.03, 0.07).SetLossModel(Pool(8, 0.3));
        double seller = Mid(Cdo(basket, ProtectionSide.Seller, 0.0, 0.02)).FairUpfrontPremium!.Value;
        double buyer = Mid(Cdo(basket, ProtectionSide.Buyer, 0.0, 0.02)).FairUpfrontPremium!.Value;
        double forward = Mid(Cdo(basket, ProtectionSide.Seller, 0.0, 0.02, schedule: Forward())).FairUpfrontPremium!.Value;

        Assert.Equal(seller, buyer, 1e-15);
        Assert.Equal(0.0, Mid(Cdo(basket, ProtectionSide.Buyer, buyer, 0.02)).NPV!.Value, 1e-14);
        Assert.Equal(0.0, Mid(Cdo(basket, ProtectionSide.Seller, seller, 0.02)).NPV!.Value, 1e-14);
        Assert.Equal(0.0, Mid(Cdo(basket, ProtectionSide.Seller, forward, 0.02, schedule: Forward())).NPV!.Value, 1e-14);
    }
}
