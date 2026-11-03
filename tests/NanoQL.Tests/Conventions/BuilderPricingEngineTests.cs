// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The swap builders take QuantLib's pricing engine as a delegate of the swap and the context, which computes what
/// QuantLib's builder computes at build time, the par rate when no fixed rate is given, and is never stored: the swap
/// returned holds no engine, where QuantLib's builder attaches one. The credit default swap's builder computes nothing
/// at build time, so its engine changes nothing.
/// </summary>
public class BuilderPricingEngineTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly FlatForward Forecast = new(Today, 0.031, new Actual365Fixed());
    private static readonly FlatForward Discount = new(Today, 0.027, new Actual365Fixed());

    [Fact]
    public void TheEngineComputesTheParRateAndIsNotStored()
    {
        var priced = new List<FixedVsFloatingSwap>();
        FixedVsFloatingSwapResults Engine(FixedVsFloatingSwap s, EvaluationContext c)
        {
            priced.Add(s);
            return DiscountingSwapEngine.Calculate(s, c, Discount, includeSettlementDateFlows: false);
        }

        // With no fixed rate, the engine prices the swap at 0% once, as QuantLib's builder does with a nominal of 100,
        // and the swap is built at the fair rate it gives.
        var index = new Euribor6M(Forecast);
        VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), index)
            .WithNominal(1e6)
            .WithPricingEngine(Engine)
            .ToVanillaSwap(Context);
        FixedVsFloatingSwap temp = Assert.Single(priced);
        Assert.Equal(0.0, temp.FixedRate);
        Assert.Equal(100.0, temp.Nominal());
        double fairRate = DiscountingSwapEngine.Calculate(temp, Context, Discount, false).FairRate!.Value;
        Assert.Equal(fairRate, swap.FixedRate);
        Assert.Equal(1e6, swap.Nominal());
        Assert.Equal(0.0, DiscountingSwapEngine.Calculate(swap, Context, Discount, false).NPV!.Value, 1e-6);

        // The swap holds no engine: none of its fields, its bases' included, holds a delegate.
        for (Type? t = typeof(VanillaSwap); t is not null; t = t.BaseType)
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.False(typeof(Delegate).IsAssignableFrom(f.FieldType), $"{t.Name}.{f.Name} holds a delegate");
            }
        }

        // With a fixed rate, the engine computes nothing.
        VanillaSwap fixedSwap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), index)
            .WithFixedRate(0.03)
            .WithPricingEngine(Engine)
            .ToVanillaSwap(Context);
        Assert.Single(priced);
        Assert.Equal(0.03, fixedSwap.FixedRate);

        // The discounting shortcut is the same engine on a curve, without the settlement date's flows; MakeOIS takes
        // both forms too.
        var estr = new Estr(Forecast);
        OvernightIndexedSwap ois = new MakeOIS(new Period(2, TimeUnit.Years), estr)
            .WithDiscountingTermStructure(Discount)
            .ToOvernightIndexedSwap(Context);
        Assert.Equal(0.0, DiscountingSwapEngine.Calculate(ois, Context, Discount, false).NPV!.Value, 1e-14);
        OvernightIndexedSwap viaEngine = new MakeOIS(new Period(2, TimeUnit.Years), estr)
            .WithPricingEngine(Engine)
            .ToOvernightIndexedSwap(Context);
        Assert.Equal(ois.FixedRate, viaEngine.FixedRate);
        Assert.Equal(2, priced.Count);

        // With neither, the par rate is priced on the index's own forecasting curve.
        OvernightIndexedSwap onOwnCurve =
            new MakeOIS(new Period(2, TimeUnit.Years), estr).ToOvernightIndexedSwap(Context);
        Assert.Equal(0.0, DiscountingSwapEngine.Calculate(onOwnCurve, Context, Forecast, false).NPV!.Value, 1e-14);
        Assert.NotEqual(ois.FixedRate, onOwnCurve.FixedRate);
    }

    [Fact]
    public void TheCreditDefaultSwapBuildersEngineChangesNothing()
    {
        // QuantLib's builder only attaches the engine to the swap it builds (makecds.cpp:90)
        static InstrumentResults Engine(CreditDefaultSwap s, EvaluationContext c) =>
            throw new InvalidOperationException("the engine was called");

        var builder = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01);
        Assert.Same(builder, builder.WithPricingEngine(Engine));
        Assert.Same(builder, builder.WithPricingEngine(null));
        Assert.Equal(
            builder.ToCreditDefaultSwap(Context).ProtectionEndDate,
            builder.WithPricingEngine(Engine).ToCreditDefaultSwap(Context).ProtectionEndDate);
    }

    [Fact]
    public void ANullEngineIsQuantLibsNullEngine()
    {
        // QuantLib's withPricingEngine(nullptr) leaves no engine, so the par rate is priced by the default engine on
        // the index's forwarding curve, also after another engine was set (makevanillaswap.cpp:178-189,
        // makeois.cpp:156-167). The wheel's par rates on a 3% curve, after a 5% discounting curve and then a null
        // engine: 0.030470963573 and 0.030037616985, those of the builds with neither.
        var curve = new FlatForward(Today, 0.03, new Actual365Fixed());
        var other = new FlatForward(Today, 0.05, new Actual365Fixed());
        var fiveYears = new Period(5, TimeUnit.Years);

        double vanilla = new MakeVanillaSwap(fiveYears, new Euribor6M(curve)).WithDiscountingTermStructure(other)
            .WithPricingEngine(null).ToVanillaSwap(Context).FixedRate;
        Assert.Equal(0.030470963573, vanilla, 1e-12);
        Assert.Equal(new MakeVanillaSwap(fiveYears, new Euribor6M(curve)).ToVanillaSwap(Context).FixedRate, vanilla);

        double ois = new MakeOIS(fiveYears, new Estr(curve)).WithDiscountingTermStructure(other)
            .WithPricingEngine(null).ToOvernightIndexedSwap(Context).FixedRate;
        Assert.Equal(0.030037616985, ois, 1e-12);
        Assert.Equal(new MakeOIS(fiveYears, new Estr(curve)).ToOvernightIndexedSwap(Context).FixedRate, ois);
    }
}
