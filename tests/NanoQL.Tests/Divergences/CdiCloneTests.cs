// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="Cdi"/>'s clone. QuantLib's <c>Cdi</c> overrides <c>forecastFixing</c> (<c>cdi.cpp:20-33</c>) but not
/// <c>clone</c>, so <c>clone(h)</c>, "a copy of itself linked to a different forwarding curve"
/// (<c>iborindex.hpp:60-62</c>), is a plain <c>OvernightIndex</c> of the same name, forecasting by the generic formula:
/// on a flat 10.75% Business/252 curve its fixing for 28 March 2024 is 0.10752293232819721 against <c>Cdi</c>'s
/// 0.11349086074650594, and an OIS helper bootstrapping a CDI curve clones its index. nanoql's
/// <see cref="Cdi.Clone"/> returns a <see cref="Cdi"/>. QuantLib's check: <c>Cdi</c>'s own forecast on the new curve
/// [executed on the wheel].
/// </summary>
public class CdiCloneTests
{
    private static readonly Date Today = new(15, March, 2024);
    private static readonly Date FixingDate = new(28, March, 2024);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void TheClonesForecastIsANewCdisOnItsCurve()
    {
        var curve = new FlatForward(Today, 0.1075, new Business252());
        var other = new FlatForward(Today, 0.12, new Business252());
        var cdi = new Cdi(curve);

        // QuantLib: 0.10752293232819721, the generic formula
        Assert.Equal(0.11349086074650594, cdi.Clone(curve).Fixing(FixingDate, Context));

        // QuantLib: 0.12002857596433358; a new Cdi on the curve, 0.1274968515794599 on the wheel
        Assert.Equal(new Cdi(other).Fixing(FixingDate, Context), cdi.Clone(other).Fixing(FixingDate, Context));
        Assert.Equal(0.1274968515794599, cdi.Clone(other).Fixing(FixingDate, Context));
    }

    [Fact]
    public void TheCloneIsACdi()
    {
        OvernightIndex clone = new Cdi().Clone(new FlatForward(Today, 0.12, new Business252()));

        // QuantLib: an OvernightIndex of the same name
        Assert.IsType<Cdi>(clone);
        Assert.Equal("CDION Business/252(Brazil)", clone.Name);
    }
}
