// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Harness;

/// <summary>
/// The vectors' tolerance (docs/porting-playbook.md §7.3): an output passes when
/// |a − b| ≤ 1e-10 × max(|a|, |b|, scale), with the output's own scale.
/// </summary>
public static class Tolerance
{
    /// <summary>The relative tolerance every output uses unless it carries a recorded override.</summary>
    public const double Relative = 1e-10;

    /// <summary>Whether two finite reals agree within the tolerance.</summary>
    /// <param name="expected">QuantLib's value.</param>
    /// <param name="actual">nanoql's value.</param>
    /// <param name="scale">The output's scale: its natural size for an amount, otherwise 1.0.</param>
    /// <param name="relative">The relative tolerance, <see cref="Relative"/> unless overridden.</param>
    /// <returns><see langword="true"/> when the difference is within the tolerance.</returns>
    public static bool Within(double expected, double actual, double scale = 1.0, double relative = Relative)
    {
        double size = Math.Max(Math.Max(Math.Abs(expected), Math.Abs(actual)), scale);
        return Math.Abs(expected - actual) <= relative * size;
    }
}
