// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A <see cref="MonteCarloPath"/> its user builds is a value (docs/porting-playbook.md §2.5): it copies the values it is
/// given, so changing the vector afterwards changes nothing in the path, and a path copied through the constructor
/// keeps its values when the original's vector changes.
/// </summary>
public class MonteCarloPathCopyTests
{
    [Fact]
    public void AUserBuiltPathCopiesItsValues()
    {
        var grid = new TimeGrid(1.0, 3);
        Vector values = [1.0, 2.0, 3.0, 4.0];
        var path = new MonteCarloPath(grid, values);

        values[0] = 42.0;
        values += 1.0;
        values.Resize(2);

        Assert.Equal(4, path.Length);
        Assert.Equal([1.0, 2.0, 3.0, 4.0], Values(path));

        // A copy through the constructor, as a caller keeps a generator's path, is independent of its source.
        Vector source = [5.0, 6.0, 7.0, 8.0];
        var original = new MonteCarloPath(grid, source);
        var copy = new MonteCarloPath(original.TimeGrid, [.. original]);
        source *= 2.0;
        Assert.Equal([5.0, 6.0, 7.0, 8.0], Values(copy));
        Assert.Same(original.TimeGrid, copy.TimeGrid);
    }

    private static double[] Values(MonteCarloPath path)
    {
        var values = new List<double>();
        foreach (double x in path)
        {
            values.Add(x);
        }

        return [.. values];
    }
}
