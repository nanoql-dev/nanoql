// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's exponential correlation vectors, <c>tests/vectors/cxx/models/marketmodels/correlations/expcorrelations.json</c>:
/// <see cref="ExpCorrelationsFunctions.ExponentialCorrelations"/>' matrices and refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExpCorrelationsCxxVectorTests
{
    [Fact]
    public void ExpCorrelationsCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/correlations/expcorrelations");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] times = c.Doubles("rateTimes");
            double corr = c.Double("longTermCorr"), beta = c.Double("beta"), gamma = c.Double("gamma"), t = c.Double("t");
            check.Output(c, "matrix", Lazy(() => Rows(ExpCorrelationsFunctions.ExponentialCorrelations(times, corr, beta, gamma, t))));
        }

        check.Complete();
    }
}
