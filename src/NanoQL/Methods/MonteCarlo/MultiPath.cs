// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/multipath.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>Correlated multiple asset paths: <c>multiPath[j]</c> is the path followed by the j-th asset.</summary>
/// <remarks>
/// A <see cref="MultiPathGenerator{TGsg}"/>'s sample is a view of its own paths, valid until its next draw (S2R2); a
/// multi-path built from paths copies them, as QuantLib's copies its vector.
/// </remarks>
public sealed class MultiPath
{
    private readonly MonteCarloPath[] multiPath;

    /// <summary>Initializes a new instance of the <see cref="MultiPath"/> class, with no asset.</summary>
    public MultiPath()
    {
        multiPath = [];
    }

    /// <summary>Initializes a new instance of the <see cref="MultiPath"/> class: paths of zeros over a time grid.</summary>
    /// <param name="nAsset">The number of assets, positive.</param>
    /// <param name="timeGrid">The time grid.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nAsset"/> isn't positive.</exception>
    public MultiPath(int nAsset, TimeGrid timeGrid)
    {
        ArgumentNullException.ThrowIfNull(timeGrid);
        if (!(nAsset > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(nAsset), nAsset, "number of asset must be positive");
        }

        multiPath = new MonteCarloPath[nAsset];
        for (int j = 0; j < nAsset; j++)
        {
            multiPath[j] = MonteCarloPath.ForGenerator(timeGrid);
        }
    }

    /// <summary>Initializes a new instance of the <see cref="MultiPath"/> class from paths, which it copies.</summary>
    /// <param name="multiPath">The assets' paths.</param>
    public MultiPath(ImmutableArray<MonteCarloPath> multiPath)
    {
        this.multiPath = multiPath.IsDefault ? [] : new MonteCarloPath[multiPath.Length];
        for (int j = 0; j < this.multiPath.Length; j++)
        {
            MonteCarloPath path = multiPath[j] ?? throw new ArgumentException("null path given", nameof(multiPath));
            MonteCarloPath copy = MonteCarloPath.ForGenerator(path.TimeGrid);
            for (int i = 0; i < path.Length; i++)
            {
                copy.Buffer[i] = path[i];
            }

            this.multiPath[j] = copy;
        }
    }

    /// <summary>Gets the number of assets.</summary>
    public int AssetNumber => multiPath.Length;

    /// <summary>The j-th asset's path.</summary>
    /// <param name="j">The asset's index.</param>
    /// <returns>The path.</returns>
    public MonteCarloPath this[int j] => multiPath[j];

    /// <summary>The number of times in each path.</summary>
    /// <returns>The paths' length.</returns>
    /// <exception cref="NanoQLException">
    /// The multi-path has no asset, where QuantLib reads the first of no paths (a divergence: nanoql refuses it).
    /// </exception>
    public int PathSize()
    {
        // QuantLib's multiPath_[0] of an empty vector is undefined.
        if (multiPath.Length == 0)
        {
            throw new NanoQLException("no path size for empty multi-path");
        }

        return multiPath[0].Length;
    }

    /// <summary>The j-th asset's path, its index checked: QuantLib's <c>at</c>.</summary>
    /// <param name="j">The asset's index.</param>
    /// <returns>The path.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="j"/> is outside the assets.</exception>
    public MonteCarloPath At(int j)
    {
        // QuantLib's std::vector::at throws std::out_of_range.
        if (!(j >= 0 && j < multiPath.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(j), $"index ({j}) must be less than {multiPath.Length}: multi-path access out of range");
        }

        return multiPath[j];
    }

    // The paths of zeros a multi-path generator owns and writes through each path's buffer.
    internal static MultiPath ForGenerator(int nAsset, TimeGrid timeGrid) => new(nAsset, timeGrid);
}
