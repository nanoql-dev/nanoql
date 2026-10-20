// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianquadratures.hpp ql/math/integrals/gaussianquadratures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The tabulated Gauss-Legendre quadratures of orders 6, 7, 12 and 20 on [−1, 1], their abscissas and weights from
/// Abramowitz and Stegun to 15 decimals.
/// </summary>
/// <remarks>Immutable: <see cref="Order(int)"/> returns a copy of another order, where QuantLib's changes it.</remarks>
public sealed class TabulatedGaussLegendre
{
    private readonly int order_;

    /// <summary>Initializes a new instance of the <see cref="TabulatedGaussLegendre"/> class.</summary>
    /// <param name="n">The order: 6, 7, 12 or 20.</param>
    /// <exception cref="ArgumentException">The order isn't tabulated.</exception>
    public TabulatedGaussLegendre(int n = 20)
    {
        CheckOrder(n, nameof(n));
        order_ = n;
    }

    // Abscissas and Weights from Abramowitz and Stegun

    /* order 6 */
    private static ReadOnlySpan<double> X6 => [0.238619186083197, 0.661209386466265, 0.932469514203152];

    private static ReadOnlySpan<double> W6 => [0.467913934572691, 0.360761573048139, 0.171324492379170];

    /* order 7 */
    private static ReadOnlySpan<double> X7 =>
        [0.000000000000000, 0.405845151377397, 0.741531185599394, 0.949107912342759];

    private static ReadOnlySpan<double> W7 =>
        [0.417959183673469, 0.381830050505119, 0.279705391489277, 0.129484966168870];

    /* order 12 */
    private static ReadOnlySpan<double> X12 =>
    [
        0.125233408511469, 0.367831498998180, 0.587317954286617,
        0.769902674194305, 0.904117256370475, 0.981560634246719,
    ];

    private static ReadOnlySpan<double> W12 =>
    [
        0.249147045813403, 0.233492536538355, 0.203167426723066,
        0.160078328543346, 0.106939325995318, 0.047175336386512,
    ];

    /* order 20 */
    private static ReadOnlySpan<double> X20 =>
    [
        0.076526521133497, 0.227785851141645, 0.373706088715420, 0.510867001950827, 0.636053680726515,
        0.746331906460151, 0.839116971822219, 0.912234428251326, 0.963971927277914, 0.993128599185095,
    ];

    private static ReadOnlySpan<double> W20 =>
    [
        0.152753387130726, 0.149172986472604, 0.142096109318382, 0.131688638449177, 0.118194531961518,
        0.101930119817240, 0.083276741576704, 0.062672048334109, 0.040601429800387, 0.017614007139152,
    ];

    /// <summary>The order.</summary>
    /// <returns>6, 7, 12 or 20.</returns>
    public int Order() => order_;

    /// <summary>A copy of the quadrature of another order, QuantLib's <c>order(Size)</c>.</summary>
    /// <param name="order">The order: 6, 7, 12 or 20.</param>
    /// <returns>The quadrature of that order.</returns>
    /// <exception cref="ArgumentException">The order isn't tabulated.</exception>
    public TabulatedGaussLegendre Order(int order)
    {
        CheckOrder(order, nameof(order));
        return new TabulatedGaussLegendre(order);
    }

    /// <summary>
    /// The weighted sum of <paramref name="f"/> at the abscissas: the middle one first for an odd order, then each
    /// positive abscissa and its negative.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <returns>Σ wᵢ·f(xᵢ).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    public double Evaluate(Func<double, double> f)
    {
        ArgumentNullException.ThrowIfNull(f);
        ReadOnlySpan<double> x_ = XTable(order_);
        ReadOnlySpan<double> w_ = WTable(order_);
        int n_ = x_.Length;

        int startIdx;
        double val;

        int isOrderOdd = order_ & 1;

        if (isOrderOdd != 0)
        {
            val = w_[0] * f(x_[0]);
            startIdx = 1;
        }
        else
        {
            val = 0.0;
            startIdx = 0;
        }

        for (int i = startIdx; i < n_; ++i)
        {
            val += w_[i] * f(x_[i]);
            val += w_[i] * f(-x_[i]);
        }

        return val;
    }

    private static void CheckOrder(int order, string paramName)
    {
        if (order is not (6 or 7 or 12 or 20))
        {
            throw new ArgumentException($"order {order} not supported", paramName);
        }
    }

    private static ReadOnlySpan<double> XTable(int order) => order switch
    {
        6 => X6,
        7 => X7,
        12 => X12,
        _ => X20,
    };

    private static ReadOnlySpan<double> WTable(int order) => order switch
    {
        6 => W6,
        7 => W7,
        12 => W12,
        _ => W20,
    };
}
