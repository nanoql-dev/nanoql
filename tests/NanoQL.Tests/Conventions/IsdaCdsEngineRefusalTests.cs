// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The ISDA engine's refusals, with QuantLib's texts (<c>isdacdsengine.cpp:54-97, 113-148, 206-210, 323</c>): an
/// option outside its enum is an <see cref="ArgumentOutOfRangeException"/> naming it; a missing curve, and a side
/// outside its enum, a <see cref="NanoQLException"/>; a curve that isn't ISDA compliant (its day counter, its reference
/// date, its interpolation) an <see cref="ArgumentException"/> naming the curve, the reference-date texts with
/// QuantLib's unclosed parenthesis; and a swap the engine isn't specified for (no accrual settled, the payment at the
/// period's end, a claim other than a face-value one, a coupon day counter other than Actual/365 (Fixed) or Actual/360)
/// an <see cref="ArgumentException"/> naming the swap.
/// </summary>
public class IsdaCdsEngineRefusalTests
{
    private static readonly Date Today = CreditFixture.Today;

    [Fact]
    public void OptionsAndMissingCurvesAreRefused()
    {
        EvaluationContext context = CreditFixture.Context();
        CreditDefaultSwap cds = CreditFixture.Swap(false, 0.01);
        FlatHazardRate probability = CreditFixture.Probability();
        FlatForward discount = CreditFixture.Discount();

        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => IsdaCdsEngine.Calculate(cds, context, probability, 0.4, discount, null, (IsdaCdsEngineNumericalFix)5));
        Assert.Equal("numericalFix", e.ParamName);
        Assert.StartsWith("numerical fix must be None or Taylor", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentOutOfRangeException>(
            () => IsdaCdsEngine.Calculate(
                cds, context, probability, 0.4, discount, null, IsdaCdsEngineNumericalFix.Taylor, (IsdaCdsEngineAccrualBias)5));
        Assert.Equal("accrualBias", e.ParamName);
        Assert.StartsWith("accrual bias must be HalfDayBias or NoBias", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentOutOfRangeException>(
            () => IsdaCdsEngine.Calculate(
                cds,
                context,
                probability,
                0.4,
                discount,
                null,
                IsdaCdsEngineNumericalFix.Taylor,
                IsdaCdsEngineAccrualBias.HalfDayBias,
                (IsdaCdsEngineForwardsInCouponPeriod)5));
        Assert.Equal("forwardsInCouponPeriod", e.ParamName);
        Assert.StartsWith("forwards in coupon period must be Flat or Piecewise", e.Message, StringComparison.Ordinal);

        Assert.Equal(
            "no discount term structure set",
            Assert.Throws<NanoQLException>(() => IsdaCdsEngine.Calculate(cds, context, probability, 0.4, null)).Message);
        Assert.Equal(
            "no probability term structure set",
            Assert.Throws<NanoQLException>(() => IsdaCdsEngine.Calculate(cds, context, null, 0.4, discount)).Message);
    }

    [Fact]
    public void TheCurvesMustBeActual365OnTheEvaluationDate()
    {
        EvaluationContext context = CreditFixture.Context();
        CreditDefaultSwap cds = CreditFixture.Swap(false, 0.01);
        Date tomorrow = Today + 1;

        AssertArgument(
            "discountCurve",
            "yield term structure day counter (Actual/360) should be Act/365(Fixed)",
            () => IsdaCdsEngine.Calculate(cds, context, CreditFixture.Probability(), 0.4, new FlatForward(Today, 0.03, new Actual360())));
        AssertArgument(
            "probability",
            "probability term structure day counter (Actual/360) should be Act/365(Fixed)",
            () => IsdaCdsEngine.Calculate(cds, context, new FlatHazardRate(Today, 0.02, new Actual360()), 0.4, CreditFixture.Discount()));
        AssertArgument(
            "discountCurve",
            $"yield term structure reference date ({tomorrow} should be evaluation date ({Today})",
            () => IsdaCdsEngine.Calculate(cds, context, CreditFixture.Probability(), 0.4, new FlatForward(tomorrow, 0.03, new Actual365Fixed())));
        AssertArgument(
            "probability",
            $"probability term structure reference date ({tomorrow} should be evaluation date ({Today})",
            () => IsdaCdsEngine.Calculate(cds, context, new FlatHazardRate(tomorrow, 0.02, new Actual365Fixed()), 0.4, CreditFixture.Discount()));
    }

    [Fact]
    public void TheSwapMustBeOfTheModelsKind()
    {
        EvaluationContext context = CreditFixture.Context();
        Schedule schedule = CreditFixture.ScheduleOf(false);
        var bond = new FixedRateBond(
            3, 100.0, schedule, [0.05], new Actual365Fixed(), BusinessDayConvention.Following, 100.0, schedule[0]);

        AssertArgument(
            "cds",
            "ISDA engine not compatible with non accrual paying CDS",
            () => Price(Swap(schedule, settlesAccrual: false), context));
        AssertArgument(
            "cds",
            "ISDA engine not compatible with end period payment",
            () => Price(Swap(schedule, paysAtDefaultTime: false), context));
        AssertArgument(
            "cds",
            "ISDA engine not compatible with non face value claim",
            () => Price(Swap(schedule, claim: new FaceValueAccrualClaim(bond)), context));

        var unknown = new CreditDefaultSwap(
            (ProtectionSide)2, 1e6, 0.01, schedule, BusinessDayConvention.Following, new Actual360());
        Assert.Equal("unknown protection side", Assert.Throws<NanoQLException>(() => Price(unknown, context)).Message);
    }

    [Fact]
    public void TheInterpolationsAndTheCouponDayCounterAreChecked()
    {
        EvaluationContext context = CreditFixture.Context();
        CreditDefaultSwap cds = CreditFixture.Swap(false, 0.01);
        Date[] dates = [Today, Today + 3650];

        AssertArgument(
            "discountCurve",
            "Yield curve must be flat forward interpolated",
            () => IsdaCdsEngine.Calculate(
                cds, context, CreditFixture.Probability(), 0.4, new InterpolatedZeroCurve<Linear>(dates, [0.03, 0.03], new Actual365Fixed())));
        AssertArgument(
            "probability",
            "Credit curve must be flat forward interpolated",
            () => IsdaCdsEngine.Calculate(
                cds, context, new InterpolatedDefaultDensityCurve<Linear>(dates, [0.02, 0.02], new Actual365Fixed()), 0.4, CreditFixture.Discount()));

        DayCounter thirty = new Thirty360(Thirty360Convention.BondBasis);
        var thirtyCds = new CreditDefaultSwap(
            ProtectionSide.Buyer, 1e6, 0.01, CreditFixture.ScheduleOf(false), BusinessDayConvention.Following, thirty);
        AssertArgument(
            "cds",
            "ISDA engine requires a coupon day counter Act/365Fixed or Act/360 (30/360 (Bond Basis))",
            () => Price(thirtyCds, context));
    }

    private static CreditDefaultSwap Swap(
        Schedule schedule, bool settlesAccrual = true, bool paysAtDefaultTime = true, Claim? claim = null) =>
        new(
            ProtectionSide.Buyer,
            1e6,
            0.01,
            schedule,
            BusinessDayConvention.Following,
            new Actual360(),
            settlesAccrual,
            paysAtDefaultTime,
            claim: claim);

    private static CreditDefaultSwapResults Price(CreditDefaultSwap cds, EvaluationContext context) =>
        IsdaCdsEngine.Calculate(cds, context, CreditFixture.Probability(), 0.4, CreditFixture.Discount());

    private static void AssertArgument(string paramName, string text, Action build)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(build);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(text, e.Message, StringComparison.Ordinal);
    }
}
