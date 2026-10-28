// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The finite-difference remainder's conventions (the S-core plan's C4): numerical differentiation refuses repeated
/// offsets (CR7 (xxxviii), the user's ruling), where QuantLib's Fornberg weights are NaN, which its evaluation skips, so
/// x³'s first derivative at 1 on offsets (−0.1, 0, 0, 0.1) is 0 for 3; the n-th order operator's offsets, the mesher's
/// distinct locations, are accepted; its weights alone need no function, as QuantLib's empty one; the jump mesher's
/// distribution refuses a time at or below 0, where QuantLib's density divides by 0, and is QuantLib's 0 at a size at
/// or below 0; the storage condition refuses a mesher not of two dimensions, which QuantLib reads unchecked; the old
/// TR-BDF2 refuses a step before its step's parts are set, as QuantLib's empty operators do; and the zero condition is
/// <c>std::max(a, 0.0)</c>. QuantLib's values beside the refusals are its own (this run's <c>probes\c4_facts.cpp</c>,
/// <c>c4_facts.out</c>).
/// </summary>
public class FiniteDifferenceRemainderConventionTests
{
    [Fact]
    public void RepeatedOffsetsAreRefused()
    {
        // QuantLib's weights -0, NaN, NaN, NaN and its derivative 0
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new NumericalDifferentiation(x => x * x * x, 1, [-0.1, 0.0, 0.0, 0.1]));
        Assert.Equal("offsets", e.ParamName);

        // a step of 0 repeats every point
        Assert.Equal("offsets", Assert.Throws<ArgumentException>(
            () => new NumericalDifferentiation(Math.Sin, 1, 0.0, 3, NumericalDifferentiationScheme.Central)).ParamName);

        // distinct offsets: 3, exactly
        Assert.True(Math.Abs(new NumericalDifferentiation(x => x * x * x, 1, [-0.1, 0.0, 0.05, 0.1]).Evaluate(1.0) - 3.0) < 1e-12);
    }

    [Fact]
    public void TheNthOrderOperatorsOffsetsAreTheMeshersDistinctLocations()
    {
        var op = new NthOrderDerivativeOp(0, 2, 3, new FdmMesherComposite(new Predefined1dMesher([0.0, 0.5, 1.5, 3.0])));
        Assert.Equal(4, op.ToMatrix().Size1);

        // repeated locations are refused by QuantLib's own check
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new NthOrderDerivativeOp(0, 1, 2, new FdmMesherComposite(new Predefined1dMesher([0.0, 1.0, 1.0, 2.0]))));
        Assert.StartsWith("inconsistent set of grid values in direction 0", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWeightsNeedNoFunction()
    {
        var weightsAlone = new NumericalDifferentiation(null, 1, 1.0, 3, NumericalDifferentiationScheme.Central);
        Assert.Equal([-0.5, 0.0, 0.5], weightsAlone.Weights());
        Assert.Throws<InvalidOperationException>(() => weightsAlone.Evaluate(0.0));
    }

    [Fact]
    public void TheJumpDistributionRefusesTimesAtOrBelowZero()
    {
        var mesher = new ExponentialJump1dMesher(10, 100.0, 4.0, 2.5);
        Assert.Equal("t", Assert.Throws<ArgumentOutOfRangeException>(() => mesher.JumpSizeDistribution(0.1, 0.0)).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => mesher.JumpSizeDistribution(0.1, -1.0));
        Assert.Equal(0.0, mesher.JumpSizeDistribution(0.0, 1.0));
        Assert.Equal(0.0, mesher.JumpSizeDistribution(-0.5, 1.0));

        Assert.StartsWith(
            "eps > 0.0 and eps < 1.0",
            Assert.Throws<ArgumentException>(() => new ExponentialJump1dMesher(10, 100.0, 4.0, 2.5, 1.0)).Message,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "minimum number of steps is two",
            Assert.Throws<ArgumentException>(() => new ExponentialJump1dMesher(1, 100.0, 4.0, 2.5)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheStorageConditionNeedsTwoDimensions()
    {
        var mesher = new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 5));
        var calculator = new FdmLogInnerValue(new PlainVanillaPayoff(OptionType.Call, 1.0), mesher, 0);
        Assert.Equal("mesher", Assert.Throws<ArgumentException>(
            () => new FdmSimpleStorageCondition([0.5], mesher, calculator, 1.0)).ParamName);
    }

    [Fact]
    public void TheOldTrBdf2NeedsItsStep()
    {
        var scheme = new TRBDF2(TridiagonalOperator.Identity(5), []);
        Assert.Equal(
            "uninitialized TridiagonalOperator",
            Assert.Throws<NanoQLException>(() => scheme.Step(new double[5], 1.0)).Message);
        scheme.SetStep(0.1);
        scheme.Step(new double[5], 1.0);
    }

    [Fact]
    public void TheZeroConditionIsStdMax()
    {
        double[] a = [-1.0, -0.0, 0.0, 2.0, double.NaN];
        new ZeroCondition().ApplyTo(a, 0.0);
        Assert.Equal(0.0, a[0]);
        Assert.True(double.IsNegative(a[1]));
        Assert.Equal(2.0, a[3]);
        Assert.True(double.IsNaN(a[4]));
    }
}
