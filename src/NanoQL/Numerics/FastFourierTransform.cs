// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2009 Liquidnet Holdings, Inc.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/fastfouriertransform.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;

namespace NanoQL.Numerics;

/// <summary>
/// The fast Fourier transform of order k: the radix-2 Cooley-Tukey transform of 2ᵏ points.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, its twiddle factors computed at construction by QuantLib's double-angle recursion (accurate to about
/// 3e-11 relative at order 18). The transforms write into a caller's span of at least <see cref="OutputSize"/>
/// points, the input in bit-reversed order at the first positions and the rest left as the caller gave them, as
/// QuantLib's iterators do, so a shorter input is zero-padded where the caller's span is zero.
/// </para>
/// <para>
/// A documented divergence (the S-bar plan's §2.2 undefined row): an input longer than 2ᵏ points is refused before
/// anything is written, where QuantLib writes past the output's end and then refuses.
/// </para>
/// </remarks>
public sealed class FastFourierTransform
{
    private readonly double[] cs;
    private readonly double[] sn;

    /// <summary>Initializes a new instance of the <see cref="FastFourierTransform"/> class.</summary>
    /// <param name="order">The order k: the transform is over 2ᵏ points.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is negative or past 30.</exception>
    public FastFourierTransform(int order)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(order, 30);
        cs = new double[order];
        sn = new double[order];

        // For order == 0 the transform is over a single element and reduces to a copy; no twiddle factors need to
        // be computed.
        if (order == 0)
        {
            return;
        }

        int m = 1 << order;
        cs[order - 1] = Math.Cos(2 * MathConstants.M_PI / m);
        sn[order - 1] = Math.Sin(2 * MathConstants.M_PI / m);
        for (int i = order - 1; i > 0; --i)
        {
            cs[i - 1] = (cs[i] * cs[i]) - (sn[i] * sn[i]);
            sn[i - 1] = 2 * sn[i] * cs[i];
        }
    }

    /// <summary>Gets the required size of the output: 2ᵏ points.</summary>
    public int OutputSize => 1 << cs.Length;

    /// <summary>The minimum order required for the given input size.</summary>
    /// <param name="inputSize">The number of input points.</param>
    /// <returns>⌈log₂ n⌉.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inputSize"/> isn't positive.</exception>
    public static int MinOrder(int inputSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(inputSize);
        return (int)Math.Ceiling(Math.Log(inputSize) / MathConstants.M_LN2);
    }

    /// <summary>The transform of real points.</summary>
    /// <param name="input">The points.</param>
    /// <param name="output">The output, of at least <see cref="OutputSize"/> points.</param>
    /// <exception cref="ArgumentException">The output is shorter than <see cref="OutputSize"/>.</exception>
    /// <exception cref="NanoQLException">The input is longer than <see cref="OutputSize"/>.</exception>
    public void Transform(ReadOnlySpan<double> input, Span<Complex> output) => TransformImpl(input, output, false);

    /// <summary>The transform of complex points.</summary>
    /// <param name="input">The points.</param>
    /// <param name="output">The output, of at least <see cref="OutputSize"/> points.</param>
    /// <exception cref="ArgumentException">The output is shorter than <see cref="OutputSize"/>.</exception>
    /// <exception cref="NanoQLException">The input is longer than <see cref="OutputSize"/>.</exception>
    public void Transform(ReadOnlySpan<Complex> input, Span<Complex> output) => TransformImpl(input, output, false);

    /// <summary>The inverse transform of real points, unscaled.</summary>
    /// <param name="input">The points.</param>
    /// <param name="output">The output, of at least <see cref="OutputSize"/> points.</param>
    /// <exception cref="ArgumentException">The output is shorter than <see cref="OutputSize"/>.</exception>
    /// <exception cref="NanoQLException">The input is longer than <see cref="OutputSize"/>.</exception>
    public void InverseTransform(ReadOnlySpan<double> input, Span<Complex> output) => TransformImpl(input, output, true);

    /// <summary>The inverse transform of complex points, unscaled.</summary>
    /// <param name="input">The points.</param>
    /// <param name="output">The output, of at least <see cref="OutputSize"/> points.</param>
    /// <exception cref="ArgumentException">The output is shorter than <see cref="OutputSize"/>.</exception>
    /// <exception cref="NanoQLException">The input is longer than <see cref="OutputSize"/>.</exception>
    public void InverseTransform(ReadOnlySpan<Complex> input, Span<Complex> output) => TransformImpl(input, output, true);

    private void TransformImpl(ReadOnlySpan<double> input, Span<Complex> output, bool inverse)
    {
        int order = cs.Length;
        CheckSizes(input.Length, output);
        for (int i = 0; i < input.Length; ++i)
        {
            output[BitReverse(i, order)] = input[i];
        }

        Butterflies(output, inverse);
    }

    private void TransformImpl(ReadOnlySpan<Complex> input, Span<Complex> output, bool inverse)
    {
        int order = cs.Length;
        CheckSizes(input.Length, output);
        for (int i = 0; i < input.Length; ++i)
        {
            output[BitReverse(i, order)] = input[i];
        }

        Butterflies(output, inverse);
    }

    private void CheckSizes(int inputSize, Span<Complex> output)
    {
        if (output.Length < OutputSize)
        {
            throw new ArgumentException($"the output holds {output.Length} points, fewer than the transform's {OutputSize}", nameof(output));
        }

        if (!(inputSize <= OutputSize))
        {
            throw new NanoQLException("FFT order is too small");
        }
    }

    private void Butterflies(Span<Complex> output, bool inverse)
    {
        int order = cs.Length;
        int n = 1 << order;
        for (int s = 1; s <= order; ++s)
        {
            int m = 1 << s;
            Complex w = 1.0;
            var wm = new Complex(cs[s - 1], inverse ? sn[s - 1] : -sn[s - 1]);
            for (int j = 0; j < m / 2; ++j)
            {
                for (int k = j; k < n; k += m)
                {
                    Complex t = w * output[k + (m / 2)];
                    Complex u = output[k];
                    output[k] = u + t;
                    output[k + (m / 2)] = u - t;
                }

                w *= wm;
            }
        }
    }

    private static int BitReverse(int x, int order)
    {
        int n = 0;
        for (int i = 0; i < order; ++i)
        {
            n <<= 1;
            n |= x & 1;
            x >>= 1;
        }

        return n;
    }
}
