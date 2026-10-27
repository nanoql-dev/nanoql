// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's pseudo-root facade vectors,
/// <c>tests/vectors/cxx/models/marketmodels/models/pseudorootfacade.json</c>:
/// <see cref="PseudoRootFacade"/> on seeded pseudo-roots (four and seven rates, the factors, undisplaced and displaced)
/// and on the alpha-form calibration's, its reads; and the refused inputs.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PseudoRootFacadeCxxVectorTests
{
    [Fact]
    public void PseudoRootFacadeCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/models/pseudorootfacade");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix[] pseudoRoots = [.. c.Inputs.GetProperty("pseudoRoots").EnumerateArray().Select(MatrixOf)];
            double[] rateTimes = Reals(c.Inputs.GetProperty("rateTimes"));
            double[] initialRates = Reals(c.Inputs.GetProperty("initialRates"));
            double[] displacements = Reals(c.Inputs.GetProperty("displacements"));
            if (c.Expected.TryGetProperty("throws", out _))
            {
                check.Output(c, "throws", Lazy(() => new PseudoRootFacade(pseudoRoots, rateTimes, initialRates, displacements)));
            }
            else
            {
                CheckModel(check, c, () => new PseudoRootFacade(pseudoRoots, rateTimes, initialRates, displacements));
            }
        }

        check.Complete();
    }
}
