// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The analytic Hagan pricer's normalisation (the S-cms plan's CMR5 (xxii)): Hagan's analytic forms add
/// sign·G′(R0)·annuity·(…) to (discount/annuity)·C(K) (<c>conundrumpricer.cpp:520-547</c>, <c>:573-576</c>), which
/// presumes G(R0) = P(Tp)/A0, where the numeric pricer divides by G(R0) (<c>:472-490</c>). The shift g-functions' Z
/// omits P(0,Tp)/P(0,T0) (<c>:783-786</c>), so their analytic convexity term is high by exactly P(T0)/P(Tp) against the
/// numeric twin: on the wheel, on QuantLib's cms.cpp market (flat 5%, its at-the-money matrix), a 1-year-delay coupon
/// fixing in 5 years prices 0.05235802683487254 on the analytic parallel shifts against 0.052312587540360674 numeric, a
/// convexity adjustment 1.045 times the numeric one where the standard model's is 0.994. On an index discounting on a
/// curve of its own (3% against a 5% forwarding curve), every analytic model is 7.9% above its numeric twin. nanoql
/// scales the shift models' analytic term by P(0,Tp)/P(0,T0) on their own curve, and on an exogenous discounting curve
/// takes discount·G′(R0)/G(R0) for annuity·G′(R0), as the numeric pricer normalises its integrand; the standard and
/// exact-yield models on one curve keep QuantLib's values. QuantLib's check: its numeric twins, which neither part
/// changes.
/// </summary>
public class HaganAnalyticNormalisationTests
{
    // (start years, delay months, QuantLib's forward, P(T0)/P(Tp), QuantLib's analytic parallel shifts' rate)
    private static readonly (int Start, int Delay, double Forward, double Ratio, double Analytic)[] Single =
    [
        (5, 3, 0.05129908529472778, 1.0125437747901869, 0.0525060911813962),
        (5, 6, 0.05129908529472778, 1.025104460979125, 0.05245793254988236),
        (5, 12, 0.05129908529472778, 1.0512710963760241, 0.05235802683487254),
        (5, 24, 0.05129908529472778, 1.1051709180756475, 0.0521400696344117),
        (20, 3, 0.05131568199511156, 1.012543774790187, 0.05510845752144073),
        (20, 6, 0.05131568199511156, 1.025244895866361, 0.054957678658336055),
        (20, 12, 0.05131568199511156, 1.051415115979385, 0.054643808123361434),
        (20, 24, 0.05131568199511156, 1.1053223217219723, 0.05395905599777265),
    ];

    private static readonly SwapIndex OneCurve = new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), CmsInputs.CmsCurve);

    [Fact]
    public void TheShiftModelsTermIsQuantLibsTimesTheirDiscountRatio()
    {
        foreach ((int start, int delay, double forward, double ratio, double analytic) in Single)
        {
            CmsCoupon c = CmsInputs.CmsCoupon(OneCurve, start, delay);
            double f = OneCurve.Fixing(c.FixingDate(), CmsInputs.CmsContext);
            Assert.True(Math.Abs(f - forward) <= 1e-15 * forward, $"{f}");
            foreach (GFunctionFactoryYieldCurveModel model in new[] { GFunctionFactoryYieldCurveModel.ParallelShifts, GFunctionFactoryYieldCurveModel.NonParallelShifts })
            {
                double term = Rate(c, new AnalyticHaganPricer(CmsInputs.CmsAtm(), model, 0.0)) - f;
                double quantLibs = analytic - forward;
                Assert.True(Math.Abs((term / (quantLibs / ratio)) - 1.0) <= 1e-10, $"{start}y {delay}m {model}: {term} against {quantLibs / ratio}");
            }
        }
    }

    [Fact]
    public void TheNumericValuesStand()
    {
        CmsCoupon c = CmsInputs.CmsCoupon(OneCurve, 5, 12);
        foreach ((GFunctionFactoryYieldCurveModel model, double quantLibs) in new[]
        {
            (GFunctionFactoryYieldCurveModel.Standard, 0.052312570530036476),
            (GFunctionFactoryYieldCurveModel.ParallelShifts, 0.052312587540360674),
            (GFunctionFactoryYieldCurveModel.NonParallelShifts, 0.052312587540360674),
        })
        {
            double r = Rate(c, new NumericHaganPricer(CmsInputs.CmsAtm(), model, 0.0));
            Assert.True(Math.Abs(r - quantLibs) <= 1e-14 * quantLibs, $"{model}: {r}");
        }

        // and the standard model's analytic value, on one curve
        double standard = Rate(c, new AnalyticHaganPricer(CmsInputs.CmsAtm(), GFunctionFactoryYieldCurveModel.Standard, 0.0));
        Assert.True(Math.Abs(standard - 0.052306351377805635) <= 1e-15 * standard, $"{standard}");
    }

    [Fact]
    public void TheShiftModelsAgreeWithTheStandardModel()
    {
        foreach ((int start, int delay, _, _, _) in Single)
        {
            CmsCoupon c = CmsInputs.CmsCoupon(OneCurve, start, delay);
            double standard = AnalyticOverNumeric(c, GFunctionFactoryYieldCurveModel.Standard);
            foreach (GFunctionFactoryYieldCurveModel model in new[] { GFunctionFactoryYieldCurveModel.ParallelShifts, GFunctionFactoryYieldCurveModel.NonParallelShifts })
            {
                double ratio = AnalyticOverNumeric(c, model);
                Assert.True(Math.Abs(ratio - standard) <= 3e-4, $"{start}y {delay}m {model}: {ratio} against {standard}");
            }
        }
    }

    [Fact]
    public void AnExogenousDiscountingCurveIsNormalised()
    {
        YieldTermStructure discount = new FlatForward(CmsInputs.CmsToday, 0.03, new Actual365Fixed());
        SwapIndex dual = new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), CmsInputs.CmsCurve, discount);
        SwapIndex same = new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), CmsInputs.CmsCurve, CmsInputs.CmsCurve);
        foreach (int start in new[] { 5, 20 })
        {
            // the ratio the normalised single curve's, an index discounting on its own forwarding curve
            foreach (GFunctionFactoryYieldCurveModel model in new[] { GFunctionFactoryYieldCurveModel.Standard, GFunctionFactoryYieldCurveModel.ParallelShifts })
            {
                double single = AnalyticOverNumeric(CmsInputs.CmsCoupon(same, start, 12), model);
                double onDual = AnalyticOverNumeric(CmsInputs.CmsCoupon(dual, start, 12), model);
                Assert.True(Math.Abs(onDual - single) <= 1e-4, $"{start}y {model}: {onDual} against {single}");
            }

            // the switch's jump: an index given its forwarding curve to discount on, against one without
            CmsCoupon cOne = CmsInputs.CmsCoupon(OneCurve, start, 12), cSame = CmsInputs.CmsCoupon(same, start, 12);
            double f = OneCurve.Fixing(cOne.FixingDate(), CmsInputs.CmsContext);
            var pricer = new AnalyticHaganPricer(CmsInputs.CmsAtm(), GFunctionFactoryYieldCurveModel.Standard, 0.0);
            double termOne = Rate(cOne, pricer) - f, termSame = Rate(cSame, pricer) - f;
            Assert.True(Math.Abs(termSame - termOne) <= 2.9e-4 * termOne, $"{start}y: {termSame} against {termOne}");
        }

        // QuantLib's on the dual curve: the standard model's analytic adjustment 7.9% above its numeric one, 5 years
        Assert.True(((0.05212455399855547 - 0.051040894437015635) / (0.05204494290162233 - 0.051040894437015635)) > 1.079);
    }

    private static double AnalyticOverNumeric(CmsCoupon c, GFunctionFactoryYieldCurveModel model)
    {
        double f = c.SwapIndex.Fixing(c.FixingDate(), CmsInputs.CmsContext);
        double analytic = Rate(c, new AnalyticHaganPricer(CmsInputs.CmsAtm(), model, 0.0)) - f;
        double numeric = Rate(c, new NumericHaganPricer(CmsInputs.CmsAtm(), model, 0.0)) - f;
        return analytic / numeric;
    }

    private static double Rate(CmsCoupon c, CmsCouponPricer pricer) => c.SetPricer(pricer).Rate(CmsInputs.CmsContext);
}
