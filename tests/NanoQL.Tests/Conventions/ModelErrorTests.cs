// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The models' and parameters' refusals, by type, message and parameter (playbook §2): a check of an argument is
/// <see cref="ArgumentException"/> naming it, a model's constructor naming its own argument where the parameter it
/// builds refuses it.
/// </summary>
public class ModelErrorTests
{
    [Fact]
    public void TheAffineModelsAndParametersRefusals()
    {
        ArgumentException factors = Assert.Throws<ArgumentException>(() => new Vasicek().DiscountBond(0.0, 1.0, new Vector()));
        Assert.Equal(typeof(ArgumentException), factors.GetType());
        Assert.Equal("factors", factors.ParamName);
        Assert.StartsWith("no factors given", factors.Message, StringComparison.Ordinal);

        ArgumentOutOfRangeException index = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ConstantParameter(0.1, new NoConstraint()).SetParam(1, 0.2));
        Assert.Equal("i", index.ParamName);
        Assert.StartsWith("parameter index 1 out of range [0, 1)", index.Message, StringComparison.Ordinal);

        ArgumentException value = Assert.Throws<ArgumentException>(() => new ConstantParameter(-0.1, new PositiveConstraint()));
        Assert.Equal("value", value.ParamName);
        Assert.StartsWith("-0.1: invalid value", value.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheShortRateModelsRefusals()
    {
        // QuantLib checks a before sigma; each refusal names the model's own argument.
        ArgumentException a = Assert.Throws<ArgumentException>(() => new Vasicek(a: 0.0, sigma: -0.01));
        Assert.Equal("a", a.ParamName);
        Assert.StartsWith("0: invalid value", a.Message, StringComparison.Ordinal);
        ArgumentException sigma = Assert.Throws<ArgumentException>(() => new Vasicek(sigma: double.NaN));
        Assert.Equal("sigma", sigma.ParamName);
        Assert.StartsWith("NaN: invalid value", sigma.Message, StringComparison.Ordinal);

        Assert.Equal("termStructure", Assert.Throws<ArgumentNullException>(() => new HullWhite(null!)).ParamName);
        var curve = new FlatForward(new Date(15, Month.January, 2026), 0.04, new Actual365Fixed());
        Assert.Equal("a", Assert.Throws<ArgumentException>(() => new HullWhite(curve, a: 0.0)).ParamName);

        ArgumentException small = Assert.Throws<ArgumentException>(() => new HullWhite(curve).SetParams(new Vector()));
        Assert.Equal("parameters", small.ParamName);
        Assert.StartsWith("parameter array too small", small.Message, StringComparison.Ordinal);

        // The convexity bias's checks, in QuantLib's order.
        (Func<double> Call, string Name, string Text)[] cases =
        [
            (() => HullWhite.ConvexityBias(-1.0, 1.0, 2.0, 0.01, 0.1), "futurePrice", "negative futures price (-1) not allowed"),
            (() => HullWhite.ConvexityBias(double.NaN, -1.0, 2.0, 0.01, 0.1), "futurePrice", "negative futures price (NaN) not allowed"),
            (() => HullWhite.ConvexityBias(95.0, -1.0, 2.0, 0.01, 0.1), "t", "negative t (-1) not allowed"),
            (() => HullWhite.ConvexityBias(95.0, 2.0, 1.0, 0.01, 0.1), "T", "T (1) must not be less than t (2)"),
            (() => HullWhite.ConvexityBias(95.0, 1.0, 2.0, -0.01, -0.1), "sigma", "negative sigma (-0.01) not allowed"),
            (() => HullWhite.ConvexityBias(95.0, 1.0, 2.0, 0.01, -0.1), "a", "negative a (-0.1) not allowed"),
        ];
        foreach ((Func<double> call, string name, string text) in cases)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => call());
            Assert.Equal(typeof(ArgumentException), e.GetType());
            Assert.Equal(name, e.ParamName);
            Assert.StartsWith(text, e.Message, StringComparison.Ordinal);
        }
    }
}
