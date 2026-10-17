// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Walter Penschke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/rngtraits.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testRanLux

using NanoQL.Numerics;
using static NanoQL.Numerics.ComparisonFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class RngTraitsTests
{
    [Fact]
    public void TestGaussian()
    {
        var rsg = PseudoRandom.MakeSequenceGenerator(100, 1234);

        ReadOnlySpan<Real> values = rsg.NextSequence().Value.Span;
        Real sum = 0.0;
        foreach (Real value in values)
        {
            sum += value;
        }

        Real stored = 4.09916;
        Real tolerance = 1.0e-5;
        if (Math.Abs(sum - stored) > tolerance)
        {
            Assert.Fail($"the sum of the samples does not match the stored value\n    calculated: {sum}\n    expected:   {stored}");
        }
    }

    // Adapted: without its reset of PoissonPseudoRandom::icInstance to null (lines 58-59), a global nanoql doesn't
    // port: MakeSequenceGenerator without an inverse cumulative uses its zero value, QuantLib's default construction.
    [Fact]
    public void TestDefaultPoisson()
    {
        var rsg = PoissonPseudoRandom.MakeSequenceGenerator(100, 1234);

        ReadOnlySpan<Real> values = rsg.NextSequence().Value.Span;
        Real sum = 0.0;
        foreach (Real value in values)
        {
            sum += value;
        }

        Real stored = 108.0;
        if (!Close(sum, stored))
        {
            Assert.Fail($"the sum of the samples does not match the stored value\n    calculated: {sum}\n    expected:   {stored}");
        }
    }

    // Adapted: the inverse cumulative QuantLib sets in the global PoissonPseudoRandom::icInstance (lines 79-81) is
    // MakeSequenceGenerator's argument, as nanoql doesn't port the global.
    [Fact]
    public void TestCustomPoisson()
    {
        var rsg = PoissonPseudoRandom.MakeSequenceGenerator(100, 1234, new InverseCumulativePoisson(4.0));

        ReadOnlySpan<Real> values = rsg.NextSequence().Value.Span;
        Real sum = 0.0;
        foreach (Real value in values)
        {
            sum += value;
        }

        Real stored = 409.0;
        if (!Close(sum, stored))
        {
            Assert.Fail($"the sum of the samples does not match the stored value\n    calculated: {sum}\n    expected:   {stored}");
        }
    }
}
