// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures.Excel;

/// <summary>The listed type of the list's tests: its own members, its base's and its interface's, each once.</summary>
/// <param name="side">The side.</param>
public sealed class Square(double side) : Shape, IShape
{
    /// <inheritdoc/>
    public string Label => "square";

    /// <inheritdoc/>
    public override double Side() => side;

    /// <inheritdoc/>
    public override double Scaled(double k) => 2 * k * side;
}
