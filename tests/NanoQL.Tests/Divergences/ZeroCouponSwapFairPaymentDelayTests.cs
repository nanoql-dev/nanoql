// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The zero-coupon swap's fair payment with a payment delay: QuantLib's <c>fairFixedPayment</c> divides the floating
/// leg's NPV by its fixed leg's end discount (<c>zerocouponswap.cpp:147-153</c>), read at
/// <c>CashFlows::maturityDate</c> of that leg (<c>discountingswapengine.cpp:97-99</c>), the accrual end for the
/// fixed-rate constructor's coupon, paid a payment delay later (<c>cashflows.cpp:52-64</c>), so that swap's fair rate
/// fed back leaves a non-zero NPV: on the wheel, on the vectors' 5-year receiver on 1e8 from 17 January 2025 at 3.2% on
/// Actual/365 (Fixed), −1,013.7182254604995, −14,173.113207207993, −42,397.21247311309 and −131,035.65942218527 at
/// delays of 1, 10, 30 and 90 days, where the fixed-payment constructor's fair payment, 16,776,091.202675004 at every
/// delay, fed back gives 0. nanoql discounts at the fixed flow's payment date. QuantLib's check: its fixed-payment
/// constructor on the same swap, its fair payment and rate. At an NPV date: the floating leg's NPV is given at the
/// engine's NPV date (<c>cashflows.cpp:498</c>) and its end discount from the curve's reference date
/// (<c>discountingswapengine.cpp:97-101</c>), so QuantLib's fair payment is high by the NPV date's discount's
/// reciprocal: on the same swap without a delay, 17,200,779.97339953 at an NPV date of 15 January 2026 and
/// 18,085,160.07066775 at 17 January 2028, fed back through the fixed-payment constructor at those dates
/// 384,195.3384123612 and 1,245,139.539481705. nanoql takes the NPV back by the NPV date's discount, as QuantLib's
/// asset swap does (<c>assetswap.cpp:273</c>, <c>:294</c>). QuantLib's check: its fair payment with no NPV date, and
/// the fed-back NPV.
/// </summary>
public class ZeroCouponSwapFairPaymentDelayTests
{
    private const double FairPayment = 16776091.202675004;

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(90)]
    public void TheFairPaymentIsTheFixedPaymentConstructors(int delay)
    {
        (IborIndex index, YieldTermStructure discount, EvaluationContext context) = Market();
        ZeroCouponSwap swap = RateSwap(0.032, index, delay);
        ZeroCouponSwapResults results = DiscountingSwapEngine.Calculate(swap, context, discount);
        Assert.True(Math.Abs(results.FairFixedPayment!.Value - FairPayment) <= 1e-6, $"{results.FairFixedPayment}");
        Assert.True(Math.Abs(swap.FairFixedRate(results, new Actual365Fixed()) - 0.0314861705497671) <= 1e-15);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(90)]
    public void TheFairRateFedBackIsAtPar(int delay)
    {
        (IborIndex index, YieldTermStructure discount, EvaluationContext context) = Market();
        ZeroCouponSwap swap = RateSwap(0.032, index, delay);
        ZeroCouponSwapResults results = DiscountingSwapEngine.Calculate(swap, context, discount);
        double fair = swap.FairFixedRate(results, new Actual365Fixed());
        double npv = DiscountingSwapEngine.Calculate(RateSwap(fair, index, delay), context, discount).NPV!.Value;
        Assert.True(Math.Abs(npv) <= 1e-6, $"{npv}");
    }

    [Theory]
    [InlineData(2026, January, 15)]
    [InlineData(2028, January, 17)]
    public void AtAnNpvDateTheFairPaymentIsTheReferenceDates(int year, Month month, int day)
    {
        (IborIndex index, YieldTermStructure discount, EvaluationContext context) = Market();
        var npvDate = new Date(day, month, year);
        ZeroCouponSwapResults results =
            DiscountingSwapEngine.Calculate(RateSwap(0.032, index, 0), context, discount, npvDate: npvDate);
        double fairPayment = results.FairFixedPayment!.Value;
        Assert.True(Math.Abs(fairPayment - FairPayment) <= 1e-6, $"{fairPayment}");
        var back = new ZeroCouponSwap(
            SwapType.Receiver,
            1e8,
            new Date(17, January, 2025),
            new Date(17, January, 2030),
            fairPayment,
            index,
            new TARGET());
        double npv = DiscountingSwapEngine.Calculate(back, context, discount, npvDate: npvDate).NPV!.Value;
        Assert.True(Math.Abs(npv) <= 1e-6, $"{npv}");
    }

    private static (IborIndex Index, YieldTermStructure Discount, EvaluationContext Context) Market()
    {
        VectorCase c = VectorFile.Load("instruments/zerocouponswap").Cases
            .Single(x => x.Name.StartsWith("swap rate, 5Y, receiver /", StringComparison.Ordinal));
        return (IndexInputs.IborIndexOf(c.Input("index")), IndexInputs.CurveOf(c.Input("discountCurve"))!, c.Context());
    }

    private static ZeroCouponSwap RateSwap(double rate, IborIndex index, int delay) => new(
        SwapType.Receiver,
        1e8,
        new Date(17, January, 2025),
        new Date(17, January, 2030),
        rate,
        new Actual365Fixed(),
        index,
        new TARGET(),
        BusinessDayConvention.Following,
        delay);
}
