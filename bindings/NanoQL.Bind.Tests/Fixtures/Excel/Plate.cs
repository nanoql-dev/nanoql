// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures.Excel;

/// <summary>A type only <see cref="PlateMaker"/> makes, so a list without the maker leaves it unmade.</summary>
public sealed class Plate
{
    internal Plate(double size) => Size = size;

    /// <summary>Gets the size.</summary>
    public double Size { get; }
}
