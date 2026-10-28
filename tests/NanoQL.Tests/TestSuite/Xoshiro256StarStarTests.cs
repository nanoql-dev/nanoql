// SPDX-FileCopyrightText: Copyright (C) 2023 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause AND CC0-1.0
// Upstream: test-suite/xoshiro256starstar.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// The block below inlines the reference implementation
// from https://prng.di.unimi.it/xoshiro256starstar.c
//
// Written in 2018 by David Blackman and Sebastiano Vigna (vigna@acm.org)
//
// To the extent possible under law, the author has dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// See <http://creativecommons.org/publicdomain/zero/1.0/>.

using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class Xoshiro256StarStarTests
{
    [Fact]
    public void TestMeanAndStdDevOfNextReal()
    {
        // Testing Xoshiro256StarStarUniformRng::nextReal() for mean=0.5 and stddev=1/12...

        var random = new Xoshiro256StarStarUniformRng(1);
        const int iterations = 10_000_000;
        var randoms = new List<Real>(iterations);
        for (int j = 0; j < iterations; ++j)
        {
            Real next = random.NextReal();
            if (next <= 0.0 || 1.0 <= next)
            {
                Assert.Fail($"next {next} not in range");
            }

            randoms.Add(next);
        }

        Real sum = 0.0;
        foreach (Real r in randoms)
        {
            sum += r;
        }

        Real mean = sum / randoms.Count;
        Real meanError = Math.Abs(0.5 - mean);
        if (meanError > 0.005)
        {
            Assert.Fail($"Mean {mean} for seed 1 is not close to 0.5.");
        }

        Real inner = 0.0;
        foreach (Real r in randoms)
        {
            Real diff = r - mean;
            inner += diff * diff;
        }

        Real stdDev = inner / randoms.Count;
        Real stdDevError = Math.Abs((1.0 / 12.0) - stdDev);
        if (stdDevError > 0.00005)
        {
            Assert.Fail($"Standard deviation {stdDev} for seed 1 is not close to 1/12.");
        }
    }

    [Fact]
    public void TestAgainstReferenceImplementationInC()
    {
        // Testing Xoshiro256StarStarUniformRng::nextInt64() against reference implementation in C...

        // some random initial seed
        const ulong seed = 10108360646465513120UL;
        const ulong s0 = 18274946675476036270UL;
        const ulong s1 = 6043068446171522962UL;
        const ulong s2 = 96311065249897859UL;
        const ulong s3 = 16504445955133574805UL;

        var reference = new ReferenceImplementation(s0, s1, s2, s3);

        var rngFromSeed = new Xoshiro256StarStarUniformRng(unchecked((long)seed));
        var rngFroms0s1s2s3 = new Xoshiro256StarStarUniformRng(
            unchecked((long)s0), unchecked((long)s1), unchecked((long)s2), unchecked((long)s3));

        for (int i = 0; i < 1_000; i++)
        {
            ulong nextRefImpl = reference.Next();
            ulong nextFromSeed = unchecked((ulong)rngFromSeed.NextInt64());
            ulong nextFroms0s1s2s3 = unchecked((ulong)rngFroms0s1s2s3.NextInt64());
            if (nextRefImpl != nextFromSeed)
            {
                Assert.Fail(
                    $"Test failed at index {i} (expected from reference implementation: {nextRefImpl}ULL, from "
                    + $"Xoshiro256StarStarUniformRng({seed}ULL): {nextFromSeed}ULL)");
            }

            if (nextFroms0s1s2s3 != nextFromSeed)
            {
                Assert.Fail(
                    $"Test failed at index {i} (from Xoshiro256StarStarUniformRng({seed}): {nextFroms0s1s2s3}ULL, from "
                    + $"Xoshiro256StarStarUniformRng({s0}ULL, {s1}ULL, {s2}ULL, {s3}ULL): {nextFromSeed}ULL)");
            }
        }
    }

    [Fact]
    public void TestAbsenceOfInteractionBetweenInstances()
    {
        // Testing Xoshiro256StarStarUniformRng for absence of interaction between instances...

        long seed = unchecked((long)16880566536755896171UL);
        var rng = new Xoshiro256StarStarUniformRng(seed);
        for (int i = 0; i < 999; ++i)
        {
            rng.NextInt64();
        }

        long referenceValue = rng.NextInt64();

        // sequential use
        Xoshiro256StarStarUniformRng rng1 = new(seed), rng2 = new(seed);
        for (int i = 0; i < 1_000; i++)
        {
            rng1.NextInt64();
        }

        for (int i = 0; i < 999; i++)
        {
            rng2.NextInt64();
        }

        if (referenceValue != rng2.NextInt64())
        {
            Assert.Fail("Detected interaction between Xoshiro256StarStarUniformRng instances during sequential computation");
        }

        // parallel use
        Xoshiro256StarStarUniformRng rng3 = new(seed), rng4 = new(seed);
        for (int i = 0; i < 999; i++)
        {
            rng3.NextInt64();
            rng4.NextInt64();
        }

        if (referenceValue != rng3.NextInt64() || referenceValue != rng4.NextInt64())
        {
            Assert.Fail("Detected interaction between Xoshiro256StarStarUniformRng instances during parallel computation");
        }
    }

    // The reference implementation's next(), its static state s[4] held here; QuantLib's test copies it inside an
    // extern "C" block.
    private sealed class ReferenceImplementation(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        private readonly ulong[] s = [s0, s1, s2, s3];

        public ulong Next()
        {
            ulong result = unchecked(Rotl(s[1] * 5, 7) * 9);

            ulong t = s[1] << 17;

            s[2] ^= s[0];
            s[3] ^= s[1];
            s[1] ^= s[2];
            s[0] ^= s[3];

            s[2] ^= t;

            s[3] = Rotl(s[3], 45);

            return result;
        }

        private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));
    }
}
