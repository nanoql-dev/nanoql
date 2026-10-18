// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The vanilla swap vectors, <c>tests/vectors/instruments/vanillaswap.json</c>: payer and receiver swaps of several
/// tenors, spreads, nominals and coupon choices, with their results and each coupon's amount; ten of them again with
/// a Black pricer set on the built swap's floating leg through
/// <see cref="CouponPricerFunctions.SetCouponPricer{T}(T, int, FloatingRateCouponPricer?)"/>; and QuantLib's
/// testNotifications swap, priced on one forecasting curve and rebuilt on another, the rebuild that replaces
/// QuantLib's relink.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VanillaSwapVectorTests
{
    [Fact]
    public void VanillaSwapVector()
    {
        var file = VectorFile.Load("instruments/vanillaswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            VanillaSwap swap = SwapInputs.VanillaSwapOf(c.Input("swap"), context);
            FloatingRateCouponPricer? pricer = CouponInputs.PricerOf(c.Input("pricer"), context);
            if (pricer is not null)
            {
                // QuantLib's setCouponPricer(swap->floatingLeg(), pricer) on the built swap.
                swap = CouponPricerFunctions.SetCouponPricer(swap, 1, pricer);
            }

            FlatForward discount = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            SwapInputs.CheckResults(check, c, () => DiscountingSwapEngine.Calculate(swap, context, discount));
            SwapInputs.CheckLeg(check, c, "fixedLeg", swap.FixedLeg, context);
            SwapInputs.CheckLeg(check, c, "floatingLeg", swap.FloatingLeg, context);
        }

        check.Complete();
    }
}
