// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// Objects inside a returned collection: keyed by the parent call and their place, never by identity, so fresh objects
/// get the same handles; each records its parent call and position, which <c>nqlxlRecipe</c> shows.
/// </summary>
[Collection(TestSettings.Name)]
public sealed class SlotTests
{
    /// <summary>Initializes a new instance of the <see cref="SlotTests"/> class, on a clean runtime.</summary>
    public SlotTests() => Reset();

    /// <summary>
    /// A map's objects: the same handles over fresh objects, one per key, while the parent's cell holds them and when it
    /// is rebuilt after its release (a workbook reopened); another parent call, other handles.
    /// </summary>
    [Fact]
    public void AMapsObjectsByTheirKeys()
    {
        static Dictionary<string, object> Fresh() => new() { ["a"] = new CompositeInstrument(), ["b"] = new CompositeInstrument() };
        object[] parent = ["A", 1.0];
        var m1 = Assert.IsType<object[,]>(X.Present("nqlProbeMap", parent, Fresh()));
        var m2 = Assert.IsType<object[,]>(X.Present("nqlProbeMap", parent, Fresh()));
        var m3 = Assert.IsType<object[,]>(X.Present("nqlProbeMap", ["B", 1.0], Fresh()));
        Assert.Equal(m1[0, 1], m2[0, 1]);
        Assert.Equal(m1[1, 1], m2[1, 1]);
        Assert.NotEqual(m1[0, 1], m1[1, 1]);
        Assert.NotEqual(m1[0, 1], m3[0, 1]);
        Assert.True(X.TryEntry((string)m1[0, 1], out XlEntry child));
        Host.Close(child.ParentKey!);
        Assert.False(X.TryEntry((string)m1[0, 1], out _));
        var m4 = Assert.IsType<object[,]>(X.Present("nqlProbeMap", parent, Fresh()));
        Assert.Equal(m1[0, 1], m4[0, 1]);
        Assert.Equal(m1[1, 1], m4[1, 1]);
    }

    /// <summary>An array's objects by their position: 200,000 of one array, 200,000 handles; each's recipe its parent call's item.</summary>
    [Fact]
    public void AnArraysObjectsByTheirPosition()
    {
        var many = Enumerable.Range(0, 200000).Select(_ => (object)new CompositeInstrument()).ToList();
        var col = Assert.IsType<object[,]>(X.Present("nqlProbeLeg", ["L", 2.0], many));
        Assert.Equal(200000, Enumerable.Range(0, col.GetLength(0)).Select(i => (string)col[i, 0]).Distinct().Count());
        Assert.Equal("nqlProbeLeg(text \"L\", 2), item 7", OwnFunctions.Recipe(col[7, 0]));
    }
}
