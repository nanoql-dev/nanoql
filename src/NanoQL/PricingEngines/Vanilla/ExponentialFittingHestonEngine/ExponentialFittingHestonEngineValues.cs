// SPDX-FileCopyrightText: Copyright (C) 2020 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/exponentialfittinghestonengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Buffers.Binary;

namespace NanoQL.PricingEngines;

/// <summary>
/// QuantLib's node table for the exponentially fitted Gauss-Laguerre rule, <c>values4</c>: 147 rows of 129 doubles,
/// each row's moneyness, its 64 nodes and its 64 weights, generated from the pin by
/// <c>tools/tables/exponentialfittinghestonengine.py</c> into the embedded <c>ExponentialFittingHestonEngineValues.bin</c>
/// and read once.
/// </summary>
internal static class ExponentialFittingHestonEngineValues
{
    /// <summary>The table's columns: the moneyness, then the order's nodes and its weights.</summary>
    internal const int Columns = 129;

    private const string ResourceName =
        "NanoQL.PricingEngines.Vanilla.ExponentialFittingHestonEngine.ExponentialFittingHestonEngineValues.bin";

    /// <summary>Gets the table, row by row.</summary>
    internal static double[][] Values { get; } = Load();

    /// <summary>Gets each row's moneyness, the table's first column, in increasing order.</summary>
    internal static double[] Moneyness { get; } = [.. Values.Select(row => row[0])];

    private static double[][] Load()
    {
        using Stream stream = typeof(ExponentialFittingHestonEngineValues).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new NanoQLException($"the embedded table {ResourceName} is missing");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        int rowBytes = Columns * sizeof(double);
        if (bytes.Length % rowBytes != 0)
        {
            throw new NanoQLException($"the embedded table {ResourceName} doesn't hold whole rows of {Columns} doubles");
        }

        var rows = new double[bytes.Length / rowBytes][];
        for (int i = 0; i < rows.Length; ++i)
        {
            rows[i] = new double[Columns];
            for (int j = 0; j < Columns; ++j)
            {
                rows[i][j] = BinaryPrimitives.ReadDoubleLittleEndian(bytes.AsSpan((i * rowBytes) + (j * sizeof(double))));
            }
        }

        return rows;
    }
}
