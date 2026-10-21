// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmLinearOpLayout vectors, <c>tests/vectors/methods/finitedifferences/operators/fdmlinearoplayout.json</c>:
/// <see cref="FdmLinearOpLayout"/>'s dimensions, size and spacing, the walk of its <see cref="FdmLinearOpIterator"/>,
/// the index of each point's coordinates, and each point's neighbourhoods in one and two directions and through
/// <see cref="FdmLinearOpLayout.IterNeighbourhood"/>, over layouts of one to three dimensions.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmLinearOpLayoutVectorTests
{
    [Fact]
    public void FdmLinearOpLayoutVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdmlinearoplayout");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int[] dims = [.. c.Input("dim").EnumerateArray().Select(d => d.GetInt32())];
            int[][] offsets = [.. c.Input("offsets").EnumerateArray().Select(o => o.EnumerateArray().Select(x => x.GetInt32()).ToArray())];
            (int, int)[] pairs = [.. c.Input("pairs").EnumerateArray().Select(p => (p[0].GetInt32(), p[1].GetInt32()))];
            var layout = new FdmLinearOpLayout(dims);
            var indices = new List<object?>();
            var coordinates = new List<object?>();
            var index = new List<object?>();
            var neighbourhood = new List<object?>();
            var neighbourhood2 = new List<object?>();
            var iterNeighbourhood = new List<object?>();
            FdmLinearOpIterator iter = layout.Begin();
            for (int i = 0; i < layout.Size; i++, iter.Increment())
            {
                indices.Add(iter.Index);
                coordinates.Add(iter.Coordinates.ToArray());
                index.Add(layout.Index(iter.Coordinates));
                neighbourhood.Add(Enumerable.Range(0, dims.Length)
                    .Select(d => offsets[d].Select(o => layout.Neighbourhood(iter, d, o)).ToArray()).ToArray());
                iterNeighbourhood.Add(Enumerable.Range(0, dims.Length)
                    .Select(d => offsets[d].Select(o => layout.IterNeighbourhood(iter, d, o).Index).ToArray()).ToArray());
                var twos = new List<int[]>();
                for (int d1 = 0; d1 < dims.Length; d1++)
                {
                    for (int d2 = 0; d2 < dims.Length; d2++)
                    {
                        if (d1 != d2)
                        {
                            twos.Add([.. pairs.Where(p => Math.Abs(p.Item1) < dims[d1] && Math.Abs(p.Item2) < dims[d2])
                                .Select(p => layout.Neighbourhood(iter, d1, p.Item1, d2, p.Item2))]);
                        }
                    }
                }

                neighbourhood2.Add(twos.ToArray());
            }

            check.Output(c, "dim", layout.Dim.ToArray());
            check.Output(c, "size", layout.Size);
            check.Output(c, "spacing", layout.Spacing.ToArray());
            check.Output(c, "indices", indices);
            check.Output(c, "coordinates", coordinates);
            check.Output(c, "index", index);
            check.Output(c, "neighbourhood", neighbourhood);
            check.Output(c, "neighbourhood2", neighbourhood2);
            check.Output(c, "iterNeighbourhood", iterNeighbourhood);
        }

        check.Complete();
    }
}
