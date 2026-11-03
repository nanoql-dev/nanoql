// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// What QuantLib's calibrated model changes in itself. Its <c>setParams</c> writes each parameter as it reaches it
/// (<c>model.cpp:138-149</c>): an array too short sets the leading parameters before refusing "parameter array too
/// small", one too long sets them all before refusing "parameter array too big!" (the wheel: <c>[0.2, 0.01]</c> and
/// <c>[0.2, 0.02]</c> left on a Hull-White model from <c>[0.1, 0.01]</c>). Its <c>value</c> sets the parameters it
/// is given into the model before it prices the helpers (<c>model.cpp:46-47, 117-124</c>), so the model is left at
/// them, and the helpers whose engines hold it price at them afterwards. nanoql's models are immutable:
/// <c>SetParams</c> returns a new model, and refuses both with QuantLib's texts before anything is written, so the
/// receiver keeps its parameters; and <c>Value</c> prices against a new model with the parameters and changes nothing.
/// </summary>
public class CalibratedModelSideEffectTests
{
    [Fact]
    public void ARefusedSetParamsLeavesTheModelUnchanged()
    {
        var model = new HullWhite(new FlatForward(new Date(15, Month.January, 2026), 0.04, new Actual365Fixed()));

        ArgumentException small = Assert.Throws<ArgumentException>(() => model.SetParams(new Vector([0.2])));
        Assert.StartsWith("parameter array too small", small.Message, StringComparison.Ordinal);
        Assert.Equal((double[])[0.1, 0.01], model.Params());

        ArgumentException big = Assert.Throws<ArgumentException>(() => model.SetParams(new Vector([0.2, 0.02, 0.3])));
        Assert.StartsWith("parameter array too big!", big.Message, StringComparison.Ordinal);
        Assert.Equal((double[])[0.1, 0.01], model.Params());
        Assert.Equal(0.1, model.A());
        Assert.Equal(0.01, model.Sigma);
    }

    [Fact]
    public void ValueLeavesTheModelUnchanged()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.February, 2002) };
        YieldTermStructure termStructure = new FlatForward(new Date(19, Month.February, 2002), 0.04875825, new Actual365Fixed());
        var index = new Euribor6M(termStructure);
        var model = new HullWhite(termStructure);
        List<CalibrationHelper> helpers = [];
        foreach ((int start, int length, double volatility) in new[] { (1, 5, 0.1148), (2, 4, 0.1108), (3, 3, 0.1070), (4, 2, 0.1021), (5, 1, 0.1000) })
        {
            helpers.Add(new SwaptionHelper(
                    new Period(start, TimeUnit.Years),
                    new Period(length, TimeUnit.Years),
                    volatility,
                    index,
                    new Period(1, TimeUnit.Years),
                    new Thirty360(Thirty360Convention.BondBasis),
                    new Actual360(),
                    termStructure,
                    context)
                .SetPricingEngine<HullWhite>((s, c, m) => JamshidianSwaptionEngine.Calculate(s, c, m)));
        }

        // The wheel's values, within the tolerance: Hull-White's B is QuantLib's master's form.
        var first = (SwaptionHelper)helpers[0];
        Assert.True(Tolerance.Within(0.013060880278853937, first.ModelValue(model, context)));

        // QuantLib's value, which leaves the wheel's model at [0.05, 0.01] and its first helper's model value at
        // 0.01497978949485756
        Assert.True(Tolerance.Within(1.5826408331265667, model.Value(new Vector([0.05, 0.01]), helpers, context)));
        Assert.Equal((double[])[0.1, 0.01], model.Params());
        Assert.True(Tolerance.Within(0.013060880278853937, first.ModelValue(model, context)));
    }
}
