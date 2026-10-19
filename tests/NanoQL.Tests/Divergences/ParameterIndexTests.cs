// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A parameter's index out of range, and its parameters before they are set. QuantLib's <c>Parameter::setParam(i, x)</c>
/// writes <c>params_[i]</c> unchecked (<c>parameter.hpp:51</c>): a silent heap write past a constant parameter's one
/// value, an access violation on a null parameter's empty array. nanoql refuses the index with
/// <see cref="ArgumentOutOfRangeException"/> naming <c>i</c>, before the copy it returns. QuantLib's
/// <c>ConstantParameter(constraint)</c> and <c>PiecewiseConstantParameter(times)</c> leave their parameters
/// uninitialized (<c>parameter.hpp:64-65, 78-83, 134-141</c>), so a read before they are set is indeterminate; nanoql's
/// are 0.
/// </summary>
public class ParameterIndexTests
{
    [Fact]
    public void AnIndexOutOfRangeIsRefused()
    {
        var constant = new ConstantParameter(0.1, new PositiveConstraint());
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => constant.SetParam(5, 0.2)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => constant.SetParam(-1, 0.2)).ParamName);
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => new NullParameter().SetParam(0, 0.2)).ParamName);

        // In range, the copy has the value, the receiver keeps its own, and the constraint isn't checked.
        Parameter copy = constant.SetParam(0, -5.0);
        Assert.Equal(-5.0, copy.Evaluate(0.0));
        Assert.Equal(0.1, constant.Evaluate(0.0));
    }

    [Fact]
    public void ParametersBeforeTheyAreSetAreZero()
    {
        var constant = new ConstantParameter(new PositiveConstraint());
        Assert.Equal(0.0, constant.Params()[0]);
        Assert.Equal(0.0, constant.Evaluate(1.0));

        var piecewise = new PiecewiseConstantParameter([1.0, 2.0]);
        Assert.Equal(3, piecewise.Size);
        Assert.All(piecewise.Params(), p => Assert.Equal(0.0, p));
    }
}
