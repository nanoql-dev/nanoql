// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.ArrayVectorTests;
using static NanoQL.Tests.Vectors.BrownianBridgeVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the callabilities, callable bonds and engines that the callable-bond vector files describe in their inputs,
/// as <c>tools/vectors/gen/experimental/callablebonds/callablebond.py</c>'s functions build them, and checks the outputs
/// those files share.
/// </summary>
/// <remarks>
/// A callability is <c>{"price": {"amount", "type"}, "type", "date"}</c>; a bond is
/// <c>{"type": "CallableFixedRateBond", ...}</c> or <c>{"type": "CallableZeroCouponBond", ...}</c> with QuantLib's
/// arguments by name, its schedule <see cref="BondInputs.ScheduleOf"/>'s; a curve, a model and a grid are
/// <see cref="ModelInputs"/>' and <see cref="BrownianBridgeVectorTests.Grid"/>'s specs; an engine is
/// <c>{"type": "TreeSteps", "model", "timeSteps", "termStructure"}</c>, <c>{"type": "TreeGrid" | "TreeLattice",
/// "model", "timeGrid", "termStructure"}</c> (QuantLib's time-grid engine, which the lattice overload reproduces from
/// the model's tree on the grid) or <c>{"type": "Black", "volatility", "discountCurve"}</c>.
/// </remarks>
internal static class CallableBondInputs
{
    /// <summary>The callability a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The callability.</returns>
    public static Callability CallabilityOf(JsonElement spec) => new(
        BondInputs.PriceOf(spec.GetProperty("price")),
        (CallabilityType)spec.GetProperty("type").GetInt32(),
        new Date(spec.GetProperty("date").GetInt32()));

    /// <summary>The callable bond a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context, which a rule-based schedule reads.</param>
    /// <returns>The bond.</returns>
    public static CallableBond BondOf(JsonElement spec, EvaluationContext context)
    {
        string type = spec.GetProperty("type").GetString()!;
        Callability[] calls = [.. spec.GetProperty("putCallSchedule").EnumerateArray().Select(CallabilityOf)];
        int settlementDays = spec.GetProperty("settlementDays").GetInt32();
        double face = Number(spec.GetProperty("faceAmount"));
        switch (type)
        {
            case "CallableFixedRateBond":
            {
                (Period period, Calendar? calendar, BusinessDayConvention convention, bool eom) =
                    ExCouponOf(spec.GetProperty("exCouponPeriod"));
                return new CallableFixedRateBond(
                    settlementDays,
                    face,
                    BondInputs.ScheduleOf(spec.GetProperty("schedule"), context),
                    Numbers(spec.GetProperty("coupons")),
                    CashFlowInputs.DayCounterOf(spec.GetProperty("accrualDayCounter")),
                    (BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32(),
                    Number(spec.GetProperty("redemption")),
                    CashFlowInputs.NullableDate(spec.GetProperty("issueDate")),
                    calls,
                    period,
                    calendar,
                    convention,
                    eom);
            }

            case "CallableZeroCouponBond":
                return new CallableZeroCouponBond(
                    settlementDays,
                    face,
                    BondInputs.CalendarOf(spec.GetProperty("calendar")),
                    new Date(spec.GetProperty("maturityDate").GetInt32()),
                    BondInputs.DayCounterOf(spec.GetProperty("dayCounter")),
                    (BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32(),
                    Number(spec.GetProperty("redemption")),
                    CashFlowInputs.NullableDate(spec.GetProperty("issueDate")),
                    calls);
            default:
                throw new InvalidOperationException($"unknown callable bond type '{type}'");
        }
    }

    /// <summary>A curve spec, or <see langword="null"/>, QuantLib's empty handle, for JSON null.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The curve, or <see langword="null"/>.</returns>
    public static YieldTermStructure? CurveOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : ModelInputs.CurveOf(spec);

    /// <summary>
    /// The engine a spec describes, as the OAS family takes it: a function of the bond, the context and the spread,
    /// which builds the engine's model, curve, volatility and lattice at its first call, so that a refusal among them
    /// is checked as the engine's. The Black engines read no spread; a <c>"Zero"</c> type is the zero-coupon engine's.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The engine.</returns>
    public static Func<CallableBond, EvaluationContext, double, CallableBondResults> EngineOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        if (type.StartsWith("Black", StringComparison.Ordinal))
        {
            var discountCurve = new Lazy<YieldTermStructure>(() => ModelInputs.CurveOf(spec.GetProperty("discountCurve")));
            JsonElement vol = spec.GetProperty("volatility");
            return type switch
            {
                "Black" => (bond, context, _) =>
                    BlackCallableFixedRateBondEngine.Calculate(bond, context, Number(vol), discountCurve.Value),
                "BlackZero" => (bond, context, _) =>
                    BlackCallableZeroCouponBondEngine.Calculate(bond, context, Number(vol), discountCurve.Value),
                "BlackStructure" => (bond, context, _) => BlackCallableFixedRateBondEngine.Calculate(
                    bond, context, VolatilityOf(vol, context), discountCurve.Value),
                "BlackZeroStructure" => (bond, context, _) => BlackCallableZeroCouponBondEngine.Calculate(
                    bond, context, VolatilityOf(vol, context), discountCurve.Value),
                _ => throw new InvalidOperationException($"unknown engine type '{type}'"),
            };
        }

        var model = new Lazy<ShortRateModel>(() => ModelInputs.ModelOf(spec.GetProperty("model")));
        var ts = new Lazy<YieldTermStructure?>(() => CurveOf(spec.GetProperty("termStructure")));
        switch (type)
        {
            case "TreeSteps":
            {
                int steps = spec.GetProperty("timeSteps").GetInt32();
                return (bond, context, s) =>
                    TreeCallableFixedRateBondEngine.Calculate(bond, context, model.Value, steps, ts.Value, s);
            }

            case "TreeZeroSteps":
            {
                int steps = spec.GetProperty("timeSteps").GetInt32();
                return (bond, context, s) =>
                    TreeCallableZeroCouponBondEngine.Calculate(bond, context, model.Value, steps, ts.Value, s);
            }

            case "TreeGrid":
            {
                var grid = new Lazy<TimeGrid>(() => Grid(spec.GetProperty("timeGrid")));
                return (bond, context, s) =>
                    TreeCallableFixedRateBondEngine.Calculate(bond, context, model.Value, grid.Value, ts.Value, s);
            }

            case "TreeZeroGrid":
            {
                var grid = new Lazy<TimeGrid>(() => Grid(spec.GetProperty("timeGrid")));
                return (bond, context, s) =>
                    TreeCallableZeroCouponBondEngine.Calculate(bond, context, model.Value, grid.Value, ts.Value, s);
            }

            case "TreeLattice":
            {
                // QuantLib's grid engine builds its tree once; the lattice overload takes it built.
                var lattice = new Lazy<Lattice>(() => model.Value.Tree(Grid(spec.GetProperty("timeGrid"))));
                return (bond, context, s) =>
                    TreeCallableFixedRateBondEngine.Calculate(bond, context, model.Value, lattice.Value, ts.Value, s);
            }

            case "TreeZeroLattice":
            {
                var lattice = new Lazy<Lattice>(() => model.Value.Tree(Grid(spec.GetProperty("timeGrid"))));
                return (bond, context, s) =>
                    TreeCallableZeroCouponBondEngine.Calculate(bond, context, model.Value, lattice.Value, ts.Value, s);
            }

            default:
                throw new InvalidOperationException($"unknown engine type '{type}'");
        }
    }

    /// <summary>
    /// The callable-bond volatility a spec describes: a constant one, <c>{"referenceDate", "volatility",
    /// "dayCounter"}</c>, or <c>{"settlementDays", "calendar", "volatility", "dayCounter"}</c> built by
    /// <see cref="CallableBondConstantVolatility.Create"/> in the context; or the harness's own subclass,
    /// <c>{"type": "HarnessCallableBondVolatility", "referenceDate", "volatility", "dayCounter", "maxBondTenor",
    /// "minStrike", "maxStrike"}</c>, which overrides only what <see cref="CallableBondVolatilityStructure"/> leaves
    /// abstract, as <c>tools/cxx/src/experimental/callablebonds/callablebondspec.hpp</c> defines it.
    /// </summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The context a moving structure is built in.</param>
    /// <returns>The structure.</returns>
    public static CallableBondVolatilityStructure VolatilityOf(JsonElement spec, EvaluationContext context)
    {
        double vol = Number(spec.GetProperty("volatility"));
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        if (spec.TryGetProperty("type", out JsonElement type))
        {
            if (type.GetString() != "HarnessCallableBondVolatility")
            {
                throw new InvalidOperationException($"unknown callable-bond volatility {type}");
            }

            return new HarnessCallableBondVolatility(
                new Date(spec.GetProperty("referenceDate").GetInt32()),
                vol,
                dc,
                SwapInputs.PeriodOf(spec.GetProperty("maxBondTenor")),
                Number(spec.GetProperty("minStrike")),
                Number(spec.GetProperty("maxStrike")));
        }

        if (spec.TryGetProperty("referenceDate", out JsonElement reference))
        {
            return new CallableBondConstantVolatility(new Date(reference.GetInt32()), vol, dc);
        }

        return CallableBondConstantVolatility.Create(
            spec.GetProperty("settlementDays").GetInt32(),
            BondInputs.CalendarOf(spec.GetProperty("calendar"))!,
            vol,
            dc,
            context);
    }

    /// <summary>
    /// Checks the bond's inspectors: its put/call schedule, its cash flows, its settlement and maturity dates, its
    /// accrued amount and whether it has expired.
    /// </summary>
    /// <param name="check">The file's checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The case's evaluation context.</param>
    public static void CheckBond(VectorAssert check, VectorCase c, CallableBond bond, EvaluationContext context)
    {
        check.Output(c, "callabilityDates", bond.Callability.Select(x => x.Date));
        check.Output(c, "callabilityTypes", bond.Callability.Select(x => x.Type));
        check.Output(c, "callabilityPrices", bond.Callability.Select(x => x.Price.Amount));
        check.Output(c, "callabilityPriceTypes", bond.Callability.Select(x => x.Price.Type));
        check.Output(c, "cashflowDates", bond.Cashflows.Select(cf => cf.Date));
        check.Output(c, "cashflowAmounts", BondInputs.Each(bond.Cashflows, cf => cf.Amount(context)));
        check.Output(c, "settlementDate", bond.SettlementDate(context));
        check.Output(c, "maturityDate", bond.MaturityDate());
        check.Output(c, "accruedAmount", () => bond.AccruedAmount(context));
        check.Output(c, "isExpired", bond.IsExpired(context));
    }

    /// <summary>
    /// Checks the bond priced by the case's engine: the results, and the clean and dirty prices read from them.
    /// </summary>
    /// <param name="check">The file's checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <param name="engine">The engine.</param>
    public static void CheckEngine(
        VectorAssert check,
        VectorCase c,
        CallableBond bond,
        EvaluationContext context,
        Func<CallableBond, EvaluationContext, double, CallableBondResults> engine)
    {
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
        check.Output(c, "errorEstimate", () => Results().ErrorEstimate);
        check.Output(c, "valuationDate", () => Results().ValuationDate);
        check.Output(c, "cleanPrice", () => bond.CleanPrice(Results(), context));
        check.Output(c, "dirtyPrice", () => bond.DirtyPrice(Results(), context));
    }

    /// <summary>
    /// Checks the spread family on the case's engine (<c>"oas"</c>): the spread for a clean price and for the engine's
    /// own clean price, the clean price for a spread, and the effective duration and convexity at it.
    /// </summary>
    /// <param name="check">The file's checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <param name="engine">The engine.</param>
    public static void CheckOAS(
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
        double s = Number(spec.GetProperty("spread"));
        double bump = Number(spec.GetProperty("bump"));
        check.Output(c, "OAS", () => bond.OAS(
            Number(spec.GetProperty("cleanPrice")),
            curve,
            dc,
            comp,
            freq,
            engine,
            context,
            settlement,
            Number(spec.GetProperty("accuracy")),
            spec.GetProperty("maxIterations").GetInt32(),
            Number(spec.GetProperty("guess"))));
        check.Output(c, "OASAtCleanPrice", () => bond.OAS(
            bond.CleanPrice(engine(bond, context, 0.0), context), curve, dc, comp, freq, engine, context));
        check.Output(c, "cleanPriceOAS", () => bond.CleanPriceOAS(s, curve, dc, comp, freq, engine, context, settlement));
        check.Output(
            c, "effectiveDuration", () => bond.EffectiveDuration(s, curve, dc, comp, freq, engine, context, bump));
        check.Output(
            c, "effectiveConvexity", () => bond.EffectiveConvexity(s, curve, dc, comp, freq, engine, context, bump));
    }

    /// <summary>Checks the implied volatilities for the case's target prices (<c>"implied"</c>).</summary>
    /// <param name="check">The file's checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The case's evaluation context.</param>
    public static void CheckImplied(VectorAssert check, VectorCase c, CallableBond bond, EvaluationContext context)
    {
        JsonElement spec = c.Input("implied");
        YieldTermStructure curve = ModelInputs.CurveOf(spec.GetProperty("discountCurve"));
        double accuracy = Number(spec.GetProperty("accuracy"));
        int maxEvaluations = spec.GetProperty("maxEvaluations").GetInt32();
        double minVol = Number(spec.GetProperty("minVol")), maxVol = Number(spec.GetProperty("maxVol"));
        BondPrice[] targets = [.. spec.GetProperty("targets").EnumerateArray().Select(BondInputs.PriceOf)];
        check.Output(c, "impliedVolatility", BondInputs.Each(targets, t => bond.ImpliedVolatility(
            t, curve, context, accuracy, maxEvaluations, minVol, maxVol)));
    }

    private static (Period Period, Calendar? Calendar, BusinessDayConvention Convention, bool EndOfMonth) ExCouponOf(
        JsonElement spec)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return (default, null, BusinessDayConvention.Unadjusted, false);
        }

        JsonElement period = spec.GetProperty("period");
        return (
            new Period(period[0].GetInt32(), (TimeUnit)period[1].GetInt32()),
            BondInputs.CalendarOf(spec.GetProperty("calendar")),
            (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
            spec.GetProperty("endOfMonth").GetBoolean());
    }

    /// <summary>
    /// The C++ harness's own callable-bond volatility: it overrides only what
    /// <see cref="CallableBondVolatilityStructure"/> leaves abstract, a constant volatility and a flat smile section by
    /// time, so that the base's defaults (the maximum bond length from the maximum tenor, the volatility by date through
    /// the date conversion) are what it answers with; QuantLib's empty calendar and the base's convention.
    /// </summary>
    private sealed class HarnessCallableBondVolatility(
        Date referenceDate, double vol, DayCounter dc, Period maxBondTenor, double minStrike, double maxStrike)
        : CallableBondVolatilityStructure(referenceDate, null, dc)
    {
        public override Date MaxDate => Date.MaxDate;

        public override Period MaxBondTenor => maxBondTenor;

        public override double MinStrike() => minStrike;

        public override double MaxStrike() => maxStrike;

        protected override NanoQL.TermStructures.SmileSection SmileSectionImpl(double optionTime, double bondLength) =>
            new FlatSmileSection(optionTime, vol, DayCounter);

        protected override double VolatilityImpl(double optionTime, double bondLength, double strike) => vol;
    }
}
