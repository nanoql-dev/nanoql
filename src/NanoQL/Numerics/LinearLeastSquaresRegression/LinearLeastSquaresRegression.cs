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
/// General linear least-squares regression on given basis functions: QuantLib's interface kept for backward
/// compatibility, which QuantLib asks callers to replace with <see cref="GeneralLinearLeastSquares"/> directly.
/// </summary>
/// <remarks>
/// <see cref="GeneralLinearLeastSquares"/> is immutable and built by its factory, so QuantLib's derived class is a
/// factory here, returning it.
/// </remarks>
public static class LinearLeastSquaresRegression
{
    /// <summary>The regression of <paramref name="y"/> on the basis <paramref name="v"/>.</summary>
    /// <typeparam name="TArgument">The samples' argument type.</typeparam>
    /// <param name="x">The samples' arguments.</param>
    /// <param name="y">The samples' y values, as many as the arguments.</param>
    /// <param name="v">The basis functions.</param>
    /// <returns>The regression.</returns>
    /// <exception cref="ArgumentNullException">An argument or a basis function is null.</exception>
    /// <exception cref="ArgumentException">
    /// The sizes don't match, or there are fewer samples than basis functions.
    /// </exception>
    public static GeneralLinearLeastSquares Create<TArgument>(
        IReadOnlyList<TArgument> x, IReadOnlyList<double> y, IReadOnlyList<Func<TArgument, double>> v) =>
        GeneralLinearLeastSquares.Create(x, y, v);
}
