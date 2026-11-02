// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel.Replay;

/// <summary>One recorded fact: its runs, those replayed whole, its calls and those replayed.</summary>
/// <param name="name">The fact's name.</param>
internal sealed class Fact(string name)
{
    /// <summary>Gets the fact's name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets or sets its runs.</summary>
    public long Runs { get; set; }

    /// <summary>Gets or sets its runs replayed whole.</summary>
    public long Whole { get; set; }

    /// <summary>Gets or sets its calls.</summary>
    public long Calls { get; set; }

    /// <summary>Gets or sets its calls replayed.</summary>
    public long Replayed { get; set; }
}
