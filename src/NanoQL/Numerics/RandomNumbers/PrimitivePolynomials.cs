// SPDX-FileCopyrightText: Copyright (C) 2002 Peter Jäckel "Monte Carlo Methods in Finance".
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause AND LicenseRef-Jaeckel
// Upstream: ql/math/randomnumbers/primitivepolynomials.hpp ql/math/randomnumbers/primitivepolynomials.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// this file is a slightly edited version of
// PrimitivePolynomialsModuloTwoUpToDegree27.h
// © 2002 "Monte Carlo Methods in Finance"
// as provided ready for compilation in the directory
// "PrimitivePolynomialsModuloTwo" on the CD accompanying the book
// "Monte Carlo Methods in Finance" by Peter Jäckel.
//
// ===========================================================================
// NOTE: The following copyright notice applies to the original code,
//
// Copyright (C) 2002 Peter Jäckel "Monte Carlo Methods in Finance".
// All rights reserved.
//
// Permission to use, copy, modify, and distribute this software is freely
// granted, provided that this notice is preserved.
// ===========================================================================
//
// This file contains 21200 polinomials, that is all primitive polynomials
// modulo two up to degree 18. Another version does contain
// 8129334 polinomials, that is all primitive polynomials
// modulo two up to degree 27. Both are slightly edited versions of
// PrimitivePolynomialsModuloTwoUpToDegree27.c
//
// © 2002 "Monte Carlo Methods in Finance"
//
//
// =========================================================================
// Copyright (C) 2002 Peter Jäckel "Monte Carlo Methods in Finance".
// All rights reserved.
//
// Permission to use, copy, modify, and distribute this software is freely
// granted, provided that this notice is preserved.
// =========================================================================

using System.Buffers.Binary;

namespace NanoQL.Numerics;

/// <summary>
/// The primitive polynomials modulo two, for Sobol sequences of higher dimensions: QuantLib's
/// <c>PrimitivePolynomials</c> array, all 21,200 polynomials up to degree 18.
/// </summary>
/// <remarks>
/// <para>
/// The coefficients of each primitive polynomial are the bits of the given integer. The leading and trailing
/// coefficients, which are 1 for all of the polynomials, have been omitted. For example, the polynomial
/// x⁴ + x² + 1 is encoded as 2 in the array of polynomials of degree 4, because the binary sequence of coefficients
/// 10101 becomes 0101 after stripping off the top bit, and this is converted to 010 by right-shifting and losing the
/// rightmost bit. Similarly, x⁵ + x⁴ + x² + x + 1 is encoded as 13, (1)1101(1), in the array for degree 5.
/// </para>
/// <para>
/// The table is generated from QuantLib's <c>primitivepolynomials.cpp</c> by <c>tools/tables/primitivepolynomials.py</c>
/// into the embedded resource <c>PrimitivePolynomials.bin</c>, which this class reads once, at first use.
/// </para>
/// </remarks>
internal static class PrimitivePolynomials
{
    /// <summary>QuantLib's <c>PPMT_MAX_DIM</c>, at its default: the number of polynomials, 21,200.</summary>
    internal const int PPMT_MAX_DIM = 21200;

    /// <summary>QuantLib's <c>N_MAX_DEGREE</c>: the highest degree the table holds.</summary>
    internal const int N_MAX_DEGREE = 18;

    private const string ResourceName = "NanoQL.Numerics.RandomNumbers.PrimitivePolynomials.bin";

    private static readonly int[][] Table = Load();

    /// <summary>
    /// QuantLib's <c>PrimitivePolynomials[i][j]</c>: the j-th (counting from zero) primitive polynomial of degree
    /// i + 1. Each degree's polynomials are terminated with an entry of -1; reading beyond it throws.
    /// </summary>
    /// <param name="i">The degree's index, from 0 to <see cref="N_MAX_DEGREE"/> − 1.</param>
    /// <param name="j">The polynomial's index within its degree.</param>
    /// <returns>The polynomial's encoding (QuantLib's <c>long</c>, 32 bits under MSVC), or -1 past the degree's last.</returns>
    internal static int Get(int i, int j) => Table[i][j];

    private static int[][] Load()
    {
        using Stream stream = typeof(PrimitivePolynomials).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new NanoQLException($"the embedded table {ResourceName} is missing");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        var degrees = new List<int[]>(N_MAX_DEGREE);
        var current = new List<int>();
        for (int at = 0; at < bytes.Length; at += sizeof(int))
        {
            int value = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
            current.Add(value);
            if (value == -1)
            {
                degrees.Add([.. current]);
                current.Clear();
            }
        }

        if (degrees.Count != N_MAX_DEGREE || current.Count != 0)
        {
            throw new NanoQLException($"the embedded table {ResourceName} doesn't hold {N_MAX_DEGREE} degrees");
        }

        return [.. degrees];
    }
}
