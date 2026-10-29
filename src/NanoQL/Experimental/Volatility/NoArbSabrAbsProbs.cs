// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/noarbsabrabsprobs.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Buffers.Binary;

namespace NanoQL.Experimental;

/// <summary>
/// QuantLib's absorption table for the no-arbitrage SABR model, <c>sabrabsprob</c>: 1,209,600 counts of paths absorbed
/// at zero out of 2,500,000 simulated on Doust's grid of expiries, initial volatilities, correlations, volatilities of
/// volatility and betas, generated from the pin by <c>tools/tables/noarbsabrabsprobs.py</c> into the embedded
/// <c>NoArbSabrAbsProbs.bin</c> and read once (the S-xeq plan's XED4).
/// </summary>
internal static class NoArbSabrAbsProbs
{
    /// <summary>The table's size, QuantLib's declared one.</summary>
    internal const int Size = 1209600;

    private const string ResourceName = "NanoQL.Experimental.Volatility.NoArbSabrAbsProbs.bin";

    /// <summary>Gets the counts, in QuantLib's order.</summary>
    internal static int[] Values { get; } = Load();

    private static int[] Load()
    {
        using Stream stream = typeof(NoArbSabrAbsProbs).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new NanoQLException($"the embedded table {ResourceName} is missing");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        if (bytes.Length != Size * sizeof(int))
        {
            throw new NanoQLException($"the embedded table {ResourceName} doesn't hold {Size} integers");
        }

        var values = new int[Size];
        for (int i = 0; i < Size; ++i)
        {
            values[i] = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i * sizeof(int)));
        }

        return values;
    }
}
