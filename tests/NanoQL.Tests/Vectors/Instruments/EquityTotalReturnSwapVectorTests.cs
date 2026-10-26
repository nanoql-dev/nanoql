// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The EquityTotalReturnSwap vectors, <c>tests/vectors/instruments/equitytotalreturnswap.json</c>: receiver and payer
/// swaps against USD Libor and SOFR priced by <see cref="DiscountingSwapEngine"/>, their legs' NPVs and cash flows, the
/// fair margin, and the check of a negative nominal.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EquityTotalReturnSwapVectorTests
{
    [Fact]
    public void EquityTotalReturnSwapVector()
    {
        var file = VectorFile.Load("instruments/equitytotalreturnswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement m = c.Input("market"), s = c.Input("swap");
            YieldTermStructure interest = CurveOf(m.GetProperty("interest"));
            var calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
            var equity = new EquityIndex(
                "eqIndex", calendar, new USDCurrency(), interest, CurveOf(m.GetProperty("dividend")), m.GetProperty("spot").GetDouble());
            Schedule schedule = CashFlowInputs.ScheduleOf(s.GetProperty("schedule"), context);
            var type = (SwapType)s.GetProperty("type").GetInt32();
            double nominal = s.GetProperty("nominal").GetDouble(), margin = s.GetProperty("margin").GetDouble();
            double gearing = s.GetProperty("gearing").GetDouble();
            int delay = s.GetProperty("paymentDelay").GetInt32();
            var swap = new Lazy<EquityTotalReturnSwap>(() => s.GetProperty("index").GetString() == "Sofr"
                ? new EquityTotalReturnSwap(
                    type, nominal, schedule, equity, new Sofr(interest), new Actual365Fixed(), margin, gearing, schedule.Calendar,
                    BusinessDayConvention.Following, delay)
                : new EquityTotalReturnSwap(
                    type, nominal, schedule, equity, new USDLibor(new Period(3, TimeUnit.Months), interest), new Actual365Fixed(),
                    margin, context, gearing, schedule.Calendar, BusinessDayConvention.Following, delay));
            var r = new Lazy<SwapResults>(() => DiscountingSwapEngine.Calculate(swap.Value, context, interest));
            check.Output(c, "NPV", () => r.Value.NPV);
            check.Output(c, "equityLegNPV", () => EquityTotalReturnSwap.EquityLegNPV(r.Value));
            check.Output(c, "interestRateLegNPV", () => EquityTotalReturnSwap.InterestRateLegNPV(r.Value));
            check.Output(c, "fairMargin", () => swap.Value.FairMargin(r.Value));
            check.Output(c, "startDate", () => swap.Value.StartDate().SerialNumber);
            check.Output(c, "maturityDate", () => swap.Value.MaturityDate().SerialNumber);
            check.Output(c, "equityLegDates", () => swap.Value.EquityLeg.Select(f => (object)f.Date.SerialNumber).ToArray());
            check.Output(c, "equityLegAmounts", () => swap.Value.EquityLeg.Select(f => (object)f.Amount(context)).ToArray());
            check.Output(c, "interestRateLegDates", () => swap.Value.InterestRateLeg.Select(f => (object)f.Date.SerialNumber).ToArray());
            check.Output(c, "interestRateLegAmounts", () => swap.Value.InterestRateLeg.Select(f => (object)f.Amount(context)).ToArray());
        }

        check.Complete();
    }
}
