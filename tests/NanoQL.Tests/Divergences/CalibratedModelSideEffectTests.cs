// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// What QuantLib's calibrated model changes in itself. Its <c>setParams</c> writes each parameter as it reaches it
/// (<c>model.cpp:138-149</c>): an array too short sets the leading parameters before refusing "parameter array too
/// small", one too long sets them all before refusing "parameter array too big!" (the wheel: <c>[0.2, 0.01]</c> and
/// <c>[0.2, 0.02]</c> left on a Hull-White model from <c>[0.1, 0.01]</c>). nanoql's models are immutable:
/// <c>SetParams</c> returns a new model, and refuses both with QuantLib's texts before anything is written, so the
/// receiver keeps its parameters.
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
}
