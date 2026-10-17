// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib-Python's <c>%template</c> names are sealed subclasses of the closed generic class they name (playbook §2.6):
/// the nine generators and the two path generators, each with a <c>Clone()</c> that returns its own type.
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
}
