// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The ISDA model's implied hazard rate and conventional spread on a weekend. QuantLib's flat curve is
/// <c>FlatHazardRate(0, WeekendsOnly(), …)</c> (<c>creditdefaultswap.cpp:352, 393</c>), which rolls a weekend to
/// Monday, and its own ISDA engine then refuses it: on Saturday 15 March 2025, for <c>MakeCreditDefaultSwap(5Y, 1%)</c>
/// on a discount curve flat at 3%, Actual/365 (Fixed), at a recovery of 40%, both throw "probability term structure
/// reference date (March 17th, 2025 should be evaluation date (March 15th, 2025)" (QuantLib's defect, R8). nanoql's
/// ISDA model dates its curve on the evaluation date. QuantLib's ISDA engine on QuantLib's <c>FlatHazardRate</c> dated
/// there, solved by QuantLib's <c>Brent</c> on the wheel, gives the hazard rate 0.016833960395149727 for a zero target,
/// and the fair spread at the conventional root, 0.009999999999972463; on the Friday before, the same oracle gives
/// QuantLib's own 0.01683399613622196 and 0.009999999999972453. The mid-point model keeps QuantLib's curve and its
/// weekend answers, 0.016853086104458855 and 0.009999999999985819.
/// </summary>
public class IsdaImpliedWeekendTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.March, 2025) };

    [Fact]
    public void TheImpliedHazardRateIsSolvedOnTheEvaluationDate()
    {
        CreditDefaultSwap cds = Swap();
        var discountCurve = new FlatForward(Context.EvaluationDate, 0.03, new Actual365Fixed());

        // QuantLib's: "probability term structure reference date (March 17th, 2025 should be evaluation date ..."
        double isda = cds.ImpliedHazardRate(
            0.0, discountCurve, new Actual365Fixed(), Context, 0.4, 1e-8, CreditDefaultSwapPricingModel.ISDA);
        double midpoint = cds.ImpliedHazardRate(
            0.0, discountCurve, new Actual365Fixed(), Context, 0.4, 1e-8, CreditDefaultSwapPricingModel.Midpoint);

        Assert.True(Tolerance.Within(0.016833960395149727, isda), $"{isda}");
        Assert.True(Tolerance.Within(0.016853086104458855, midpoint), $"{midpoint}");
    }

    [Fact]
    public void TheConventionalSpreadIsTheFairSpreadAtTheRoot()
    {
        CreditDefaultSwap cds = Swap();
        var discountCurve = new FlatForward(Context.EvaluationDate, 0.03, new Actual365Fixed());

        // QuantLib's: "probability term structure reference date (March 17th, 2025 should be evaluation date ..."
        double isda = cds.ConventionalSpread(
            0.4, discountCurve, new Actual365Fixed(), Context, CreditDefaultSwapPricingModel.ISDA);
        double midpoint = cds.ConventionalSpread(
            0.4, discountCurve, new Actual365Fixed(), Context, CreditDefaultSwapPricingModel.Midpoint);

        Assert.True(Tolerance.Within(0.009999999999972463, isda), $"{isda}");
        Assert.True(Tolerance.Within(0.009999999999985819, midpoint), $"{midpoint}");
    }

    private static CreditDefaultSwap Swap() =>
        new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01).ToCreditDefaultSwap(Context);
}
