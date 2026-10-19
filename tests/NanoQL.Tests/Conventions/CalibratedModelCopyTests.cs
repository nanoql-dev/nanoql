// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A model is immutable (playbook §2's calibration row): QuantLib's <c>setParams</c> becomes <c>SetParams</c>, which
/// returns a new model of the receiver's own type, built on a copy of the parameters it is given, with what the model
/// derives from them recomputed; the receiver keeps its parameters.
/// </summary>
public class CalibratedModelCopyTests
{
    [Fact]
    public void SetParamsReturnsANewModelOfTheReceiversType()
    {
        var curve = new FlatForward(new Date(15, Month.January, 2026), 0.04, new Actual365Fixed());
        var model = new HullWhite(curve, 0.1, 0.01);
        var parameters = new Vector([0.05, 0.02]);

        HullWhite updated = model.SetParams(parameters);

        Assert.NotSame(model, updated);
        Assert.Equal(0.05, updated.A());
        Assert.Equal(0.02, updated.Sigma);
        Assert.Equal(0.1, model.A());
        Assert.Equal(0.01, model.Sigma);
        Assert.Same(curve, updated.TermStructure);
        Assert.Equal(model.R0, updated.R0);

        // the analytical fitting parameter follows the new parameters
        var rebuilt = new HullWhite(curve, 0.05, 0.02);
        Assert.Equal(rebuilt.Dynamics().ShortRate(1.5, 0.0), updated.Dynamics().ShortRate(1.5, 0.0));
        Assert.NotEqual(model.Dynamics().ShortRate(1.5, 0.0), updated.Dynamics().ShortRate(1.5, 0.0));

        // the vector given isn't held
        parameters[0] = 0.3;
        Assert.Equal(0.05, updated.A());

        // the constraint follows the new model's arguments
        Assert.True(updated.Constraint.Test(new Vector([0.2, 0.2])));
        Assert.False(updated.Constraint.Test(new Vector([-0.2, 0.2])));

        // a Vasicek keeps its four parameters
        Vasicek vasicek = new Vasicek().SetParams(new Vector([0.2, 0.03, 0.02, 0.1]));
        Assert.Equal((double[])[0.2, 0.03, 0.02, 0.1], vasicek.Params());
        Assert.Equal(0.1, vasicek.Lambda);
    }
}
