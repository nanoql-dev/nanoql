// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2002 Peter Jäckel "Monte Carlo Methods in Finance".
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause AND LicenseRef-Jaeckel
// Upstream: ql/math/primenumbers.hpp ql/math/primenumbers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// ===========================================================================
// NOTE: The following copyright notice applies to the original code,
//
// Copyright (C) 2002 Peter Jäckel "Monte Carlo Methods in Finance".
// All rights reserved.
//
// Permission to use, copy, modify, and distribute this software is freely
// granted, provided that this notice is preserved.
// ===========================================================================

namespace NanoQL.Numerics;

/// <summary>The prime numbers, by index: 2, 3, 5, 7, … Taken from "Monte Carlo Methods in Finance", by Peter Jäckel.</summary>
/// <remarks>
/// QuantLib grows one shared list of primes on demand. nanoql keeps no shared state: each call starts from Jäckel's
/// table of the first primes and extends it, in the same order, to the index it needs.
/// </remarks>
public static class PrimeNumbers
{
    private static ReadOnlySpan<uint> FirstPrimes =>
    [
        // the first two primes are mandatory for bootstrapping
        2, 3,
        // optional additional precomputed primes
        5, 7, 11, 13, 17, 19, 23, 29,
        31, 37, 41, 43, 47,
    ];

    /// <summary>The prime at <paramref name="absoluteIndex"/>, counting 2 as the 0th.</summary>
    /// <param name="absoluteIndex">The index, non-negative.</param>
    /// <returns>The prime, QuantLib's <c>BigNatural</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="absoluteIndex"/> is negative.</exception>
    public static long Get(int absoluteIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(absoluteIndex);
        var primeNumbers = new List<uint>(FirstPrimes.Length);
        primeNumbers.AddRange(FirstPrimes);
        while (primeNumbers.Count <= absoluteIndex)
        {
            NextPrimeNumber(primeNumbers);
        }

        return primeNumbers[absoluteIndex];
    }

    /// <summary>
    /// The primes at indices 0 to <paramref name="count"/> − 1, as <see cref="Get"/> gives them, computed in one pass
    /// for a caller that needs them all (<see cref="HaltonRsg"/>, one per dimension).
    /// </summary>
    /// <param name="count">How many primes, non-negative.</param>
    /// <returns>The primes, QuantLib's <c>BigNatural</c>s.</returns>
    internal static uint[] First(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var primeNumbers = new List<uint>(Math.Max(count, FirstPrimes.Length));
        primeNumbers.AddRange(FirstPrimes);
        while (primeNumbers.Count < count)
        {
            NextPrimeNumber(primeNumbers);
        }

        return [.. primeNumbers.Take(count)];
    }

    // Appends the prime after the list's last, by trial division by the primes up to its square root.
    private static uint NextPrimeNumber(List<uint> primeNumbers)
    {
        uint p, n, m = primeNumbers[^1];
        do
        {
            // skip the even numbers
            m += 2;
            n = (uint)Math.Sqrt((double)m);
            // i=1 since the even numbers have already been skipped
            int i = 1;
            do
            {
                p = primeNumbers[i];
                ++i;
            }
            while (((m % p) != 0U) && p <= n);
        }
        while (p <= n);

        primeNumbers.Add(m);
        return m;
    }
}
