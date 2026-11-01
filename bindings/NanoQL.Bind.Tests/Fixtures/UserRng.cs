// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>A user's generator, unmarked itself: the mark reaches it through nanoql's interface, so it crosses as a snapshot.</summary>
public sealed class UserRng : IRng
{
    private double next;

    /// <summary>Initializes a new instance of the <see cref="UserRng"/> class.</summary>
    /// <param name="start">The first draw.</param>
    public UserRng(double start) => next = start;

    /// <inheritdoc/>
    public Sample<double> Next() => new(next++, 1.0);

    /// <inheritdoc/>
    public IRng Clone() => new UserRng(next);
}
