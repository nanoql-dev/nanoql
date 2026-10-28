// SPDX-FileCopyrightText: Copyright (C) 2009 Dimitri Reiswich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/kernelinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Kernel interpolation between discrete points (Hakala and Wystup, <i>Foreign Exchange Risk</i>, p. 256): the value
/// at x is Σᵢ αᵢ·K(|x − xᵢ|) / Σᵢ K(|x − xᵢ|), the weights α solving y = M·α with Mᵢⱼ = K(|xᵢ − xⱼ|)/Σₖ K(|xᵢ − xₖ|).
/// </summary>
/// <remarks>
/// Immutable: the weights are solved at construction, by <see cref="QRDecompositionFunctions.QRSolve"/>. The kernel
/// is general, though the cited text considers a Gaussian (<see cref="GaussianKernel"/>). The primitive and the
/// derivatives are refused, as QuantLib's.
/// </remarks>
public sealed class KernelInterpolation : Interpolation
{
    private readonly Func<double, double> kernel;
    private readonly Vector alphaVec;

    /// <summary>Initializes a new instance of the <see cref="KernelInterpolation"/> class.</summary>
    /// <param name="x">The x values, sorted.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <param name="kernel">The kernel K, of a nonnegative distance.</param>
    /// <param name="epsilon">The tolerance on each element of |M·α − y|.</param>
    /// <exception cref="ArgumentNullException"><paramref name="kernel"/> is null.</exception>
    /// <exception cref="ArgumentException">Fewer than two points, or the sizes don't match.</exception>
    /// <exception cref="NanoQLException">The solve's residual reaches <paramref name="epsilon"/>.</exception>
    public KernelInterpolation(
        ReadOnlySpan<double> x, ReadOnlySpan<double> y, Func<double, double> kernel, double epsilon = 1.0E-7)
        : base(x, y, 2)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        this.kernel = kernel;
        alphaVec = UpdateAlphaVec(epsilon);
    }

    /// <inheritdoc/>
    protected override double ValueCore(double x)
    {
        ReadOnlySpan<double> xs = X;
        double res = 0.0;

        for (int i = 0; i < xs.Length; ++i)
        {
            res += alphaVec[i] * KernelAbs(x, xs[i]);
        }

        return res / GammaFunc(x);
    }

    /// <inheritdoc/>
    protected override double PrimitiveCore(double x) =>
        throw new NanoQLException("Primitive calculation not implemented for kernel interpolation");

    /// <inheritdoc/>
    protected override double DerivativeCore(double x) =>
        throw new NanoQLException("First derivative calculation not implemented for kernel interpolation");

    /// <inheritdoc/>
    protected override double SecondDerivativeCore(double x) =>
        throw new NanoQLException("Second derivative calculation not implemented for kernel interpolation");

    private double KernelAbs(double x1, double x2) => kernel(Math.Abs(x1 - x2));

    private double GammaFunc(double x)
    {
        ReadOnlySpan<double> xs = X;
        double res = 0.0;

        for (int i = 0; i < xs.Length; ++i)
        {
            res += KernelAbs(x, xs[i]);
        }

        return res;
    }

    // the weights for the fixed pillars and values
    private Vector UpdateAlphaVec(double invPrec)
    {
        ReadOnlySpan<double> xs = X, ys = Y;
        int xSize = xs.Length;
        var m = new Matrix(xSize, xSize);
        var yVec = new Vector(xSize);

        // Write Matrix M
        double tmp;

        for (int rowIt = 0; rowIt < xSize; ++rowIt)
        {
            yVec[rowIt] = ys[rowIt];
            tmp = 1.0 / GammaFunc(xs[rowIt]);

            for (int colIt = 0; colIt < xSize; ++colIt)
            {
                m[rowIt, colIt] = KernelAbs(xs[rowIt], xs[colIt]) * tmp;
            }
        }

        // Solve y=M*\alpha for \alpha
        Vector alpha = QRDecompositionFunctions.QRSolve(m, yVec);

        // check if inversion worked up to a reasonable precision.
        Vector diffVec = ArrayFunctions.Abs((m * alpha) - yVec);

        foreach (double d in diffVec.AsSpan())
        {
            if (!(d < invPrec))
            {
                throw new NanoQLException("Inversion failed in 1d kernel interpolation");
            }
        }

        return alpha;
    }
}
