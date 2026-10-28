// SPDX-FileCopyrightText: Copyright (C) 2024 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/zigguratgaussian.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class ZigguratGaussianTests
{
    [Fact]
    public void TestStatisticsOfNextReal()
    {
        // Testing ZigguratGaussianRng<Xoshiro256StarStarUniformRng>::nextReal() for mean, variance, skewness and
        // kurtosis...

        long seed = 42L;
        var uniformRandom = new Xoshiro256StarStarUniformRng(seed);
        var random = new ZigguratGaussianRng<Xoshiro256StarStarUniformRng>(uniformRandom);
        var randoms = new IncrementalStatistics();
        int iterations = 10_000_000;
        for (int j = 0; j < iterations; ++j)
        {
            Real next = random.Next().Value;
            randoms.Add(next);
        }

        Real mean = randoms.Mean();
        Real variance = randoms.Variance();
        Real skewness = randoms.Skewness();
        Real kurtosis = randoms.Kurtosis();

        if (Math.Abs(mean) > 0.001)
        {
            Assert.Fail($"Mean {mean} for seed {seed} is not close to 0.");
        }

        if (Math.Abs(1.0 - variance) > 0.005)
        {
            Assert.Fail($"Variance {variance} for seed {seed} is not close to 1.");
        }

        if (Math.Abs(skewness) > 0.001)
        {
            Assert.Fail($"Skewness {skewness} for seed {seed} is not close to 0.");
        }

        if (Math.Abs(kurtosis) > 0.03)
        {
            Assert.Fail($"Kurtosis {kurtosis} for seed {seed} is not close to 0.");
        }
    }

    [Fact]
    public void TestWedgeDrawIsAccepted()
    {
        // Testing ZigguratGaussianRng<ScriptedRng>::nextReal() accepts a draw that falls in a wedge...

        // Layer 1 runs from normX(1) = 3.654152885361008796 down to
        // normX(2) = 3.449278298560964462. A draw whose |x| lands between the two
        // misses the rectangle fast path and must go through the wedge acceptance
        // test, which is the only way such a value can be produced from layer 1.
        //
        // Take x = 3.5, comfortably inside that band, and feed the wedge test
        // u = 0.5. Marsaglia-Tsang acceptance is
        //
        //     normF(i+1) + (normF(i) - normF(i+1)) * u  <  pdf(x)
        //
        // and with normF(1) = 0.001260285930498598,
        //          normF(2) = 0.002609072746106363,
        //          pdf(3.5) = 0.002187491118182889:
        //
        //     lhs = 0.001934679338302481  <  pdf(x)     -> accept, margin 2.5e-04
        //
        // so the generator must return the wedge draw itself.
        const int wedgeLayer = 1;
        const Real normX1 = 3.654152885361008796;
        const Real wedgeX = 3.5;

        // The generator consumes this second draw only if it rejected the first:
        // layer 200 with |x| = 0.1035, well inside normX(201), so the rectangle
        // fast path returns it immediately. It sits 3.4 away from the wedge value,
        // which makes the two outcomes unambiguous.
        const int fastPathLayer = 200;
        const Real fastPathU = 0.1;

        var scripted = new ScriptedRng(
            [DrawFor(wedgeLayer, wedgeX / normX1), DrawFor(fastPathLayer, fastPathU)],
            [0.5]);
        var random = new ZigguratGaussianRng<ScriptedRng>(scripted);
        Real sample = random.Next().Value;
        if (Math.Abs(sample - wedgeX) > 1.0e-12)
        {
            Assert.Fail(
                "ZigguratGaussianRng rejected a wedge draw that the Marsaglia-Tsang acceptance test accepts.\n"
                + $"    expected {wedgeX} (the wedge draw)\n"
                + $"    returned {sample} (the following fast-path draw)");
        }
    }

    // nextReal() splits a single 64-bit draw into the ordinate and the layer,
    //     u = 2*((r >> 11) + 0.5)/2^53 - 1,      i = r & 0xff
    // so inverting it lets a test ask for a specific (layer, u) pair.
    // The preconditions are load-bearing rather than decorative: a layer of
    // 256 or more would alias through `r & 0xff` (256 becomes layer 0, the
    // tail case), and u = 1 would round the bucket up to 2^53, whose shift
    // overflows to zero and decodes back as u = -1. Either would silently
    // encode a different draw than the one asked for.
    private static long DrawFor(int layer, Real u)
    {
        Assert.True(layer >= 0 && layer <= 0xFF, $"layer {layer} outside [0, 255]");
        Assert.True(u >= -1.0 && u < 1.0, $"u {u} outside [-1, 1)");
        Real twoPow53 = Math.ScaleB(1.0, 53);
        ulong bucket = (ulong)(long)Math.Round(((u + 1.0) / 2.0 * twoPow53) - 0.5, MidpointRounding.AwayFromZero);
        Assert.True(bucket < (1UL << 53), $"bucket {bucket} overflows for u = {u}");
        return unchecked((long)((bucket << 11) | (uint)layer));
    }

    // Deterministic stand-in for the uniform source.
    // ZigguratGaussianRng is a template over its RNG and documents the
    // interface it needs (nextReal / nextInt64), so a scripted source lets a
    // test drive one exact draw instead of relying on aggregate statistics.
    private sealed class ScriptedRng(long[] ints, Real[] reals) : IInt64Rng
    {
        private int intPos;
        private int realPos;

        public long NextInt64()
        {
            Assert.True(intPos < ints.Length, "scripted nextInt64() exhausted");
            return ints[intPos++];
        }

        public Real NextReal()
        {
            Assert.True(realPos < reals.Length, "scripted nextReal() exhausted");
            return reals[realPos++];
        }

        public Sample<Real> Next() => new(NextReal(), 1.0);

        public IRng Clone()
        {
            var copy = new ScriptedRng(ints, reals) { intPos = intPos, realPos = realPos };
            return copy;
        }
    }
}
