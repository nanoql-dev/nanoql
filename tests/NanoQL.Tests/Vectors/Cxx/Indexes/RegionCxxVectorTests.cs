// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's region vectors, <c>tests/vectors/cxx/indexes/region.json</c>: <see cref="Region"/>'s
/// <c>==</c> and <c>!=</c>, QuantLib's comparison by name, over every pair of the preset and custom regions.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RegionCxxVectorTests
{
    [Fact]
    public void RegionCxxVector()
    {
        var file = VectorFile.Load("cxx/indexes/region");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Region[] regions = [.. c.Input("regions").EnumerateArray().Select(InflationInputs.RegionOf)];
            check.Output(c, "equal", regions.Select(a => regions.Select(b => a == b).ToArray()).ToArray());
            check.Output(c, "notEqual", regions.Select(a => regions.Select(b => a != b).ToArray()).ToArray());
        }

        check.Complete();
    }
}
