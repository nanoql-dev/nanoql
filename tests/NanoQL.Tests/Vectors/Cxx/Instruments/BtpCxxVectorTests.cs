// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Italian bonds' harness vectors, <c>tests/vectors/cxx/instruments/bonds/btp.json</c>: <see cref="CCTEU"/> and
/// <see cref="BTP"/> bonds, their settlement dates, cash flows, rounded accrued amounts, prices and yields;
/// <see cref="RendistatoBasket"/>'s weights and refusals; and <see cref="RendistatoCalculator"/>'s yields, durations,
/// swaps and equivalent swap, with the two quotes on it, and its refusal of a missing discount curve.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BtpCxxVectorTests
{
    [Fact]
    public void BtpCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/bonds/btp");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement[] args = [.. c.Input("arguments").EnumerateArray()];
            switch (c.String("method"))
            {
                case "CCTEU":
                {
                    var bond = new CCTEU(
                        new Date(args[0].GetInt32()),
                        args[1].GetDouble(),
                        context,
                        CashFlowInputs.CurveOf(args[2]),
                        CashFlowInputs.NullableDate(args[3]),
                        CashFlowInputs.NullableDate(args[4]));
                    CheckBond(check, c, bond, CashFlowInputs.CurveOf(args[2]), args[5], context);
                    break;
                }

                case "BTP":
                {
                    BTP bond = BtpOf(args[0], context);
                    CheckBond(check, c, bond, CashFlowInputs.CurveOf(args[1]), args[3], context);
                    double clean = args[2].GetDouble();
                    Date? last = CashFlowInputs.NullableDate(args[3].EnumerateArray().Last());
                    check.Output(c, "yield", () => bond.Yield(clean, context));
                    check.Output(c, "yieldAtLastDate", () => bond.Yield(clean, context, last));
                    break;
                }

                case "RendistatoBasket":
                {
                    RendistatoBasket Basket() => BasketOf(args[0], args[1], args[2], context);
                    if (c.Expected.TryGetProperty("construct", out _))
                    {
                        check.Output(c, "construct", () => Basket());
                        break;
                    }

                    RendistatoBasket basket = Basket();
                    check.Output(c, "size", basket.Size);
                    check.Output(c, "outstanding", basket.Outstanding);
                    check.Output(c, "outstandings", basket.Outstandings);
                    check.Output(c, "weights", basket.Weights);
                    break;
                }

                default:
                {
                    int[] tenor = [.. args[3].EnumerateArray().Select(x => x.GetInt32())];
                    RendistatoCalculator Calculator() => new(
                        BasketOf(args[0], args[1], args[2], context),
                        new Euribor(new Period(tenor[0], (TimeUnit)tenor[1]), CashFlowInputs.CurveOf(args[4])),
                        args[5].ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CurveOf(args[5]),
                        context);
                    if (c.Expected.TryGetProperty("construct", out _))
                    {
                        check.Output(c, "construct", () => Calculator());
                        break;
                    }

                    RendistatoCalculator r = Calculator();
                    check.Output(c, "yield", r.Yield);
                    check.Output(c, "duration", r.Duration);
                    check.Output(c, "yields", r.Yields);
                    check.Output(c, "durations", r.Durations);
                    check.Output(c, "swapLengths", r.SwapLengths);
                    check.Output(c, "swapRates", r.SwapRates);

                    // btp.py leaves out QuantLib's starting guess for the swaps the calculator never solves: their
                    // yields are null here
                    check.Output(c, "solvedSwapYields", r.SwapYields.Where(y => y is not null));
                    check.Output(c, "swapDurations", r.SwapDurations);
                    check.Output(c, "equivalentSwapRate", r.EquivalentSwapRate);
                    check.Output(c, "equivalentSwapYield", r.EquivalentSwapYield);
                    check.Output(c, "equivalentSwapDuration", r.EquivalentSwapDuration);
                    check.Output(c, "equivalentSwapLength", r.EquivalentSwapLength);
                    check.Output(c, "equivalentSwapSpread", r.EquivalentSwapSpread);
                    check.Output(c, "equivalentSwapFixedRate", r.EquivalentSwap.FixedRate);
                    check.Output(c, "equivalentSwapMaturity", r.EquivalentSwap.MaturityDate());
                    check.Output(c, "lengthQuote", new RendistatoEquivalentSwapLengthQuote(r).Value);
                    check.Output(c, "spreadQuote", new RendistatoEquivalentSwapSpreadQuote(r).Value);
                    break;
                }
            }
        }

        check.Complete();
    }

    // The bond's settlement date, cash flows, accrued amounts at the dates and prices on the curve.
    private static void CheckBond(VectorAssert check, VectorCase c, Bond bond, YieldTermStructure curve, JsonElement dates, EvaluationContext context)
    {
        check.Output(c, "settlementDate", bond.SettlementDate(context));
        check.Output(c, "cashflowDates", bond.Cashflows.Select(cf => cf.Date));
        check.Output(c, "cashflowAmounts", bond.Cashflows.Select(cf => (Func<object?>)(() => cf.Amount(context))).ToArray());
        check.Output(
            c,
            "accruedAmounts",
            dates.EnumerateArray().Select(d => (Func<object?>)(() => bond.AccruedAmount(context, CashFlowInputs.NullableDate(d)))).ToArray());
        BondResults results = DiscountingBondEngine.Calculate(bond, context, curve);
        check.Output(c, "cleanPrice", () => bond.CleanPrice(results, context));
        check.Output(c, "dirtyPrice", () => bond.DirtyPrice(results, context));
    }

    private static BTP BtpOf(JsonElement spec, EvaluationContext context)
    {
        var maturity = new Date(spec.GetProperty("maturity").GetInt32());
        double rate = spec.GetProperty("rate").GetDouble();
        Date? start = CashFlowInputs.NullableDate(spec.GetProperty("start"));
        Date? issue = CashFlowInputs.NullableDate(spec.GetProperty("issue"));
        JsonElement redemption = spec.GetProperty("redemption");
        return redemption.ValueKind == JsonValueKind.Null
            ? new BTP(maturity, rate, context, start, issue)
            : new BTP(maturity, rate, redemption.GetDouble(), context, start, issue);
    }

    private static RendistatoBasket BasketOf(JsonElement bonds, JsonElement outstandings, JsonElement prices, EvaluationContext context) => new(
        bonds.EnumerateArray().Select(b => BtpOf(b, context)),
        outstandings.EnumerateArray().Select(x => x.GetDouble()),
        prices.EnumerateArray().Select(x => x.GetDouble()));
}
