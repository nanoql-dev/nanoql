// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Bind.Tests.Fixtures;

#pragma warning disable NQL0001 // A user's class over nanoql's experimental interface, as C# lets a user write one.

/// <summary>A user's stable class over one of nanoql's experimental interfaces: experimental in each layer too.</summary>
public sealed class Walk : IRandomWalkDistribution
{
    /// <inheritdoc/>
    public double Next(MersenneTwisterUniformRng engine) => 0.5;

    /// <inheritdoc/>
    public IRandomWalkDistribution Clone() => new Walk();
}
