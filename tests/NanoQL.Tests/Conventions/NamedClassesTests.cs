// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib-Python's <c>%template</c> names are sealed subclasses of the closed generic class they name (playbook §2.6):
/// the nine generators and the two path generators, each with a <c>Clone()</c> that returns its own type; and the
/// named curves, iterative and global, each piecewise one with a <c>Create</c> that returns its own type.
/// </summary>
public class NamedClassesTests
{
    [Fact]
    public void EachNamedClassIsASubclassOfItsClosedGeneric()
    {
        (Type Named, Type Generic)[] classes =
        [
            (typeof(MersenneTwisterUniformRsg), typeof(RandomSequenceGenerator<MersenneTwisterUniformRng>)),
            (typeof(InvCumulativeMersenneTwisterGaussianRng),
                typeof(InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>)),
            (typeof(MoroInvCumulativeMersenneTwisterGaussianRng),
                typeof(InverseCumulativeRng<MersenneTwisterUniformRng, MoroInverseCumulativeNormal>)),
            (typeof(InvCumulativeMersenneTwisterGaussianRsg),
                typeof(InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal>)),
            (typeof(MoroInvCumulativeMersenneTwisterGaussianRsg),
                typeof(InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, MoroInverseCumulativeNormal>)),
            (typeof(InvCumulativeHaltonGaussianRsg), typeof(InverseCumulativeRsg<HaltonRsg, InverseCumulativeNormal>)),
            (typeof(MoroInvCumulativeHaltonGaussianRsg), typeof(InverseCumulativeRsg<HaltonRsg, MoroInverseCumulativeNormal>)),
            (typeof(InvCumulativeSobolGaussianRsg), typeof(InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal>)),
            (typeof(MoroInvCumulativeSobolGaussianRsg), typeof(InverseCumulativeRsg<SobolRsg, MoroInverseCumulativeNormal>)),
            (typeof(InvCumulativeMersenneTwisterPathGenerator),
                typeof(PathGenerator<InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal>>)),
            (typeof(GaussianSobolPathGenerator), typeof(PathGenerator<InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal>>)),
        ];
        foreach ((Type named, Type generic) in classes)
        {
            Assert.True(named.IsSealed, named.Name);
            Assert.Equal(generic, named.BaseType);
            MethodInfo clone = named.GetMethod(
                "Clone", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, Type.EmptyTypes)!;
            Assert.Equal(named, clone.ReturnType);
        }
    }

    [Fact]
    public void EachNamedCurveIsASubclassOfItsClosedGeneric()
    {
        // the iterative piecewise curves and the interpolated curves (SD5); each piecewise one hides the generic's
        // Create with its own, returning its own type
        (Type Named, Type Generic)[] piecewise =
        [
            (typeof(PiecewiseLinearSimpleZero), typeof(PiecewiseYieldCurve<SimpleZeroYield, Linear, IterativeBootstrap>)),
            (typeof(PiecewiseFlatForward), typeof(PiecewiseYieldCurve<ForwardRate, BackwardFlat, IterativeBootstrap>)),
            (typeof(PiecewiseLogLinearDiscount), typeof(PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>)),
            (typeof(PiecewiseLinearForward), typeof(PiecewiseYieldCurve<ForwardRate, Linear, IterativeBootstrap>)),
            (typeof(PiecewiseLinearZero), typeof(PiecewiseYieldCurve<ZeroYield, Linear, IterativeBootstrap>)),
            (typeof(PiecewiseCubicZero), typeof(PiecewiseYieldCurve<ZeroYield, Cubic, IterativeBootstrap>)),
            (typeof(PiecewiseLogCubicDiscount), typeof(PiecewiseYieldCurve<Discount, LogCubic, IterativeBootstrap>)),
            (typeof(PiecewiseKrugerLogDiscount), typeof(PiecewiseYieldCurve<Discount, KrugerLog, IterativeBootstrap>)),
            (typeof(PiecewiseLogMixedLinearCubicDiscount),
                typeof(PiecewiseYieldCurve<Discount, LogMixedLinearCubic, IterativeBootstrap>)),
        ];
        (Type Named, Type Generic)[] interpolated =
        [
            (typeof(DiscountCurve), typeof(InterpolatedDiscountCurve<LogLinear>)),
            (typeof(LogCubicDiscountCurve), typeof(InterpolatedDiscountCurve<LogCubic>)),
            (typeof(KrugerLogDiscountCurve), typeof(InterpolatedDiscountCurve<KrugerLog>)),
            (typeof(LogMixedLinearCubicDiscountCurve), typeof(InterpolatedDiscountCurve<LogMixedLinearCubic>)),
            (typeof(ZeroCurve), typeof(InterpolatedZeroCurve<Linear>)),
            (typeof(CubicZeroCurve), typeof(InterpolatedZeroCurve<Cubic>)),
            (typeof(SimpleZeroCurve), typeof(InterpolatedSimpleZeroCurve<Linear>)),
            (typeof(ForwardCurve), typeof(InterpolatedForwardCurve<BackwardFlat>)),
            (typeof(LinearForwardCurve), typeof(InterpolatedForwardCurve<Linear>)),
            (typeof(CubicForwardCurve), typeof(InterpolatedForwardCurve<Cubic>)),
        ];
        foreach ((Type named, Type generic) in piecewise.Concat(interpolated))
        {
            Assert.True(named.IsSealed, named.Name);
            Assert.Equal(generic, named.BaseType);
        }

        foreach ((Type named, _) in piecewise)
        {
            MethodInfo create = named.GetMethod("Create", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
            Assert.Equal(named, create.ReturnType);
        }
    }

    [Fact]
    public void EachGlobalNamedCurveIsASubclassOfItsClosedGeneric()
    {
        (Type Named, Type Generic)[] classes =
        [
            (typeof(GlobalLinearSimpleZeroCurve), typeof(PiecewiseYieldCurve<SimpleZeroYield, Linear, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseFlatForward), typeof(PiecewiseYieldCurve<ForwardRate, BackwardFlat, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseLogLinearDiscount), typeof(PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseLinearForward), typeof(PiecewiseYieldCurve<ForwardRate, Linear, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseLinearZero), typeof(PiecewiseYieldCurve<ZeroYield, Linear, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseCubicZero), typeof(PiecewiseYieldCurve<ZeroYield, Cubic, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseLogCubicDiscount), typeof(PiecewiseYieldCurve<Discount, LogCubic, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseSplineCubicDiscount), typeof(PiecewiseYieldCurve<Discount, Cubic, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseKrugerLogDiscount), typeof(PiecewiseYieldCurve<Discount, KrugerLog, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseLogMixedLinearCubicDiscount),
                typeof(PiecewiseYieldCurve<Discount, LogMixedLinearCubic, GlobalBootstrap>)),
        ];
        foreach ((Type named, Type generic) in classes)
        {
            Assert.True(named.IsSealed, named.Name);
            Assert.Equal(generic, named.BaseType);
            MethodInfo create = named.GetMethod("Create", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
            Assert.Equal(named, create.ReturnType);
        }
    }
}
