// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The convertible bonds' specs, <c>tools/vectors/gen/instruments/bonds/convertiblebonds.py</c>'s, and the binomial
/// convertible engines by QuantLib-Python's tree names.
/// </summary>
/// <remarks>
/// A bond spec is <c>{"type", "exercise" ({"type", "dates"}), "conversionRatio", "callability", "issueDate",
/// "settlementDays", "dayCounter", "schedule", "redemption"}</c> and, for the fixed-coupon bond, <c>"coupons"</c> and
/// <c>"exCouponPeriod"</c>, for the floating-rate bond <c>"index"</c>, <c>"fixingDays"</c>, <c>"spreads"</c> and
/// <c>"exCouponPeriod"</c>; a callability is <see cref="CallableBondInputs.CallabilityOf"/>'s spec with a
/// <c>"trigger"</c>, null but for a soft call.
/// </remarks>
internal static class ConvertibleInputs
{
    /// <summary>The convertible bond a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which a rule-based schedule and an Ibor leg read.</param>
    /// <returns>The bond.</returns>
    public static ConvertibleBond BondOf(JsonElement spec, EvaluationContext context)
    {
        string type = spec.GetProperty("type").GetString()!;
        Exercise exercise = ExerciseOf(spec.GetProperty("exercise"));
        double ratio = spec.GetProperty("conversionRatio").GetDouble();
        Callability[] calls = [.. spec.GetProperty("callability").EnumerateArray().Select(CallabilityOf)];
        Date? issue = CashFlowInputs.NullableDate(spec.GetProperty("issueDate"));
        int days = spec.GetProperty("settlementDays").GetInt32();
        DayCounter dayCounter = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        Schedule schedule = BondInputs.ScheduleOf(spec.GetProperty("schedule"), context);
        double redemption = spec.GetProperty("redemption").GetDouble();
        switch (type)
        {
            case "ConvertibleZeroCouponBond":
                return new ConvertibleZeroCouponBond(exercise, ratio, calls, issue, days, dayCounter, schedule, redemption);
            case "ConvertibleFixedCouponBond":
            {
                (Period period, Calendar? calendar, BusinessDayConvention convention, bool eom) =
                    BondInputs.ExCouponOf(spec.GetProperty("exCouponPeriod"));
                return new ConvertibleFixedCouponBond(
                    exercise, ratio, calls, issue, days, Doubles(spec.GetProperty("coupons")), dayCounter, schedule,
                    redemption, period, calendar, convention, eom);
            }

            case "ConvertibleFloatingRateBond":
            {
                (Period period, Calendar? calendar, BusinessDayConvention convention, bool eom) =
                    BondInputs.ExCouponOf(spec.GetProperty("exCouponPeriod"));
                return new ConvertibleFloatingRateBond(
                    exercise, ratio, calls, issue, days, IndexInputs.IborIndexOf(spec.GetProperty("index")),
                    spec.GetProperty("fixingDays").GetInt32(), Doubles(spec.GetProperty("spreads")), dayCounter, schedule,
                    context, redemption, period, calendar, convention, eom);
            }

            default:
                throw new InvalidOperationException($"unknown convertible bond '{type}'");
        }
    }

    /// <summary>The exercise a spec describes: European on its one date, American between its two, Bermudan on all.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The exercise.</returns>
    public static Exercise ExerciseOf(JsonElement spec)
    {
        Date[] dates = [.. spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
        return spec.GetProperty("type").GetString() switch
        {
            "European" => new EuropeanExercise(dates[0]),
            "American" => new AmericanExercise(dates[0], dates[1]),
            "Bermudan" => new BermudanExercise(dates),
            string s => throw new InvalidOperationException($"unknown exercise '{s}'"),
            null => throw new InvalidOperationException("no exercise type"),
        };
    }

    /// <summary>The callability a spec describes: a soft call where it has a trigger.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The callability.</returns>
    public static Callability CallabilityOf(JsonElement spec) =>
        spec.GetProperty("trigger").ValueKind == JsonValueKind.Null
            ? CallableBondInputs.CallabilityOf(spec)
            : new SoftCallability(
                BondInputs.PriceOf(spec.GetProperty("price")),
                new Date(spec.GetProperty("date").GetInt32()),
                spec.GetProperty("trigger").GetDouble());

    /// <summary>The binomial convertible engine on a tree, by QuantLib-Python's name for it.</summary>
    /// <param name="tree">The tree's name.</param>
    /// <returns>The engine.</returns>
    public static Func<ConvertibleBond, EvaluationContext, GeneralizedBlackScholesProcess, int, double, ImmutableArray<Dividend>, BondResults> Engine(
        string tree) =>
        tree switch
        {
            "jr" => BinomialJRConvertibleEngine.Calculate,
            "crr" => BinomialCRRConvertibleEngine.Calculate,
            "eqp" => BinomialEQPConvertibleEngine.Calculate,
            "trigeorgis" => BinomialTrigeorgisConvertibleEngine.Calculate,
            "tian" => BinomialTianConvertibleEngine.Calculate,
            "lr" => BinomialLRConvertibleEngine.Calculate,
            "joshi4" => BinomialJ4ConvertibleEngine.Calculate,
            _ => throw new ArgumentException($"unknown tree {tree}", nameof(tree)),
        };

    private static double[] Doubles(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetDouble())];
}
