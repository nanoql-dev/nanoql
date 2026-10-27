// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2009 Liquidnet Holdings, Inc.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/fastfouriertransform.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;
using NanoQL.Numerics;
using Vector = NanoQL.Numerics.Vector;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class FastFourierTransformTests
{
    [Fact]
    public void TestSimple()
    {
        // Testing complex direct FFT...
        Complex[] a = [new(0, 0), new(1, 1), new(3, 3), new(4, 4), new(4, 4), new(3, 3), new(1, 1), new(0, 0)];
        var b = new Complex[8];
        var fft = new FastFourierTransform(3);
        fft.Transform(a, b);
        Complex[] expected =
        [
            new(16, 16), new(-4.8284, -11.6569), new(0, 0), new(-0.3431, 0.8284),
            new(0, 0), new(0.8284, -0.3431), new(0, 0), new(-11.6569, -4.8284),
        ];
        for (int i = 0; i < 8; i++)
        {
            if ((Math.Abs(b[i].Real - expected[i].Real) > 1.0e-2) || (Math.Abs(b[i].Imaginary - expected[i].Imaginary) > 1.0e-2))
            {
                Assert.Fail($"Convolution({i})\n    calculated: {b[i]}\n    expected:   {expected[i]}");
            }
        }
    }

    [Fact]
    public void TestInverse()
    {
        // Testing convolution via inverse FFT...
        var x = new Vector(3);
        x[0] = 1;
        x[1] = 2;
        x[2] = 3;

        int order = FastFourierTransform.MinOrder(x.Size) + 1;
        var fft = new FastFourierTransform(order);

        int nFrq = fft.OutputSize;
        var ft = new Complex[nFrq];
        var tmp = new Real[nFrq];
        Complex z = default;

        fft.InverseTransform(x.AsSpan(), ft);
        for (int i = 0; i < nFrq; ++i)
        {
            tmp[i] = (ft[i].Real * ft[i].Real) + (ft[i].Imaginary * ft[i].Imaginary);
            ft[i] = z;
        }

        fft.InverseTransform(tmp, ft);

        // 0
        Real calculated = ft[0].Real / nFrq;
        Real expected = (x[0] * x[0]) + (x[1] * x[1]) + (x[2] * x[2]);
        if (Math.Abs(calculated - expected) > 1.0e-10)
        {
            Assert.Fail($"Convolution(0)\n    calculated: {calculated}\n    expected:   {expected}");
        }

        // 1
        calculated = ft[1].Real / nFrq;
        expected = (x[0] * x[1]) + (x[1] * x[2]);
        if (Math.Abs(calculated - expected) > 1.0e-10)
        {
            Assert.Fail($"Convolution(1)\n    calculated: {calculated}\n    expected:   {expected}");
        }

        // 2
        calculated = ft[2].Real / nFrq;
        expected = x[0] * x[2];
        if (Math.Abs(calculated - expected) > 1.0e-10)
        {
            Assert.Fail($"Convolution(1)\n    calculated: {calculated}\n    expected:   {expected}");
        }
    }

    [Fact]
    public void TestTrivialOrder()
    {
        // Testing FFT of size 1 (order 0)...

        // min_order(1) is 0; constructing an FFT of order 0 used to write out of bounds in the constructor.  A size-1
        // transform reduces to a copy of the single input element.
        Assert.Equal(0, FastFourierTransform.MinOrder(1));

        var fft = new FastFourierTransform(0);
        Assert.Equal(1, fft.OutputSize);

        Complex[] a = [new(2.5, -1.5)];
        var b = new Complex[1];
        fft.Transform(a, b);
        if (Math.Abs(b[0].Real - a[0].Real) > 1.0e-12 || Math.Abs(b[0].Imaginary - a[0].Imaginary) > 1.0e-12)
        {
            Assert.Fail($"Size-1 FFT\n    calculated: {b[0]}\n    expected:   {a[0]}");
        }
    }
}
