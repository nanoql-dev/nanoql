// SPDX-FileCopyrightText: Copyright (C) 2009 Dirk Eddelbuettel
// SPDX-FileCopyrightText: Copyright (C) 2006, 2009, 2010 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2010 Kakhkhor Abdijalilov
// SPDX-FileCopyrightText: Copyright (C) 2010 Slava Mazur
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/linearleastsquaresregression.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Linear regression, yᵢ = a₀ + a₁·xᵢ,₀ + … + aₙ·xᵢ,ₙ₋₁ + ε, by general linear least squares: QuantLib's
/// <c>LinearRegression</c>, a <see cref="GeneralLinearLeastSquares"/> on the linear basis.
/// </summary>
/// <remarks>
/// <see cref="GeneralLinearLeastSquares"/> is immutable and built by its factory, so QuantLib's derived class is a
/// factory here, returning it. Its basis is QuantLib's <c>details::LinearFcts</c>: the intercept's constant where the
/// intercept isn't 0, then x itself, or each of a vector's elements, by position. QuantLib reads the first sample's
/// size for that basis, undefined on no samples; no samples are refused.
/// </remarks>
public static class LinearRegression
{
    /// <summary>The regression on one variable, y = a₀·c + a₁·x, c the intercept's constant.</summary>
    /// <param name="x">The samples' x values.</param>
    /// <param name="y">The samples' y values, as many as the x values.</param>
    /// <param name="intercept">The constant basis function's value; 0 for a regression without one.</param>
    /// <returns>The regression.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// The sizes don't match, or there are fewer samples than basis functions.
    /// </exception>
    public static GeneralLinearLeastSquares Create(
        IReadOnlyList<double> x, IReadOnlyList<double> y, double intercept = 1.0)
    {
        ArgumentNullException.ThrowIfNull(x);
        var v = new List<Func<double, double>>();
        if (intercept != 0.0)
        {
            v.Add(_ => intercept);
        }

        v.Add(xi => xi);
        return GeneralLinearLeastSquares.Create(x, y, v);
    }

    /// <summary>
    /// The regression on several variables, y = a₀·c + a₁·x₀ + … + aₙ·xₙ₋₁, c the intercept's constant, n the first
    /// sample's size.
    /// </summary>
    /// <param name="x">The samples' x vectors.</param>
    /// <param name="y">The samples' y values, as many as the x vectors.</param>
    /// <param name="intercept">The constant basis function's value; 0 for a regression without one.</param>
    /// <returns>The regression.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// No samples, the sizes don't match, or there are fewer samples than basis functions.
    /// </exception>
    public static GeneralLinearLeastSquares Create(
        IReadOnlyList<Vector> x, IReadOnlyList<double> y, double intercept = 1.0)
    {
        ArgumentNullException.ThrowIfNull(x);
        if (x.Count == 0)
        {
            throw new ArgumentException("sample set is too small", nameof(x));
        }

        var v = new List<Func<Vector, double>>();
        if (intercept != 0.0)
        {
            v.Add(_ => intercept);
        }

        int m = x[0].Size;
        for (int i = 0; i < m; ++i)
        {
            int index = i;
            v.Add(xi => xi[index]);
        }

        return GeneralLinearLeastSquares.Create(x, y, v);
    }

    /// <summary>The regression on the given basis functions.</summary>
    /// <typeparam name="TX">The samples' argument type.</typeparam>
    /// <param name="x">The samples' arguments.</param>
    /// <param name="y">The samples' y values, as many as the arguments.</param>
    /// <param name="v">The basis functions.</param>
    /// <returns>The regression.</returns>
    /// <exception cref="ArgumentNullException">An argument or a basis function is null.</exception>
    /// <exception cref="ArgumentException">
    /// The sizes don't match, or there are fewer samples than basis functions.
    /// </exception>
    public static GeneralLinearLeastSquares Create<TX>(
        IReadOnlyList<TX> x, IReadOnlyList<double> y, IReadOnlyList<Func<TX, double>> v) =>
        GeneralLinearLeastSquares.Create(x, y, v);
}
