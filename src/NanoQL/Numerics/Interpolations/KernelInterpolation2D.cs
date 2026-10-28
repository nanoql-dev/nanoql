// SPDX-FileCopyrightText: Copyright (C) 2009 Dimitri Reiswich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/kernelinterpolation2d.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Two-dimensional kernel interpolation (Hakala and Wystup, <i>Foreign Exchange Risk</i>, p. 256): the value at X =
/// (x, y) is Σₖ αₖ·K(‖X − Xₖ‖) / Σₖ K(‖X − Xₖ‖) over the grid's points Xₖ, the weights α solving z = M·α.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: the weights are solved at construction, by <see cref="QRDecompositionFunctions.QRSolve"/>, and a
/// residual reaching 1e-10 is refused. The z values are a matrix with a row per x value and a column per y value
/// (zᵢⱼ at (xᵢ, yⱼ)), QuantLib's layout for this interpolation, the transpose of other two-dimensional ones'. The grid's
/// points run through x within each y.
/// </para>
/// <para>
/// QuantLib's implementation can set another tolerance for the residual, which its interpolation doesn't expose, so
/// 1e-10 is fixed here too.
/// </para>
/// </remarks>
public sealed class KernelInterpolation2D : Interpolation2D
{
    private const double InvPrec = 1.0e-10;

    private readonly Func<double, double> kernel;
    private readonly Vector alphaVec;

    /// <summary>Initializes a new instance of the <see cref="KernelInterpolation2D"/> class.</summary>
    /// <param name="x">The x values, sorted, at least two.</param>
    /// <param name="y">The y values, sorted, at least two.</param>
    /// <param name="zData">The z values, a row per x value and a column per y value.</param>
    /// <param name="kernel">The kernel K, of a nonnegative distance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="zData"/> or <paramref name="kernel"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Fewer than two x or y values, or the z matrix's rows or columns don't match them.
    /// </exception>
    /// <exception cref="NanoQLException">The solve's residual reaches 1e-10.</exception>
    public KernelInterpolation2D(
        ReadOnlySpan<double> x, ReadOnlySpan<double> y, Matrix zData, Func<double, double> kernel)
        : base(x, y, zData)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        if (!(zData.Rows == x.Length))
        {
            throw new ArgumentException("Z value matrix has wrong number of rows", nameof(zData));
        }

        if (!(zData.Columns == y.Length))
        {
            throw new ArgumentException("Z value matrix has wrong number of columns", nameof(zData));
        }

        this.kernel = kernel;
        alphaVec = UpdateAlphaVec();
    }

    /// <inheritdoc/>
    protected override double ValueCore(double x, double y)
    {
        ReadOnlySpan<double> xs = X, ys = Y;
        double res = 0.0;

        var xv = new Vector(2);
        var xn = new Vector(2);
        xv[0] = x;
        xv[1] = y;

        int cnt = 0;

        for (int j = 0; j < ys.Length; ++j)
        {
            for (int i = 0; i < xs.Length; ++i)
            {
                xn[0] = xs[i];
                xn[1] = ys[j];
                res += alphaVec[cnt] * KernelAbs(xv, xn);
                cnt++;
            }
        }

        return res / GammaFunc(xv);
    }

    // K(‖X − Y‖)
    private double KernelAbs(Vector x, Vector y) => kernel(ArrayFunctions.Norm2(x - y));

    private double GammaFunc(Vector x)
    {
        ReadOnlySpan<double> xs = X, ys = Y;
        double res = 0.0;
        var xn = new Vector(x.Size);

        for (int j = 0; j < ys.Length; ++j)
        {
            for (int i = 0; i < xs.Length; ++i)
            {
                xn[0] = xs[i];
                xn[1] = ys[j];
                res += KernelAbs(x, xn);
            }
        }

        return res;
    }

    // the weights for the fixed pillars and values
    private Vector UpdateAlphaVec()
    {
        ReadOnlySpan<double> xs = X, ys = Y;
        int xySize = xs.Length * ys.Length;
        var m = new Matrix(xySize, xySize);
        var yVec = new Vector(xySize);

        var xk = new Vector(2);
        var xn = new Vector(2);

        int rowCnt = 0, colCnt;
        double tmpVar;

        // write y-vector and M-Matrix
        for (int j = 0; j < ys.Length; ++j)
        {
            for (int i = 0; i < xs.Length; ++i)
            {
                yVec[rowCnt] = ZRow(i)[j];

                // calculate X_k
                xk[0] = xs[i];
                xk[1] = ys[j];

                tmpVar = 1 / GammaFunc(xk);
                colCnt = 0;

                for (int jM = 0; jM < ys.Length; ++jM)
                {
                    for (int iM = 0; iM < xs.Length; ++iM)
                    {
                        xn[0] = xs[iM];
                        xn[1] = ys[jM];
                        m[rowCnt, colCnt] = KernelAbs(xk, xn) * tmpVar;
                        colCnt++;
                    }
                }

                rowCnt++;
            }
        }

        Vector alpha = QRDecompositionFunctions.QRSolve(m, yVec);

        // check if inversion worked up to a reasonable precision.
        Vector diffVec = ArrayFunctions.Abs((m * alpha) - yVec);
        foreach (double d in diffVec.AsSpan())
        {
            if (!(d < InvPrec))
            {
                throw new NanoQLException("inversion failed in 2d kernel interpolation");
            }
        }

        return alpha;
    }
}
