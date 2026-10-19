// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="MakeCapFloor.WithPricingEngine"/> is kept from QuantLib and changes nothing: QuantLib's builder attaches
/// the engine to the cap it builds (<c>makecapfloor.cpp:102</c>) and, with no strike, reads the discount curve out of a
/// Black or Bachelier engine (<c>makecapfloor.cpp:71-98</c>). A nanoql cap holds no engine, its engines being functions
/// called on it (playbook §2.1, §2.3), and a function hides its curve, so the at-the-money curve is given to
/// <see cref="MakeCapFloor.WithDiscountingTermStructure"/>, which computes the strike and stores nothing on the cap.
/// </summary>
public class MakeCapFloorPricingEngineTests
{
    [Fact]
    public void ThePricingEngineChangesNothing()
    {
        var today = new Date(16, Month.June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.04, new Actual365Fixed());
        var builder = new MakeCapFloor(CapFloorType.Cap, new Period(3, TimeUnit.Years), new Euribor6M(curve)).WithStrike(0.035);
        bool called = false;

        MakeCapFloor withEngine = builder.WithPricingEngine((c, ctx) =>
        {
            called = true;
            throw new InvalidOperationException("the engine is never called");
        });

        Assert.Same(builder, withEngine);
        CapFloor plain = builder.ToCapFloor(context), engined = withEngine.ToCapFloor(context);
        Assert.False(called);
        Assert.Equal(plain.CapRates, engined.CapRates);
        Assert.Equal(plain.FloatingLeg.Select(cf => cf.Date), engined.FloatingLeg.Select(cf => cf.Date));
        Assert.Same(builder, builder.WithPricingEngine(null));

        // The at-the-money strike needs the curve, which an engine can't give: the engine alone is refused, as QuantLib
        // refuses a builder whose engine isn't a Black or Bachelier one.
        MakeCapFloor atTheMoney = builder.WithStrike(null).WithPricingEngine((c, ctx) => BlackCapFloorEngine.Calculate(c, ctx, curve, 0.2));
        Assert.Throws<NanoQLException>(() => atTheMoney.ToCapFloor(context));
        CapFloor struck = atTheMoney.WithDiscountingTermStructure(curve).ToCapFloor(context);
        Assert.Equal(struck.AtmRate(curve, context), struck.CapRates[0]);
    }
}
