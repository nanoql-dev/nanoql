// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/latticerules.hpp ql/math/randomnumbers/latticerules.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Buffers.Binary;
using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// The generating vectors of the rank-1 lattice rules of Cools, Kuo and Nuyens (SIAM J. Sci. Comp., 2006), for
/// <see cref="LatticeRsg"/>: four rules, each of 3,600 dimensions, for 2^10 to 2^20 points.
/// </summary>
/// <remarks>
/// The tables are generated from QuantLib's <c>latticerules.cpp</c> by <c>tools/tables/latticerules.py</c> into the
/// embedded resource <c>LatticeRules.bin</c>, which this class reads once, at first use.
/// </remarks>
public static class LatticeRule
{
    /// <summary>The dimensions each rule's generating vector holds, QuantLib's <c>ruleLength</c>.</summary>
    public const int RuleLength = 3600;

    private const string ResourceName = "NanoQL.Numerics.RandomNumbers.LatticeRules.LatticeRules.bin";

    private static readonly ImmutableArray<double>[] Rules = Load();

    /// <summary>The generating vector of a rule, for a number of points.</summary>
    /// <remarks>
    /// QuantLib checks the number of points against 2.9^20, about 1.8e9, where its message and its tables say 2^20, so
    /// it accepts up to about 1.8e9 points; nanoql checks 2^20 (a documented divergence in
    /// <c>Divergences/LatticeRsgTests.cs</c>). As in QuantLib, the number of points needn't be a power of 2.
    /// </remarks>
    /// <param name="name">The rule.</param>
    /// <param name="n">The number of points the rule will draw, in [2^10, 2^20].</param>
    /// <returns>The rule's generating vector, its 3,600 integers as reals.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="n"/> is outside [2^10, 2^20], or <paramref name="name"/> isn't a rule.
    /// </exception>
    public static ImmutableArray<double> GetRule(LatticeRuleType name, int n)
    {
        if (!(n >= 1024 && n <= (1 << 20)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(n), n, "N must be between 2 to 10 and 2 to the 20 for these lattice rules ");
        }

        return name switch
        {
            LatticeRuleType.A => Rules[0],
            LatticeRuleType.B => Rules[1],
            LatticeRuleType.C => Rules[2],
            LatticeRuleType.D => Rules[3],
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown lattice rule requested"),
        };
    }

    private static ImmutableArray<double>[] Load()
    {
        using Stream stream = typeof(LatticeRule).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new NanoQLException($"the embedded table {ResourceName} is missing");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        if (bytes.Length != 4 * RuleLength * sizeof(int))
        {
            throw new NanoQLException($"the embedded table {ResourceName} doesn't hold four rules of {RuleLength}");
        }

        var rules = new ImmutableArray<double>[4];
        for (int r = 0; r < 4; r++)
        {
            var z = new double[RuleLength];
            for (int i = 0; i < RuleLength; i++)
            {
                z[i] = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(((r * RuleLength) + i) * sizeof(int)));
            }

            rules[r] = ImmutableArray.Create(z);
        }

        return rules;
    }
}
