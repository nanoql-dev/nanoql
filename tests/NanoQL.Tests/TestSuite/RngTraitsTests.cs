// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Walter Penschke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/rngtraits.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

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

    [Fact]
    public void TestRanLux()
    {
        // Testing known RanLux sequence...

        var ranlux3 = new Ranlux3UniformRng(2938723U);
        var ranlux4 = new Ranlux4UniformRng(4390109U);

        Real[] ranlux3_expected =
        [
            0.307448851544538826, 0.666313657894363587, 0.698528013702823358,
            0.0217381272445322793, 0.862964516238161394, 0.909193419106014034,
            0.674484308686746914, 0.849607570377191479, 0.054626078713596371,
            0.416474163715683687,
        ];

        Real[] ranlux4_expected =
        [
            0.222209169374078641, 0.420181950405986271, 0.0302156663005135329,
            0.0836259809475237148, 0.480549766594993599, 0.723472021829124401,
            0.905819507194266293, 0.54072519936540786, 0.445908421479817463,
            0.651084788437518824,
        ];

        for (int i = 0; i < 10010; ++i)
        {
            ranlux3.Next();
            ranlux4.Next();
        }

        for (int i = 0; i < 10; ++i)
        {
            if (!CloseEnough(ranlux3.Next().Value, ranlux3_expected[i]))
            {
                Assert.Fail("failed to reproduce ranlux3 numbers...");
            }

            if (!CloseEnough(ranlux4.Next().Value, ranlux4_expected[i]))
            {
                Assert.Fail("failed to reproduce ranlux4 numbers...");
            }
        }
    }
}
