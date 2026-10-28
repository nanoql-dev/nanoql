// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's n-th-to-default vectors, <c>tests/vectors/cxx/experimental/credit/integralntdengine.json</c>:
/// swaps of every order on constant-loss models, priced by the integral engine, their inspectors, an expired swap and
/// the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IntegralNtdEngineCxxVectorTests
{
    [Fact]
    public void IntegralNtdEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/integralntdengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            YieldTermStructure discount = CreditInputs.DiscountOf(c.Input("discount"))!;
            JsonElement model = c.Input("model");
            if (c.Name == "expired and refused")
            {
                Basket old = PortfolioInputs.BoundBasketOf(c.Input("old"), model, context);
                Basket b = PortfolioInputs.BoundBasketOf(c.Input("basket"), model, context);
                NthToDefault expired = Ntd(old, c.Input("expired"), context);
                check.Output(c, "isExpired", expired.IsExpired(context));
                check.Output(c, "expired", Results(IntegralNtdEngine.Calculate(expired, context, new Period(1, TimeUnit.Weeks), discount)));
                check.Output(c, "refused", Each(c.Input("refused").EnumerateArray(), s => Inspect(Ntd(b, s, context))));
                continue;
            }

            Basket basket = PortfolioInputs.BoundBasketOf(c.Input("basket"), model, context);
            JsonElement[] swaps = [.. c.Input("swaps").EnumerateArray()];
            Period[] steps = [.. c.Input("steps").EnumerateArray().Select(PortfolioInputs.PeriodOf)];
            check.Output(c, "inspect", Each(swaps, s => Inspect(Ntd(basket, s, context))));
            check.Output(c, "calculate", Each(
                steps.SelectMany(step => swaps.Select(s => (step, s))),
                p => Results(IntegralNtdEngine.Calculate(Ntd(basket, p.s, context), context, p.step, discount))));
        }

        check.Complete();
    }

    private static NthToDefault Ntd(Basket basket, JsonElement s, EvaluationContext context) => new(
        basket,
        s.GetProperty("n").GetInt32(),
        (ProtectionSide)s.GetProperty("side").GetInt32(),
        CreditInputs.ScheduleOf(s.GetProperty("schedule"), context),
        s.GetProperty("upfrontRate").GetDouble(),
        s.GetProperty("premiumRate").GetDouble(),
        CreditInputs.DayCounterOf(s.GetProperty("dayCounter")),
        s.GetProperty("nominal").GetDouble(),
        s.GetProperty("settlePremiumAccrual").GetBoolean());

    private static object[] Inspect(NthToDefault x) =>
        [x.Rank, x.Nominal, x.Premium, (int)x.Side, x.BasketSize, x.Maturity];

    private static object?[] Results(NthToDefaultResults r) =>
    [
        r.NPV, r.FairPremium, r.PremiumLegNPV, r.ProtectionLegNPV,
        r.AdditionalResults.TryGetValue("fairPremium", out object? f) ? f : null,
        r.AdditionalResults.TryGetValue("premiumLegNPV", out object? p) ? p : null,
        r.AdditionalResults.TryGetValue("protectionLegNPV", out object? q) ? q : null,
    ];

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
