// SPDX-FileCopyrightText: Copyright (C) 2010 Liquidnet Holdings, Inc.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/autocovariance.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;

namespace NanoQL.Numerics;

/// <summary>
/// Convolutions, autocovariances and autocorrelations of a sequence, by the fast Fourier transform: the data's
/// transform, its squared modulus, and that transformed again.
/// </summary>
/// <remarks>
/// Each function writes the lags 0 to <c>maxLag</c> into the first <c>maxLag + 1</c> places of a caller's span. The
/// overloads taking <c>reuse</c> accept data that isn't centred: they remove its mean, return it, and write the centred
/// data back into the caller's span where <c>reuse</c> is <see langword="true"/>.
/// </remarks>
public static class AutocovarianceFunctions
{
    /// <summary>The convolutions x₀xₙ + x₁xₙ₊₁ + x₂xₙ₊₂ + … for n = 0 to <paramref name="maxLag"/>.</summary>
    /// <param name="data">The sequence.</param>
    /// <param name="output">The convolutions, at least <paramref name="maxLag"/> + 1 places.</param>
    /// <param name="maxLag">The last lag.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLag"/> is negative.</exception>
    /// <exception cref="ArgumentException">The output is shorter than <paramref name="maxLag"/> + 1.</exception>
    /// <exception cref="NanoQLException"><paramref name="maxLag"/> isn't less than the data's size.</exception>
    public static void Convolutions(ReadOnlySpan<double> data, Span<double> output, int maxLag)
    {
        CheckOutput(output, maxLag);
        int nData = data.Length;
        if (!(maxLag < nData))
        {
            throw new NanoQLException("maxLag must be less than data size");
        }

        Complex[] ft = DoubleFt(data);
        double w = 1.0 / ft.Length;
        for (int k = 0; k <= maxLag; ++k)
        {
            output[k] = ft[k].Real * w;
        }
    }

    /// <summary>The unbiased autocovariances of centred data, for lags 0 to <paramref name="maxLag"/>.</summary>
    /// <param name="data">The sequence, centred (of zero mean).</param>
    /// <param name="output">The autocovariances, at least <paramref name="maxLag"/> + 1 places.</param>
    /// <param name="maxLag">The last lag.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLag"/> is negative.</exception>
    /// <exception cref="ArgumentException">The output is shorter than <paramref name="maxLag"/> + 1.</exception>
    /// <exception cref="NanoQLException"><paramref name="maxLag"/> isn't less than the data's size.</exception>
    public static void Autocovariances(ReadOnlySpan<double> data, Span<double> output, int maxLag)
    {
        CheckOutput(output, maxLag);
        int nData = data.Length;
        if (!(maxLag < nData))
        {
            throw new NanoQLException("number of covariances must be less than data size");
        }

        Complex[] ft = DoubleFt(data);
        double w1 = 1.0 / ft.Length, w2 = nData;
        for (int k = 0; k <= maxLag; ++k, w2 -= 1.0)
        {
            output[k] = ft[k].Real * w1 / w2;
        }
    }

    /// <summary>
    /// The unbiased autocovariances for lags 0 to <paramref name="maxLag"/> of data that needn't be centred: the mean
    /// removed and returned.
    /// </summary>
    /// <param name="data">The sequence; the centred sequence is written back into it where
    /// <paramref name="reuse"/> is <see langword="true"/>.</param>
    /// <param name="output">The autocovariances, at least <paramref name="maxLag"/> + 1 places.</param>
    /// <param name="maxLag">The last lag.</param>
    /// <param name="reuse">Whether the centred sequence is written back into <paramref name="data"/>.</param>
    /// <returns>The data's mean.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLag"/> is negative.</exception>
    /// <exception cref="ArgumentException">The output is shorter than <paramref name="maxLag"/> + 1.</exception>
    /// <exception cref="NanoQLException"><paramref name="maxLag"/> isn't less than the data's size.</exception>
    public static double Autocovariances(Span<double> data, Span<double> output, int maxLag, bool reuse)
    {
        double mean;
        if (reuse)
        {
            mean = RemoveMean(data, data);
            Autocovariances(data, output, maxLag);
        }
        else
        {
            var tmp = new double[data.Length];
            mean = RemoveMean(data, tmp);
            Autocovariances(tmp, output, maxLag);
        }

        return mean;
    }

    /// <summary>
    /// The unbiased autocorrelations of centred data for lags 0 to <paramref name="maxLag"/>, the first place holding
    /// the unbiased sample variance.
    /// </summary>
    /// <param name="data">The sequence, centred (of zero mean).</param>
    /// <param name="output">The variance and the autocorrelations, at least <paramref name="maxLag"/> + 1 places.</param>
    /// <param name="maxLag">The last lag.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLag"/> is negative.</exception>
    /// <exception cref="ArgumentException">The output is shorter than <paramref name="maxLag"/> + 1.</exception>
    /// <exception cref="NanoQLException"><paramref name="maxLag"/> isn't less than the data's size.</exception>
    public static void Autocorrelations(ReadOnlySpan<double> data, Span<double> output, int maxLag)
    {
        CheckOutput(output, maxLag);
        int nData = data.Length;
        if (!(maxLag < nData))
        {
            throw new NanoQLException("number of correlations must be less than data size");
        }

        Complex[] ft = DoubleFt(data);
        double w1 = 1.0 / ft.Length, w2 = nData;
        double variance = ft[0].Real * w1 / w2;
        output[0] = variance * w2 / (w2 - 1.0);
        w2 -= 1.0;
        for (int k = 1; k <= maxLag; ++k, w2 -= 1.0)
        {
            output[k] = ft[k].Real * w1 / (variance * w2);
        }
    }

    /// <summary>
    /// The unbiased autocorrelations for lags 0 to <paramref name="maxLag"/> of data that needn't be centred, the
    /// first place holding the unbiased sample variance: the mean removed and returned.
    /// </summary>
    /// <param name="data">The sequence; the centred sequence is written back into it where
    /// <paramref name="reuse"/> is <see langword="true"/>.</param>
    /// <param name="output">The variance and the autocorrelations, at least <paramref name="maxLag"/> + 1 places.</param>
    /// <param name="maxLag">The last lag.</param>
    /// <param name="reuse">Whether the centred sequence is written back into <paramref name="data"/>.</param>
    /// <returns>The data's mean.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLag"/> is negative.</exception>
    /// <exception cref="ArgumentException">The output is shorter than <paramref name="maxLag"/> + 1.</exception>
    /// <exception cref="NanoQLException"><paramref name="maxLag"/> isn't less than the data's size.</exception>
    public static double Autocorrelations(Span<double> data, Span<double> output, int maxLag, bool reuse)
    {
        double mean;
        if (reuse)
        {
            mean = RemoveMean(data, data);
            Autocorrelations(data, output, maxLag);
        }
        else
        {
            var tmp = new double[data.Length];
            mean = RemoveMean(data, tmp);
            Autocorrelations(tmp, output, maxLag);
        }

        return mean;
    }

    private static void CheckOutput(Span<double> output, int maxLag)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLag);
        if (output.Length < maxLag + 1)
        {
            throw new ArgumentException($"the output holds {output.Length} places, fewer than the {maxLag + 1} lags", nameof(output));
        }
    }

    // input -> FFT -> norm -> FFT -> out
    private static Complex[] DoubleFt(ReadOnlySpan<double> data)
    {
        int order = FastFourierTransform.MinOrder(data.Length) + 1;
        var fft = new FastFourierTransform(order);
        var ft = new Complex[fft.OutputSize];
        fft.Transform(data, ft);
        var tmp = new double[ft.Length];
        for (int i = 0; i < ft.Length; ++i)
        {
            tmp[i] = (ft[i].Real * ft[i].Real) + (ft[i].Imaginary * ft[i].Imaginary);
            ft[i] = Complex.Zero;
        }

        fft.Transform(tmp, ft);
        return ft;
    }

    // calculates and subtracts the mean from the input data; returns the mean
    private static double RemoveMean(ReadOnlySpan<double> data, Span<double> output)
    {
        double mean = 0.0;
        int n = 1;
        foreach (double x in data)
        {
            mean = ((mean * (n - 1)) + x) / n;
            ++n;
        }

        for (int i = 0; i < data.Length; ++i)
        {
            output[i] = data[i] - mean;
        }

        return mean;
    }
}
