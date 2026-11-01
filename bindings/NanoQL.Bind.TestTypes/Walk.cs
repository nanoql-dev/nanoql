// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Bind.TestTypes;

#pragma warning disable NQL0001 // A user's class over nanoql's experimental interface, as C# lets a user write one.

/// <summary>A user's stable class over one of nanoql's experimental interfaces: experimental in each layer too.</summary>
public sealed class Walk : IRandomWalkDistribution
{
    /// <inheritdoc/>
    public double Next(MersenneTwisterUniformRng engine) => 0.5;

    /// <inheritdoc/>
    public IRandomWalkDistribution Clone() => new Walk();

    /// <summary>A step's count a year: a default that is one of nanoql's stable enums, read when the opt-in defines it.</summary>
    /// <param name="frequency">The frequency.</param>
    /// <returns>Its count a year.</returns>
    public static int PerYear(Frequency frequency = Frequency.Annual) => (int)frequency;
}
