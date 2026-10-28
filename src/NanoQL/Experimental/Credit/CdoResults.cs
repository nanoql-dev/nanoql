// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/midpointcdoengine.cpp ql/experimental/credit/integralcdoengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Experimental;

/// <summary>The two CDO engines' common ending: the legs signed by the side, the value and the fair rates.</summary>
internal static class CdoResults
{
    /// <summary>The results of the seller's legs.</summary>
    /// <param name="cdo">The CDO.</param>
    /// <param name="basket">Its basket.</param>
    /// <param name="premiumValue">The seller's premium leg.</param>
    /// <param name="protectionValue">The seller's protection leg.</param>
    /// <param name="upfrontPremiumValue">The seller's upfront.</param>
    /// <param name="remainingNotional">The levered outstanding notional.</param>
    /// <param name="error">The steps where the expected loss fell.</param>
    /// <param name="expectedTrancheLoss">The expected tranche losses.</param>
    /// <param name="upfrontDiscount">The first accrual start's discount, or <see langword="null"/> once the first
    /// coupon is paid.</param>
    /// <returns>The results.</returns>
    internal static SyntheticCDOResults Assemble(
        SyntheticCDO cdo,
        Basket basket,
        double premiumValue,
        double protectionValue,
        double upfrontPremiumValue,
        double remainingNotional,
        int error,
        ImmutableArray<double> expectedTrancheLoss,
        double? upfrontDiscount)
    {
        // the side-invariant upfront zeroing the value, on the seller's legs
        double? fairUpfront = upfrontDiscount.HasValue
            ? (protectionValue - premiumValue) / (remainingNotional * upfrontDiscount.Value)
            : null;
        if (cdo.Side == ProtectionSide.Buyer)
        {
            protectionValue *= -1;
            premiumValue *= -1;
            upfrontPremiumValue *= -1;
        }

        double value = premiumValue - protectionValue + upfrontPremiumValue;

        // Fair spread GIVEN the upfront: the instrument's
        double fairSpread = 0.0;
        if (premiumValue != 0.0)
        {
            fairSpread = cdo.RunningRate * (protectionValue - upfrontPremiumValue) / premiumValue;
        }

        return new SyntheticCDOResults
        {
            NPV = value,
            ErrorEstimate = null,
            PremiumValue = premiumValue,
            ProtectionValue = protectionValue,
            UpfrontPremiumValue = upfrontPremiumValue,
            RemainingNotional = remainingNotional,
            XMin = basket.AttachmentAmount,
            XMax = basket.DetachmentAmount,
            Error = error,
            ExpectedTrancheLoss = expectedTrancheLoss,
            FairPremium = premiumValue != 0.0 ? fairSpread : null,
            FairUpfrontPremium = fairUpfront,
            PremiumLegNPV = cdo.Side == ProtectionSide.Buyer ? premiumValue : -premiumValue,
            ProtectionLegNPV = cdo.Side == ProtectionSide.Buyer ? -protectionValue : protectionValue,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty
                .Add("fairPremium", fairSpread)
                .Add("premiumLegNPV", premiumValue + upfrontPremiumValue)
                .Add("protectionLegNPV", protectionValue),
        };
    }
}
