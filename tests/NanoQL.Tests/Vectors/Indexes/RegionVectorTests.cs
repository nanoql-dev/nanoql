// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The region vectors, <c>tests/vectors/indexes/region.json</c>: each preset <see cref="Region"/>'s name and code, and
/// <see cref="CustomRegion"/>'s.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RegionVectorTests
{
    [Fact]
    public void RegionVector()
    {
        var file = VectorFile.Load("indexes/region");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Region region = InflationInputs.RegionOf(c.Input("region"));
            check.Output(c, "name", region.Name);
            check.Output(c, "code", region.Code);
        }

        check.Complete();
    }
}
