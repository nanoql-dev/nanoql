// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A swaption helper's pricing engine (playbook §2's calibration row): <c>SetPricingEngine</c> returns a copy holding
/// the engine, a delegate that takes the model being calibrated, and leaves the receiver without it; the engine is kept
/// for any model, so pricing the copy against a model of another type than the engine's throws
/// <see cref="InvalidCastException"/>, .NET's type for a failed cast, as a result of another type does.
/// </summary>
public class SwaptionHelperEngineTests
{
    [Fact]
    public void AHelperPricedAgainstAModelOfAnotherTypeThrowsInvalidCastException()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.February, 2002) };
        var curve = new FlatForward(new Date(19, Month.February, 2002), 0.04875825, new Actual365Fixed());
        var helper = new SwaptionHelper(
            new Period(1, TimeUnit.Years),
            new Period(5, TimeUnit.Years),
            0.1148,
            new Euribor6M(curve),
            new Period(1, TimeUnit.Years),
            new Thirty360(Thirty360Convention.BondBasis),
            new Actual360(),
            curve,
            context);
        var hullWhite = new HullWhite(curve);
        var vasicek = new Vasicek();

        SwaptionHelper priced = helper.SetPricingEngine<HullWhite>((s, c, m) => JamshidianSwaptionEngine.Calculate(s, c, m));
        Assert.NotSame(helper, priced);
        // The wheel's value, within the tolerance: Hull-White's B is QuantLib's master's form.
        Assert.True(Tolerance.Within(0.013060880278853937, priced.ModelValue(hullWhite, context)));
        Assert.Throws<InvalidCastException>(() => priced.ModelValue(vasicek, context));
        Assert.Throws<InvalidCastException>(() => priced.CalibrationError(vasicek, context));

        // The receiver keeps no engine; a copy with none is QuantLib's empty engine.
        Assert.Equal("null pricing engine", Assert.Throws<NanoQLException>(() => helper.ModelValue(hullWhite, context)).Message);
        SwaptionHelper none = priced.SetPricingEngine<HullWhite>(null);
        Assert.Equal("null pricing engine", Assert.Throws<NanoQLException>(() => none.ModelValue(hullWhite, context)).Message);
        Assert.Equal(helper.MarketValue, priced.MarketValue);
        Assert.Same(helper.Swaption, priced.Swaption);
    }
}
