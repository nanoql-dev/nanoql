// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmMesherComposite vectors, <c>tests/vectors/methods/finitedifferences/meshers/fdmmeshercomposite.json</c>:
/// <see cref="FdmMesherComposite"/> over one to four 1-D meshers, with and without an explicit layout, its layout's
/// dimensions and size, its locations in each direction and its 1-D meshers'
/// (<see cref="FdmMesherComposite.GetFdm1dMeshers"/>), and the location, dplus and dminus at every layout point; and
/// the refusal of a layout that doesn't fit its meshers.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmMesherCompositeVectorTests
{
    [Fact]
    public void FdmMesherCompositeVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/meshers/fdmmeshercomposite");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("mesher", out _))
            {
                check.Output(c, "mesher", () => MesherOf(c.Input("mesher"), context));
                continue;
            }

            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), context);
            FdmLinearOpLayout layout = mesher.Layout;
            int directions = layout.Dim.Length;
            check.Output(c, "dim", layout.Dim.ToArray());
            check.Output(c, "size", layout.Size);
            check.Output(c, "locations", Enumerable.Range(0, directions).Select(d => mesher.Locations(d).ToArray()).ToArray());
            check.Output(c, "location", AtEachPoint(mesher, (it, d) => mesher.Location(it, d)));
            check.Output(c, "dplus", AtEachPoint(mesher, (it, d) => Nullable(mesher.Dplus(it, d))));
            check.Output(c, "dminus", AtEachPoint(mesher, (it, d) => Nullable(mesher.Dminus(it, d))));
            check.Output(c, "fdm1dMeshers", mesher.GetFdm1dMeshers().Select(m => m.Locations().ToArray()).ToArray());
        }

        check.Complete();
    }

    // In each direction, the value at each layout point, walked as the generator walks it
    private static double?[][] AtEachPoint(FdmMesher mesher, Func<FdmLinearOpIterator, int, double?> value)
    {
        FdmLinearOpLayout layout = mesher.Layout;
        var values = new double?[layout.Dim.Length][];
        for (int d = 0; d < values.Length; d++)
        {
            values[d] = new double?[layout.Size];
            FdmLinearOpIterator iter = layout.Begin();
            for (int i = 0; i < layout.Size; i++, iter.Increment())
            {
                values[d][i] = value(iter, d);
            }
        }

        return values;
    }
}
