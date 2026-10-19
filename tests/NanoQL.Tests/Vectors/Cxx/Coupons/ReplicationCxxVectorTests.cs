// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's replication vectors, <c>tests/vectors/cxx/cashflows/replication.json</c>: the replication type
/// and gap of a <see cref="DigitalReplication"/> built with QuantLib's defaults, with a type alone and with a type and
/// a gap; and the text of each <see cref="ReplicationType"/>, its <see cref="object.ToString"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ReplicationCxxVectorTests
{
    [Fact]
    public void ReplicationCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/replication");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("type", out _))
            {
                check.Output(c, "text", ((ReplicationType)c.Int("type")).ToString());
                continue;
            }

            JsonElement[] given = [.. c.Input("arguments").EnumerateArray()];
            DigitalReplication replication = given.Length switch
            {
                0 => new DigitalReplication(),
                1 => new DigitalReplication((ReplicationType)given[0].GetInt32()),
                _ => new DigitalReplication((ReplicationType)given[0].GetInt32(), given[1].GetDouble()),
            };
            check.Output(c, "replicationType", (int)replication.ReplicationType);
            check.Output(c, "gap", replication.Gap);
        }

        check.Complete();
    }
}
