// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The perpetual futures engine's tail past its horizon. QuantLib refuses only a non-positive last funding rate
/// (<c>discountingperpetualfuturesengine.cpp:86-88</c>), while its flat tail converges only where k + r_f − r_d &gt; 0
/// (continuous funding) or (1 + k)·e^{Δt(r_f − r_d)} &gt; 1 (discrete); below that its closed form returns the divergent
/// series' analytic continuation: on QuantLib's case market (r_d 4%, r_f 2%, interest-rate difference 0.005) continuous
/// funding at k 0.01 prices −4,999.998 and quarterly funding at k 0.004 a plausible 9,876.34; and where r_f &gt; r_d it
/// refuses a non-positive rate whose tail converges. nanoql refuses exactly where the tail diverges, a discrete rate of
/// −1 or below among them, and prices a convergent tail at any rate (the S-exo plan's ER5 (xl), ER8). The checks: the
/// refusals' type and text; Ackerer, Hugonnier and Jermann's constant-rate closed forms, QuantLib's own case's.
/// </summary>
public class PerpetualFuturesDivergentTailTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter Dc = new ActualActual(ActualActualConvention.ISDA);

    [Fact]
    public void AContinuousDivergentTailIsRefused()
    {
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => Price(PerpetualFuturesPayoffType.Linear, new Period(0, TimeUnit.Months), 0.04, 0.02, 0.01, 0.005));
        Assert.StartsWith("fundingRate + foreign rate - domestic rate at max time is not positive", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADiscreteDivergentTailIsRefused()
    {
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => Price(PerpetualFuturesPayoffType.Linear, new Period(3, TimeUnit.Months), 0.04, 0.02, 0.004, 0.005));
        Assert.StartsWith(
            "(1 + fundingRate) exp(timeStep (foreign rate - domestic rate)) at max time is not above 1",
            e.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ADiscreteFundingRateOfMinusOneOrBelowIsRefused()
    {
        foreach (double k in new[] { -1.0, -1.5 })
        {
            NanoQLException e = Assert.Throws<NanoQLException>(
                () => Price(PerpetualFuturesPayoffType.Linear, new Period(3, TimeUnit.Months), 0.02, 0.04, k, 0.0));
            Assert.StartsWith("1 + fundingRate at max time is not positive", e.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ANonPositiveFundingRateOnAConvergentTailIsPriced()
    {
        // r_d 2%, r_f 4%: the tail converges at rates QuantLib refuses
        const double s = 10000.0, rd = 0.02, rf = 0.04, iDiff = 0.001;
        foreach (double k in new[] { 0.0, -0.005 })
        {
            // continuous, linear: s·(k − i)/(r_f − r_d + k), within the trapezoid rule's accuracy, its 1e-6 per
            // integral accumulating over the horizon's sixty years where the funding barely discounts
            double linear = Price(PerpetualFuturesPayoffType.Linear, new Period(0, TimeUnit.Months), rd, rf, k, iDiff);
            Assert.Equal(s * (k - iDiff) / (rf - rd + k), linear, 1e-5 * Math.Abs(linear));
        }

        foreach (double k in new[] { 0.0, -0.002 })
        {
            // quarterly funding on the previous spot, linear, (1 + k)·e^{Δt(r_f − r_d)} above 1: Equation (12)
            const double dt = 0.25;
            double discrete = Price(PerpetualFuturesPayoffType.Linear, new Period(3, TimeUnit.Months), rd, rf, k, iDiff);
            double expected = s * (k - iDiff) * Math.Exp(rf * dt)
                / (Math.Exp(rf * dt) - Math.Exp(rd * dt) + (k * Math.Exp(rf * dt)));
            Assert.Equal(expected, discrete, 1e-6 * Math.Abs(expected));
        }
    }

    // QuantLib's case: funding on the previous spot, one funding time, its rate and difference flat.
    private static double Price(
        PerpetualFuturesPayoffType payoff, Period frequency, double rd, double rf, double k, double iDiff) =>
        DiscountingPerpetualFuturesEngine.Calculate(
            new PerpetualFutures(payoff, PerpetualFuturesFundingType.FundingWithPreviousSpot, frequency, new NullCalendar(), Dc),
            Context,
            new FlatForward(Today, rd, Dc),
            new FlatForward(Today, rf, Dc),
            10000.0,
            [0.0],
            [k],
            [iDiff]).NPV!.Value;
}
