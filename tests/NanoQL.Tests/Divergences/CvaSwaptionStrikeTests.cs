// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The CVA engine's swaptionlets (C3's review, C3-O2, ruled by the user on 2026-10-24, (a)): QuantLib strikes every
/// swaptionlet and its reverse at the swap's risk-free fair rate, <c>-baseSwapRate * legNPV[1] / legNPV[0]</c>
/// (<c>cvaswapengine.cpp:147</c>, <c>:170</c>, <c>:179</c>), which the fixed rate cancels out of, so its adjustment is
/// the same whatever the swap's rate. On the harness, on the vectors' 5-year payer from 17 January 2025 on 1e7 (Black
/// 15%, a flat 2% hazard rate, 40% recovery, no investor curve), QuantLib's adjustment is -2,625.223121590 at 1%, at
/// the fair rate 0.03147227737464738 and at 5%, where the swap is worth 990,581.19 and -854,739.96, and the
/// receiver's -2,617.287664647 at each. nanoql strikes them at the swap's fixed rate, the contract's own exposure: its
/// adjustment is QuantLib's at the fair rate (the harness's vectors, each swap at its fair rate), and follows the
/// exposure off par: -21,968.948298009 at 1% and -75.016505939 at 5%. The independent check off par is the swaptions'
/// parity: each period's payer and receiver swaptions differ by the forward swap at their strike, so a payer swap's
/// adjustment less the receiver's at one rate is minus the forward swaps at that rate weighted by the parties' losses
/// given default over the periods, at every volatility. The fair rate keeps QuantLib's (C3's review, C3R2-O1), its
/// first-order root of the adjusted NPV on swaptionlets struck at the risk-free fair rate: QuantLib's
/// 0.031415371873538545 at 1%, 0.03141537187353853 at the fair rate and 0.03141537187353854 at 5%, nanoql's to the bit,
/// the adjusted NPV at it -26.87; on the contract's strikes it would be 0.030996068704439883 at 1%, the adjusted NPV at
/// it 19,112.14, and 0.03147065128358219 at 5%.
/// </summary>
public class CvaSwaptionStrikeTests
{
    private const double QuantLibsPayerAdjustment = -2625.223121590;

    [Fact]
    public void OffParTheAdjustmentFollowsTheExposure()
    {
        VectorCase c = Case("5Y payer, Black, no investor curve");
        EvaluationContext context = c.Context();
        double fair = c.Input("swap").GetProperty("fixedRate").GetDouble();

        double atPar = Adjustment(c, context, fair);
        double itm = Adjustment(c, context, 0.01);
        double otm = Adjustment(c, context, 0.05);

        Assert.True(Math.Abs(atPar - QuantLibsPayerAdjustment) <= 1e-6, $"at par {atPar}");
        Assert.True(itm < atPar && atPar < otm && otm < 0.0, $"{itm} {atPar} {otm}");
    }

    [Theory]
    [InlineData(0.01, 0.15)]
    [InlineData(0.01, 0.30)]
    [InlineData(0.05, 0.15)]
    [InlineData(0.05, 0.30)]
    public void APayerLessAReceiverIsTheForwardSwapsAtTheRate(double rate, double volatility)
    {
        VectorCase c = Case("5Y payer, Black, no investor curve");
        EvaluationContext context = c.Context();
        YieldTermStructure discount = IndexInputs.CurveOf(c.Input("discountCurve"))!;
        VanillaSwap payer = SwapAt(c, context, rate, SwapType.Payer);
        VanillaSwap receiver = SwapAt(c, context, rate, SwapType.Receiver);
        Date start = payer.StartDate();
        Assert.True(start > context.EvaluationDate);
        var ctpty = new FlatHazardRate(start, 0.02, new Actual365Fixed());
        var investor = new FlatHazardRate(start, 0.01, new Actual365Fixed());
        const double ctptyRecovery = 0.4, investorRecovery = 0.3;

        double Adjusted(VanillaSwap swap) =>
            CounterpartyAdjSwapEngine.Calculate(
                swap, context, discount, volatility, ctpty, ctptyRecovery, investor, investorRecovery).NPV!.Value
            - DiscountingSwapEngine.Calculate(swap, context, discount).NPV!.Value;

        // the engine's periods: from the price date, the counterparty curve's reference date, to each fixed payment
        var index = (IborIndex)((FloatingRateCoupon)payer.FloatingLeg[0]).Index;
        Date[] payDates = [.. payer.FixedLeg.Select(cf => cf.Date).Where(d => d >= start)];
        double expected = 0.0;
        Date periodStart = start;
        foreach (Date payDate in payDates)
        {
            VanillaSwap forward = new MakeVanillaSwap(
                    new Period(payDates[^1].SerialNumber - periodStart.SerialNumber, TimeUnit.Days), index)
                .WithFixedRate(rate)
                .WithType(SwapType.Payer)
                .WithNominal(payer.Nominal())
                .WithEffectiveDate(periodStart)
                .WithTerminationDate(payDates[^1])
                .ToVanillaSwap(context);
            double weight = ((1.0 - ctptyRecovery) * ctpty.DefaultProbability(periodStart, payDate))
                + ((1.0 - investorRecovery) * investor.DefaultProbability(periodStart, payDate));
            expected -= weight * DiscountingSwapEngine.Calculate(forward, context, discount).NPV!.Value;
            periodStart = payDate;
        }

        double actual = Adjusted(payer) - Adjusted(receiver);
        Assert.True(Math.Abs(expected) > 1000.0, $"{expected}");
        Assert.True(Math.Abs(actual - expected) <= 1e-8 * Math.Abs(expected), $"{actual} {expected}");
    }

    [Theory]
    [InlineData(0.01, 0.031415371873538545)]
    [InlineData(0.03147227737464738, 0.03141537187353853)]
    [InlineData(0.05, 0.03141537187353854)]
    public void TheFairRateIsQuantLibsAtEveryRate(double rate, double quantLibsFairRate)
    {
        VectorCase c = Case("5Y payer, Black, no investor curve");
        EvaluationContext context = c.Context();
        FixedVsFloatingSwapResults results = CounterpartyAdjSwapEngine.Calculate(
            SwapAt(c, context, rate, SwapType.Payer),
            context,
            IndexInputs.CurveOf(c.Input("discountCurve"))!,
            c.Input("exposure").GetProperty("volatility").GetDouble(),
            CreditInputs.CurveOf(c.Input("ctptyCurve"), context),
            c.Double("ctptyRecoveryRate"),
            null,
            c.Double("investorRecoveryRate"));

        Assert.Equal(quantLibsFairRate, results.FairRate);
    }

    private static VectorCase Case(string name) =>
        VectorFile.Load("cxx/pricingengines/swap/cvaswapengine").Cases.Single(x => x.Name == name);

    private static VanillaSwap SwapAt(VectorCase c, EvaluationContext context, double rate, SwapType type)
    {
        JsonNode spec = JsonNode.Parse(c.Input("swap").GetRawText())!;
        spec["fixedRate"] = rate;
        spec["type"] = (int)type;
        return SwapInputs.VanillaSwapOf(JsonSerializer.SerializeToElement(spec), context);
    }

    private static double Adjustment(VectorCase c, EvaluationContext context, double rate)
    {
        VanillaSwap swap = SwapAt(c, context, rate, SwapType.Payer);
        YieldTermStructure discount = IndexInputs.CurveOf(c.Input("discountCurve"))!;
        return CounterpartyAdjSwapEngine.Calculate(
                swap,
                context,
                discount,
                c.Input("exposure").GetProperty("volatility").GetDouble(),
                CreditInputs.CurveOf(c.Input("ctptyCurve"), context),
                c.Double("ctptyRecoveryRate"),
                null,
                c.Double("investorRecoveryRate")).NPV!.Value
            - DiscountingSwapEngine.Calculate(swap, context, discount).NPV!.Value;
    }
}
