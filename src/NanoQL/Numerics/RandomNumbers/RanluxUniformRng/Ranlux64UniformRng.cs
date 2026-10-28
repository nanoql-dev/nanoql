// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/ranluxuniformrng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// M. Luescher's "luxury" uniform random-number generator, at the luxury level its derived class sets:
/// <see cref="Ranlux3UniformRng"/> (any theoretically possible correlations have a very small chance of being observed)
/// or <see cref="Ranlux4UniformRng"/> (the highest possible luxury).
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's implementation is a proxy for the standard library's generator: a 48-bit subtract-with-carry engine
/// (<c>std::subtract_with_carry_engine&lt;std::uint_fast64_t, 48, 10, 24&gt;</c>) under a discard-block engine that
/// keeps r of every p draws, QuantLib's template parameters, here the protected constructor's. Its numbers are those of
/// MSVC's standard library, which built the Python wheel and the harness. For more detail see M. Luescher, A portable
/// high-quality random number generator for lattice field theory simulations, Comp. Phys. Comm. 79 (1994) 100.
/// </para>
/// <para>
/// A mutable working type: each draw changes the generator, and it is not thread-safe. Its seed is explicit, in
/// [1, 4294967295], with no default, where QuantLib's defaults to 19780503: the standard's seeding takes 0 as that
/// default, and reduces a seed modulo 2147483563 through its linear congruential seed sequence, so seeds that differ by
/// that modulus draw one sequence, and a multiple of it draws the sequence of seed 1, in QuantLib as here.
/// </para>
/// </remarks>
public abstract class Ranlux64UniformRng : IRng
{
    // The engine's long and short lags and its word size
    private const int R = 24;
    private const int S = 10;
    private const ulong Mod = 1UL << 48;

    private const double nx = 1.0 / (1UL << 48);

    private readonly ulong[] x;
    private readonly int blockSize;
    private readonly int usedBlock;
    private int position;
    private int carry;
    private int drawn;

    /// <summary>Initializes a new instance of the <see cref="Ranlux64UniformRng"/> class from a seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="blockSize">The block size p: of every p draws of the engine, the generator returns the first
    /// r.</param>
    /// <param name="usedBlock">The draws r kept of each block.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="seed"/> is outside [1, 4294967295], or <paramref name="usedBlock"/> is outside
    /// [1, <paramref name="blockSize"/>].
    /// </exception>
    protected Ranlux64UniformRng(long seed, int blockSize, int usedBlock)
    {
        uint s = Seeds.Check(seed);
        if (usedBlock < 1 || usedBlock > blockSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(usedBlock), usedBlock, $"the draws kept ({usedBlock}) must be in [1, {blockSize}]");
        }

        this.blockSize = blockSize;
        this.usedBlock = usedBlock;
        x = new ulong[R];

        // The engine's seeding: a linear congruential generator, seeded with the seed modulo its modulus (a multiple of
        // the modulus taking 1; QuantLib's 0, refused here, the default seed), fills each word from two of its draws.
        uint lc = s % 2147483563U;
        if (lc == 0)
        {
            lc = 1;
        }

        for (int i = 0; i < R; ++i)
        {
            lc = NextLinearCongruential(lc);
            ulong word = lc;
            lc = NextLinearCongruential(lc);
            word |= (ulong)lc << 32;
            x[i] = word % Mod;
        }

        carry = x[R - 1] == 0 ? 1 : 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Ranlux64UniformRng"/> class from a copy of another, for a derived
    /// class's <see cref="Clone"/>.
    /// </summary>
    /// <param name="source">The generator to copy, deeply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    protected Ranlux64UniformRng(Ranlux64UniformRng source)
    {
        ArgumentNullException.ThrowIfNull(source);
        x = (ulong[])source.x.Clone();
        blockSize = source.blockSize;
        usedBlock = source.usedBlock;
        position = source.position;
        carry = source.carry;
        drawn = source.drawn;
    }

    /// <summary>Draws a sample with weight 1.0 containing a random number in the [0.0, 1.0) interval.</summary>
    /// <returns>The sample.</returns>
    public Sample<double> Next() => new(Ranlux() * nx, 1.0);

    /// <summary>
    /// Copies the generator deeply: the copy's next draw equals this generator's, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public abstract Ranlux64UniformRng Clone();

    /// <inheritdoc/>
    IRng IRng.Clone() => Clone();

    private static uint NextLinearCongruential(uint previous) => (uint)((ulong)previous * 40014UL % 2147483563UL);

    // The discard-block engine's draw: r draws of the engine kept, then p - r discarded.
    private ulong Ranlux()
    {
        if (usedBlock <= drawn)
        {
            // discard values
            while (drawn++ < blockSize)
            {
                SubtractWithCarry();
            }

            drawn = 0;
        }

        ++drawn;
        return SubtractWithCarry();
    }

    // The subtract-with-carry engine's draw: x_i = (x_{i-s} - x_{i-r} - c) mod 2^48, the carry set where it underflows.
    // position holds x_{i-r}, the oldest of the last r words.
    private ulong SubtractWithCarry()
    {
        ulong xis = x[(position + R - S) % R];
        ulong xir = x[position];
        bool underflowed = false;
        ulong newx = xis;
        if (newx < xir)
        {
            underflowed = true;
        }

        newx = unchecked(newx - xir);
        if (newx < (uint)carry)
        {
            underflowed = true;
        }

        newx = unchecked(newx - (uint)carry);
        if (underflowed)
        {
            // underflowed, so add the modulus
            newx = unchecked(newx + Mod);
            carry = 1;
        }
        else
        {
            carry = 0;
        }

        x[position] = newx;
        position = (position + 1) % R;
        return newx;
    }
}
