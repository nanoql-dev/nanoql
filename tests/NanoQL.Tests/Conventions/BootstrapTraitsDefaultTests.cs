// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's optional traits members, which its global bootstrap detects (<c>globalGuess</c>, and
/// <c>transformDirect</c> with <c>transformInverse</c>), are static virtual members of
/// <see cref="IBootstrapTraits{TTermStructure}"/> whose defaults are QuantLib's behaviour without them (the playbook's
/// templates row): a traits type that doesn't define them says so, its transforms are the identity, and its global
/// guess, which the bootstrap reads only from a traits type that has one, refuses when called directly.
/// </summary>
public class BootstrapTraitsDefaultTests
{
    [Fact]
    public void TheOptionalMembersDefaultToQuantLibsBehaviourWithoutThem()
    {
        Assert.False(HasGlobalGuess<WithoutOptionalMembers>());
        Assert.False(HasTransform<WithoutOptionalMembers>());
        Assert.Equal(-0.25, TransformDirect<WithoutOptionalMembers>(-0.25));
        Assert.Equal(0.75, TransformInverse<WithoutOptionalMembers>(0.75));
        Assert.Equal(
            "the traits give no global guess",
            Assert.Throws<NotSupportedException>(() => GlobalGuess<WithoutOptionalMembers>()).Message);

        // the yield traits' own
        Assert.True(HasGlobalGuess<Discount>() && HasGlobalGuess<ZeroYield>());
        Assert.True(HasTransform<Discount>() && HasTransform<SimpleZeroYield>());
        Assert.False(HasTransform<ZeroYield>() || HasTransform<ForwardRate>());
    }

    private static bool HasGlobalGuess<T>()
        where T : IBootstrapTraits<YieldTermStructure> => T.HasGlobalGuess;

    private static bool HasTransform<T>()
        where T : IBootstrapTraits<YieldTermStructure> => T.HasTransform;

    private static double TransformDirect<T>(double x)
        where T : IBootstrapTraits<YieldTermStructure> => T.TransformDirect(x, 1, null!);

    private static double TransformInverse<T>(double x)
        where T : IBootstrapTraits<YieldTermStructure> => T.TransformInverse(x, 1, null!);

    private static (Vector Guess, bool NeedsTransform) GlobalGuess<T>()
        where T : IBootstrapTraits<YieldTermStructure> => T.GlobalGuess(null!, false);

    // A traits type with QuantLib's required members only.
    private readonly struct WithoutOptionalMembers : IBootstrapTraits<YieldTermStructure>
    {
        public static int MaxIterations => 1;

        public static Date InitialDate(BootstrapState<YieldTermStructure> c) => default;

        public static double InitialValue(BootstrapState<YieldTermStructure> c) => 1.0;

        public static double Guess(int i, BootstrapState<YieldTermStructure> c, bool validData, int firstAliveHelper) =>
            1.0;

        public static double MinValueAfter(
            int i, BootstrapState<YieldTermStructure> c, bool validData, int firstAliveHelper) => 0.0;

        public static double MaxValueAfter(
            int i, BootstrapState<YieldTermStructure> c, bool validData, int firstAliveHelper) => 2.0;

        public static void UpdateGuess(double[] data, double value, int i) => data[i] = value;
    }
}
