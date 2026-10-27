// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's accounting-engine vectors, <c>tests/vectors/cxx/models/marketmodels/accountingengine.json</c>:
/// <see cref="AccountingEngine"/>'s values over a few paths of the Euler, predictor-corrector and normal evolvers, read
/// through the statistics' samples, weight sum, means, variances and error estimates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AccountingEngineCxxVectorTests
{
    [Fact]
    public void AccountingEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/accountingengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MarketModelProductCxxInputs.CheckEngine(check, c);
        }

        check.Complete();
    }
}
