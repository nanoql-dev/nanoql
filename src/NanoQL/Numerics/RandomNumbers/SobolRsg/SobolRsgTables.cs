// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Richard Gould
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2013 Cheng Li
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause AND LicenseRef-JoeKuo
// Upstream: ql/math/randomnumbers/sobolrsg.hpp ql/math/randomnumbers/sobolrsg.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// The Joe-Kuo numbers are available under a BSD-style license, at http://web.maths.unsw.edu.au/~fkuo/sobol/:
//
// Copyright (c) 2008, Frances Y. Kuo and Stephen Joe
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//
//     * Neither the names of the copyright holders nor the names of the
//       University of New South Wales and the University of Waikato
//       and its contributors may be used to endorse or promote products derived
//       from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ``AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System.Buffers.Binary;

namespace NanoQL.Numerics;

/// <summary>
/// The tables of <c>sobolrsg.cpp</c>'s anonymous namespace: the alternative primitive polynomials, and the
/// coefficients of the free direction integers of each <see cref="SobolRsgDirectionIntegers"/> set but
/// <see cref="SobolRsgDirectionIntegers.Unit"/>.
/// </summary>
/// <remarks>
/// <c>tools/tables/sobolrsg.py</c> generates them from QuantLib's <c>sobolrsg.cpp</c> into embedded resources, one per
/// QuantLib table, each laid out as QuantLib's: a dimension's coefficients ended by 0, a degree's polynomials ended by
/// -1. Each table is read once, at its first use.
/// </remarks>
internal static class SobolRsgTables
{
    /// <summary>QuantLib's <c>maxAltDegree</c>: the number of dimensions in the alternative primitive polynomials.</summary>
    internal const int MaxAltDegree = 52;

    // N_ALT_MAX_DEGREE
    private const int AltDegrees = 8;

    private const string Prefix = "NanoQL.Numerics.RandomNumbers.SobolRsg.";

    private static readonly Lazy<int[][]> AltPrimitivePolynomialsTable = new(() => Polynomials("AltPrimitivePolynomials.bin"));

    // Sobol' Levitan coefficients of the free direction integers as given by Bratley, P., Fox, B.L. (1988)
    private static readonly Lazy<uint[][]> SLinitializers = new(() => Coefficients("SLinitializers.bin", 39));

    // coefficients of the free direction integers as given in "Monte Carlo Methods in Finance", by Peter Jäckel,
    // section 8.3
    private static readonly Lazy<uint[][]> Initializers = new(() => Coefficients("Initializers.bin", 31));

    // Lemieux coefficients of the free direction integers as given in Christiane Lemieux, private communication,
    // September 2004
    private static readonly Lazy<uint[][]> Linitializers = new(() => Coefficients("Linitializers.bin", 359));

    private static readonly Lazy<uint[][]> Kuoinitializers = new(() => Coefficients("Kuoinitializers.bin", 4925));

    private static readonly Lazy<uint[][]> Kuo2initializers = new(() => Coefficients("Kuo2initializers.bin", 3946));

    private static readonly Lazy<uint[][]> Kuo3initializers = new(() => Coefficients("Kuo3initializers.bin", 4586));

    private static readonly Lazy<uint[][]> JoeKuoD5initializers = new(() => Coefficients("JoeKuoD5initializers.bin", 1999));

    private static readonly Lazy<uint[][]> JoeKuoD6initializers = new(() => Coefficients("JoeKuoD6initializers.bin", 21200));

    private static readonly Lazy<uint[][]> JoeKuoD7initializers = new(() => Coefficients("JoeKuoD7initializers.bin", 1899));

    /// <summary>
    /// QuantLib's <c>AltPrimitivePolynomials</c>: the primitive polynomials of degrees 1 to 8 in the order the Kuo
    /// numbers need, each degree's ended by -1.
    /// </summary>
    internal static int[][] AltPrimitivePolynomials => AltPrimitivePolynomialsTable.Value;

    /// <summary>
    /// A set's coefficients of the free direction integers, by dimension from the second: QuantLib's
    /// <c>initializers</c>, <c>SLinitializers</c> and the others, each dimension's ended by 0.
    /// </summary>
    /// <param name="directionIntegers">The set, any but <see cref="SobolRsgDirectionIntegers.Unit"/>.</param>
    /// <returns>The coefficients.</returns>
    internal static uint[][] Coefficients(SobolRsgDirectionIntegers directionIntegers) => directionIntegers switch
    {
        SobolRsgDirectionIntegers.Jaeckel => Initializers.Value,
        SobolRsgDirectionIntegers.SobolLevitan => SLinitializers.Value,
        SobolRsgDirectionIntegers.SobolLevitanLemieux => Linitializers.Value,
        SobolRsgDirectionIntegers.JoeKuoD5 => JoeKuoD5initializers.Value,
        SobolRsgDirectionIntegers.JoeKuoD6 => JoeKuoD6initializers.Value,
        SobolRsgDirectionIntegers.JoeKuoD7 => JoeKuoD7initializers.Value,
        SobolRsgDirectionIntegers.Kuo => Kuoinitializers.Value,
        SobolRsgDirectionIntegers.Kuo2 => Kuo2initializers.Value,
        SobolRsgDirectionIntegers.Kuo3 => Kuo3initializers.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(directionIntegers), directionIntegers, "no tabulated coefficients"),
    };

    private static byte[] Read(string name)
    {
        using Stream stream = typeof(SobolRsgTables).Assembly.GetManifestResourceStream(Prefix + name)
            ?? throw new NanoQLException($"the embedded table {Prefix + name} is missing");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static int[][] Polynomials(string name)
    {
        byte[] bytes = Read(name);
        var degrees = new List<int[]>(AltDegrees);
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

        if (degrees.Count != AltDegrees || current.Count != 0)
        {
            throw new NanoQLException($"the embedded table {Prefix + name} doesn't hold {AltDegrees} degrees");
        }

        return [.. degrees];
    }

    private static uint[][] Coefficients(string name, int dimensions)
    {
        byte[] bytes = Read(name);
        var table = new List<uint[]>(dimensions);
        var current = new List<uint>();
        for (int at = 0; at < bytes.Length; at += sizeof(uint))
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
            current.Add(value);
            if (value == 0U)
            {
                table.Add([.. current]);
                current.Clear();
            }
        }

        if (table.Count != dimensions || current.Count != 0)
        {
            throw new NanoQLException($"the embedded table {Prefix + name} doesn't hold {dimensions} dimensions");
        }

        return [.. table];
    }
}
