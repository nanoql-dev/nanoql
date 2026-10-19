// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="MakeSwaption.WithPricingEngine"/> is kept from QuantLib and changes nothing: QuantLib's builder only
/// attaches the engine to the swaption it builds (<c>makeswaption.cpp:147</c>), computing nothing with it (its
/// at-the-money strike prices the index's swap on an engine of its own), and a nanoql swaption holds no engine, its
/// engines being functions called on it (playbook §2.1, §2.3).
/// </summary>
public class MakeSwaptionPricingEngineTests
{
    [Fact]
    public void ThePricingEngineChangesNothing()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var index = new EuriborSwapIsdaFixA(new Period(5, TimeUnit.Years), new FlatForward(today, 0.03, new Actual365Fixed()));
        var builder = new MakeSwaption(index, new Period(1, TimeUnit.Years));
        bool called = false;

        MakeSwaption withEngine = builder.WithPricingEngine((s, c) =>
        {
            called = true;
            throw new InvalidOperationException("the engine is never called");
        });

        Assert.Same(builder, withEngine);
        Swaption plain = builder.ToSwaption(context), engined = withEngine.ToSwaption(context);
        Assert.False(called);
        Assert.Equal(plain.Underlying.FixedRate, engined.Underlying.FixedRate);
        Assert.Equal(plain.Exercise.Dates, engined.Exercise.Dates);
        Assert.Same(builder, builder.WithPricingEngine(null));
    }
}
