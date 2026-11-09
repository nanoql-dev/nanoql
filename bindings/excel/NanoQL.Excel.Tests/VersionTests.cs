// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel.Tests;

/// <summary>
/// The add-in's version is the release's, Directory.Build.props's, as nqlxlVersion gives it and a repro vector's
/// generator names it, not the assemblies' version, which stays 2.0.0.0 within 2.y.
/// </summary>
public class VersionTests
{
    /// <summary>nqlxlVersion and a vector's generator name Directory.Build.props's version.</summary>
    [Fact]
    public void TheVersionIsTheReleases()
    {
        string props = File.ReadAllText(Path.Combine(ErrorTests.Repository(), "Directory.Build.props"));
        int start = props.IndexOf("<Version>", StringComparison.Ordinal) + "<Version>".Length;
        string version = props[start..props.IndexOf("</Version>", start, StringComparison.Ordinal)];
        Assert.StartsWith("2.", version, StringComparison.Ordinal);
        Assert.Equal(version, X.VersionText);
        Assert.StartsWith($"nanoql {version} runtime ", OwnFunctions.Version(), StringComparison.Ordinal);
    }
}
