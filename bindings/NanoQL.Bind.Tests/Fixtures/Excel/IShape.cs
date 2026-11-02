// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures.Excel;

/// <summary>An interface a listed type implements: its member reaches the type once, beside the class's own.</summary>
public interface IShape
{
    /// <summary>Gets the label.</summary>
    string Label { get; }

    /// <summary>A scaled side.</summary>
    /// <param name="k">The scale.</param>
    /// <returns>The scaled side.</returns>
    double Scaled(double k);
}
