// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's FdmHestonGreensFct vectors,
/// <c>tests/vectors/cxx/methods/finitedifferences/utilities/fdmhestongreensfct.json</c>:
/// <see cref="FdmHestonGreensFct"/> on two Heston processes under the plain, power and log transformations, by the
/// zero-correlation and Gaussian algorithms, at a day and at 0.02 years, the leverage 1 and 0.8.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmHestonGreensFctCxxVectorTests
{
    [Fact]
    public void FdmHestonGreensFctCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/utilities/fdmhestongreensfct");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var locations = c.Input("locations");
            var mesher = new FdmMesherComposite(
                new Predefined1dMesher(locations[0].EnumerateArray().Select(e => e.GetDouble())),
                new Predefined1dMesher(locations[1].EnumerateArray().Select(e => e.GetDouble())));
            var greens = new FdmHestonGreensFct(
                mesher,
                HestonCxxInputs.ProcessOf(c.Input("process")),
                (FdmSquareRootFwdOpTransformationType)c.Input("trafoType").GetInt32(),
                c.Double("l0"));
            var algorithm = (FdmHestonGreensFctAlgorithm)c.Input("algorithm").GetInt32();
            check.Output(
                c,
                "density",
                c.Input("times").EnumerateArray().Select(t => (object?)greens.Get(t.GetDouble(), algorithm).ToArray()).ToArray());
        }

        check.Complete();
    }
}
