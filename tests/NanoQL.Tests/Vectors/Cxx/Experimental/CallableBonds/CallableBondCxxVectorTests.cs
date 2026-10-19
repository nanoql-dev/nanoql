// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's callable-bond vectors, <c>tests/vectors/cxx/experimental/callablebonds/callablebond.json</c>: bonds
/// on QuantLib's empty calendar and payment day counter, the zero-coupon tree and Black engines, the Black engine on a
/// volatility structure, the spread family on the tree engines with explicit settlement dates, accuracies, guesses and
/// bumps, and the implied volatility (<see cref="CallableBondInputs"/>). A refused construction is the case's one
/// output, "construct".
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CallableBondCxxVectorTests
{
    [Fact]
    public void CallableBondCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/callablebonds/callablebond");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("construct", out _))
            {
                check.Output(c, "construct", () => CallableBondInputs.BondOf(c.Input("bond"), context));
                continue;
            }

            CallableBond bond = CallableBondInputs.BondOf(c.Input("bond"), context);
            Func<CallableBond, EvaluationContext, double, CallableBondResults> engine =
                CallableBondInputs.EngineOf(c.Input("engine"));
            check.Output(c, "callabilityDates", bond.Callability.Select(x => x.Date));
            check.Output(c, "callabilityTypes", bond.Callability.Select(x => x.Type));
            check.Output(c, "callabilityPrices", bond.Callability.Select(x => x.Price.Amount));
            check.Output(c, "cashflowDates", bond.Cashflows.Select(cf => cf.Date));
            check.Output(c, "cashflowAmounts", BondInputs.Each(bond.Cashflows, cf => cf.Amount(context)));
            check.Output(c, "settlementDate", () => bond.SettlementDate(context));
            check.Output(c, "maturityDate", bond.MaturityDate());
            check.Output(c, "accruedAmount", () => bond.AccruedAmount(context));

            CallableBondResults? results = null;
            Exception? error = null;
            try
            {
                results = engine(bond, context, 0.0);
            }
            catch (Exception e) when (e is ArgumentException or NanoQLException)
            {
                error = e;
            }

            CallableBondResults Results() => results ?? throw error!;
            check.Output(c, "NPV", () => Results().NPV);
            check.Output(c, "settlementValue", () => Results().SettlementValue);
            check.Output(c, "cleanPrice", () => bond.CleanPrice(Results(), context));
            check.Output(c, "dirtyPrice", () => bond.DirtyPrice(Results(), context));
            if (c.Inputs.TryGetProperty("oas", out _))
            {
                CheckOAS(check, c, bond, context, engine);
            }

            if (c.Inputs.TryGetProperty("implied", out _))
            {
                CallableBondInputs.CheckImplied(check, c, bond, context);
            }
        }

        check.Complete();
    }

    // The spread family with every argument the harness passes: the settlement date, accuracy, evaluations and guess.
    private static void CheckOAS(
        VectorAssert check,
        VectorCase c,
        CallableBond bond,
        EvaluationContext context,
        Func<CallableBond, EvaluationContext, double, CallableBondResults> engine)
    {
        JsonElement spec = c.Input("oas");
        YieldTermStructure curve = ModelInputs.CurveOf(spec.GetProperty("curve"));
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        var comp = (Compounding)spec.GetProperty("compounding").GetInt32();
        var freq = (Frequency)spec.GetProperty("frequency").GetInt32();
        Date? settlement = CashFlowInputs.NullableDate(spec.GetProperty("settlementDate"));
        double s = spec.GetProperty("spread").GetDouble();
        double bump = spec.GetProperty("bump").GetDouble();
        check.Output(c, "OAS", () => bond.OAS(
            spec.GetProperty("cleanPrice").GetDouble(),
            curve,
            dc,
            comp,
            freq,
            engine,
            context,
            settlement,
            spec.GetProperty("accuracy").GetDouble(),
            spec.GetProperty("maxIterations").GetInt32(),
            spec.GetProperty("guess").GetDouble()));
        check.Output(c, "cleanPriceOAS", () => bond.CleanPriceOAS(s, curve, dc, comp, freq, engine, context, settlement));
        check.Output(
            c, "effectiveDuration", () => bond.EffectiveDuration(s, curve, dc, comp, freq, engine, context, bump));
        check.Output(
            c, "effectiveConvexity", () => bond.EffectiveConvexity(s, curve, dc, comp, freq, engine, context, bump));
    }
}
