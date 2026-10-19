// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit.Sdk;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="AllocationAssert"/> tells code that allocates from a counter that moved once: an allocation at one
/// measurement passes, at the next measurement's, and one at every measurement fails, with each measurement's bytes.
/// Each measurement runs on state its preparation made for it, so code that allocates once per state fails each time.
/// </summary>
public class AllocationAssertTests
{
    [Fact]
    public void AnAllocationAtOneMeasurementOnlyPasses()
    {
        int calls = 0;
        byte[]? kept = null;

        AllocationAssert.AllocatesNothing(
            () => calls,
            _ =>
            {
                if (calls++ == 0)
                {
                    kept = new byte[4096];
                }
            });

        // a measurement after the first allocated nothing (the runtime's own allocation may take one more)
        Assert.NotNull(kept);
        Assert.InRange(calls, 2, 5);
    }

    [Fact]
    public void AnAllocationAtEveryMeasurementFails()
    {
        int calls = 0;
        byte[]? kept = null;

        XunitException e = Assert.ThrowsAny<XunitException>(() => AllocationAssert.AllocatesNothing(
            () => calls,
            _ =>
            {
                calls++;
                kept = new byte[64];
            }));

        Assert.NotNull(kept);
        Assert.Equal(5, calls);
        Assert.StartsWith("allocated ", e.Message, StringComparison.Ordinal);
        Assert.EndsWith(" bytes in 5 measurements", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EachMeasurementRunsOnStateOfItsOwn()
    {
        object[] seen = new object[5];
        int calls = 0;

        Assert.ThrowsAny<XunitException>(() => AllocationAssert.AllocatesNothing(
            () => new object(),
            state =>
            {
                seen[calls++] = state;
                GC.KeepAlive(new byte[64]);
            }));

        Assert.Equal(5, calls);
        Assert.Equal(5, seen.Distinct(ReferenceEqualityComparer.Instance).Count());
    }
}
