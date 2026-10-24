// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The digital coupon specs and outputs of the harness's digital vectors (tools/cxx/src/cashflows/digitalspec.hpp's):
/// a replication null or <c>{"type", "gap"}</c>; a coupon's terms; a leg's digital settings; and the inspectors and
/// rates each coupon is checked on.
/// </summary>
internal static class DigitalInputs
{
    /// <summary>The digital inspectors' output names.</summary>
    public static readonly string[] Inspectors =
    [
        "callStrike", "putStrike", "callDigitalPayoff", "putDigitalPayoff", "hasCall", "hasPut", "hasCollar",
        "isLongCall", "isLongPut", "date", "accrualStartDate", "accrualEndDate", "fixingDate", "nominal", "gearing",
        "spread",
    ];

    /// <summary>A replication spec's replication.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The replication, or <see langword="null"/> for QuantLib's default.</returns>
    public static DigitalReplication? ReplicationOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null
            ? null
            : new DigitalReplication(
                (ReplicationType)spec.GetProperty("type").GetInt32(), spec.GetProperty("gap").GetDouble());

    /// <summary>The digital coupon a terms spec builds on an underlying.</summary>
    /// <param name="underlying">The underlying.</param>
    /// <param name="spec">The terms.</param>
    /// <returns>The coupon.</returns>
    public static DigitalCoupon DigitalOf(FloatingRateCoupon underlying, JsonElement spec) => new(
        underlying,
        Rate(spec.GetProperty("callStrike")),
        (PositionType)spec.GetProperty("callPosition").GetInt32(),
        spec.GetProperty("isCallATMIncluded").GetBoolean(),
        Rate(spec.GetProperty("callDigitalPayoff")),
        Rate(spec.GetProperty("putStrike")),
        (PositionType)spec.GetProperty("putPosition").GetInt32(),
        spec.GetProperty("isPutATMIncluded").GetBoolean(),
        Rate(spec.GetProperty("putDigitalPayoff")),
        ReplicationOf(spec.GetProperty("replication")),
        spec.GetProperty("nakedOption").GetBoolean());

    /// <summary>A coupon's digital inspector, by name.</summary>
    /// <param name="c">The coupon.</param>
    /// <param name="name">The inspector's name.</param>
    /// <returns>Its value.</returns>
    public static object? Inspector(DigitalCoupon c, string name) => name switch
    {
        "callStrike" => c.CallStrike,
        "putStrike" => c.PutStrike,
        "callDigitalPayoff" => c.CallDigitalPayoff,
        "putDigitalPayoff" => c.PutDigitalPayoff,
        "hasCall" => c.HasCall,
        "hasPut" => c.HasPut,
        "hasCollar" => c.HasCollar,
        "isLongCall" => c.IsLongCall,
        "isLongPut" => c.IsLongPut,
        "date" => c.Date,
        "accrualStartDate" => c.AccrualStartDate,
        "accrualEndDate" => c.AccrualEndDate,
        "fixingDate" => c.FixingDate(),
        "nominal" => c.ContractNominal,
        "gearing" => c.Gearing,
        "spread" => c.Spread,
        _ => throw new ArgumentException($"unknown inspector {name}", nameof(name)),
    };

    /// <summary>A coupon's rate, call option rate or put option rate, by name.</summary>
    /// <param name="c">The coupon.</param>
    /// <param name="name">The rate's name.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    public static double RateOf(DigitalCoupon c, string name, EvaluationContext context) => name switch
    {
        "rate" => c.Rate(context),
        "callOptionRate" => c.CallOptionRate(context),
        "putOptionRate" => c.PutOptionRate(context),
        _ => throw new ArgumentException($"unknown rate {name}", nameof(name)),
    };

    /// <summary>A leg's outputs, each a list over the coupons as the generators flatten them: the inspectors and, with
    /// rates, the three rates of each digital coupon, null for a fixed coupon (and a digital coupon's rate null without
    /// rates); "digital"; "date"; and a fixed coupon's rate under "rate".</summary>
    /// <param name="check">The assertions.</param>
    /// <param name="c">The case.</param>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="withRates">Whether the rates are checked.</param>
    public static void CheckLeg(
        VectorAssert check, VectorCase c, IReadOnlyList<CashFlow> leg, EvaluationContext context, bool withRates)
    {
        check.Output(c, "digital", leg.Select(cf => cf is DigitalCoupon).ToArray());
        check.Output(c, "date", leg.Select(cf => cf.Date).ToArray());
        foreach (string name in Inspectors.Where(n => n != "date"))
        {
            check.Output(c, name, leg.Select(cf => cf is DigitalCoupon d ? Inspector(d, name) : null).ToArray());
        }

        foreach (string name in withRates ? new[] { "rate", "callOptionRate", "putOptionRate" } : ["rate"])
        {
            check.Output(c, name, leg.Select(cf => (Func<object?>)(() => cf switch
            {
                DigitalCoupon d when withRates => RateOf(d, name, context),
                FixedRateCoupon f when name == "rate" => f.Rate(context),
                _ => null,
            })).ToArray());
        }
    }

    private static double? Rate(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetDouble();
}
