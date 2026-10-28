// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The credit portfolio vectors' shared inputs, as <c>tools/vectors/gen/cxx/experimental/credit/defaultevent.py</c>
/// writes them: currencies, default types, keys, events, issuers, pools, baskets and loss models, and the summaries the
/// harness returns of them.
/// </summary>
internal static class PortfolioInputs
{
    /// <summary>The currency a code names, or <see langword="null"/>, QuantLib's empty currency, for JSON null.</summary>
    /// <param name="spec">The code, or JSON <c>null</c>.</param>
    /// <returns>The currency.</returns>
    public static Currency? CurrencyOf(JsonElement spec) => spec.ValueKind == JsonValueKind.Null
        ? null
        : spec.GetString() switch
        {
            "EUR" => new EURCurrency(),
            "USD" => new USDCurrency(),
            "GBP" => new GBPCurrency(),
            string s => throw new ArgumentException($"unknown currency {s}", nameof(spec)),
            _ => throw new ArgumentException("a currency's code is a string", nameof(spec)),
        };

    /// <summary>The period a spec <c>[length, units]</c> describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The period.</returns>
    public static Period PeriodOf(JsonElement spec) => new(spec[0].GetInt32(), (TimeUnit)spec[1].GetInt32());

    /// <summary>The default type a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The type.</returns>
    public static DefaultType TypeOf(JsonElement spec) =>
        spec.TryGetProperty("failureToPay", out JsonElement grace)
            ? new FailureToPay(PeriodOf(grace), spec.GetProperty("amount").GetDouble())
            : new DefaultType(
                (AtomicDefaultType)spec.GetProperty("atomic").GetInt32(),
                (RestructuringType)spec.GetProperty("restructuring").GetInt32());

    /// <summary>The default key a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The key.</returns>
    public static DefaultProbKey KeyOf(JsonElement spec)
    {
        if (!spec.EnumerateObject().Any())
        {
            return new DefaultProbKey();
        }

        Currency? currency = CurrencyOf(spec.GetProperty("currency"));
        var seniority = (Seniority)spec.GetProperty("seniority").GetInt32();
        if (spec.TryGetProperty("northAmericaCorp", out _))
        {
            Period? grace = spec.TryGetProperty("grace", out JsonElement g) ? PeriodOf(g) : null;
            double amount = spec.TryGetProperty("amount", out JsonElement a) ? a.GetDouble() : 1.0e6;
            RestructuringType r = spec.TryGetProperty("restructuring", out JsonElement x)
                ? (RestructuringType)x.GetInt32()
                : RestructuringType.CR;
            return new NorthAmericaCorpDefaultKey(currency, seniority, grace, amount, r);
        }

        return new DefaultProbKey([.. spec.GetProperty("eventTypes").EnumerateArray().Select(TypeOf)], currency, seniority);
    }

    /// <summary>The default event a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The event.</returns>
    public static DefaultEvent EventOf(JsonElement spec)
    {
        string kind = spec.GetProperty("kind").GetString()!;
        var d = new Date(spec.GetProperty("date").GetInt32());
        Currency? c = CurrencyOf(spec.GetProperty("currency"));
        var sen = (Seniority)spec.GetProperty("seniority").GetInt32();
        JsonElement s = spec.GetProperty("settlementDate");
        Date? settle = s.ValueKind == JsonValueKind.Null ? null : new Date(s.GetInt32());
        double? single = spec.TryGetProperty("recovery", out JsonElement r) ? r.GetDouble() : null;
        Dictionary<Seniority, double>? rates = spec.TryGetProperty("recoveries", out JsonElement m)
            ? m.EnumerateArray().ToDictionary(p => (Seniority)p[0].GetInt32(), p => p[1].GetDouble())
            : null;
        return kind switch
        {
            "DefaultEvent" when single is { } x => new DefaultEvent(d, TypeOf(spec.GetProperty("type")), c, sen, settle, x),
            "DefaultEvent" => new DefaultEvent(d, TypeOf(spec.GetProperty("type")), c, sen, settle, rates),
            "FailureToPayEvent" when single is { } x => new FailureToPayEvent(d, c, sen, spec.GetProperty("amount").GetDouble(), settle, x),
            "FailureToPayEvent" => new FailureToPayEvent(d, c, sen, spec.GetProperty("amount").GetDouble(), settle, rates),
            "BankruptcyEvent" when single is { } x => new BankruptcyEvent(d, c, sen, settle, x),
            "BankruptcyEvent" => new BankruptcyEvent(d, c, sen, settle, rates),
            _ => throw new ArgumentException($"unknown default event {kind}", nameof(spec)),
        };
    }

    /// <summary>The issuer a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which a settlement-days curve reads.</param>
    /// <returns>The issuer.</returns>
    public static Issuer IssuerOf(JsonElement spec, EvaluationContext context)
    {
        List<(DefaultProbKey, DefaultProbabilityTermStructure)> curves = spec.TryGetProperty("curves", out JsonElement cs)
            ? [.. cs.EnumerateArray().Select(kc => (KeyOf(kc.GetProperty("key")), CreditInputs.CurveOf(kc.GetProperty("curve"), context)))]
            : [];
        List<DefaultEvent> events = spec.TryGetProperty("events", out JsonElement es) ? [.. es.EnumerateArray().Select(EventOf)] : [];
        return new Issuer(curves, events);
    }

    /// <summary>The pool a spec describes, its names added in order.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The pool.</returns>
    public static Pool PoolOf(JsonElement spec, EvaluationContext context)
    {
        var pool = new Pool();
        foreach (JsonElement n in spec.EnumerateArray())
        {
            JsonElement k = n.GetProperty("key");
            pool = pool.Add(
                n.GetProperty("name").GetString()!,
                IssuerOf(n.GetProperty("issuer"), context),
                k.ValueKind == JsonValueKind.Null ? null : KeyOf(k));
        }

        return pool;
    }

    /// <summary>The basket a spec describes, with no loss model.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The basket.</returns>
    public static Basket BasketOf(JsonElement spec, EvaluationContext context)
    {
        Pool pool = PoolOf(spec.GetProperty("pool"), context);
        return new Basket(
            new Date(spec.GetProperty("refDate").GetInt32()),
            pool.Names,
            spec.GetProperty("notionals").EnumerateArray().Select(VectorCase.Real),
            pool,
            context,
            spec.GetProperty("attachment").GetDouble(),
            spec.GetProperty("detachment").GetDouble());
    }

    /// <summary>The loss model a spec describes, unbound.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The model.</returns>
    public static IDefaultLossModel LossModelOf(JsonElement spec)
    {
        string kind = spec.GetProperty("kind").GetString()!;
        return kind switch
        {
            "ConstantLossModel" => ConstantLoss(spec),
            "DefaultLossModel" => new BaseLossModel(),
            _ => throw new ArgumentException($"unknown loss model {kind}", nameof(spec)),
        };
    }

    /// <summary>A basket and a model's specs, the model bound to the basket.</summary>
    /// <param name="basket">The basket's spec.</param>
    /// <param name="model">The model's spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The bound basket.</returns>
    public static Basket BoundBasketOf(JsonElement basket, JsonElement model, EvaluationContext context) =>
        BasketOf(basket, context).SetLossModel(LossModelOf(model));

    /// <summary>A model's factor weights, <c>[[...]...]</c>.</summary>
    /// <param name="spec">The rows.</param>
    /// <returns>The weights.</returns>
    public static double[][] MatrixOf(JsonElement spec) =>
        [.. spec.EnumerateArray().Select(r => r.EnumerateArray().Select(VectorCase.Real).ToArray())];

    /// <summary>The T copula's orders a spec lists.</summary>
    /// <param name="spec">The orders.</param>
    /// <returns>The initialization parameters.</returns>
    public static TCopulaPolicyInitTraits TraitsOf(JsonElement spec) => new([.. spec.EnumerateArray().Select(x => x.GetInt32())]);

    /// <summary>A type's summary as the harness returns it: [atomic, restructuring].</summary>
    /// <param name="t">The type.</param>
    /// <returns>The summary.</returns>
    public static object[] TypeSummary(DefaultType t) => [(int)t.AtomicType, (int)t.RestructuringType];

    /// <summary>A key's summary as the harness returns it: [seniority, currency code, [type summary...]].</summary>
    /// <param name="k">The key.</param>
    /// <returns>The summary.</returns>
    public static object?[] KeySummary(DefaultProbKey k) => [(int)k.Seniority, k.Currency?.Code, k.EventTypes.Select(TypeSummary).ToArray()];

    // DefaultLossModel with every default kept, the harness's BaseLossModel: each statistic refuses.
    private sealed class BaseLossModel : DefaultLossModel
    {
        protected override void ResetModel()
        {
        }
    }

    private static IDefaultLossModel ConstantLoss(JsonElement spec)
    {
        string copula = spec.GetProperty("copula").GetString()!;
        double[] recoveries = [.. spec.GetProperty("recoveries").EnumerateArray().Select(VectorCase.Real)];
        var type = (LatentModelIntegrationType)spec.GetProperty("integration").GetInt32();
        bool quote = spec.TryGetProperty("correlation", out JsonElement c);
        int n = quote ? spec.GetProperty("nVariables").GetInt32() : 0;
        if (copula == "Gaussian")
        {
            return quote
                ? new ConstantLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(c.GetDouble(), recoveries, type, n)
                : new ConstantLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(MatrixOf(spec.GetProperty("factorWeights")), recoveries, type);
        }

        TCopulaPolicyInitTraits ini = TraitsOf(spec.GetProperty("tOrders"));
        return quote
            ? new ConstantLossModel<TCopulaPolicy, TCopulaPolicyInitTraits>(c.GetDouble(), recoveries, type, n, ini)
            : new ConstantLossModel<TCopulaPolicy, TCopulaPolicyInitTraits>(MatrixOf(spec.GetProperty("factorWeights")), recoveries, type, ini);
    }
}
