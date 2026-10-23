// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Processes;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The simple-process mesher of no point (the S-sr plan's SSR6 (xi)): QuantLib builds its locations by reading the
/// first of an empty array (<c>fdmsimpleprocess1dmesher.cpp</c>, undefined behaviour, master §2.2's undefined-path
/// row); nanoql refuses a size of 0 before reading anything.
/// </summary>
public class SimpleProcessMesherSizeTests
{
    [Fact]
    public void AMesherOfNoPointIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new FdmSimpleProcess1dMesher(0, new OrnsteinUhlenbeckProcess(0.1, 0.01), 1.0));
        Assert.Equal("size", e.ParamName);
        Assert.StartsWith("a mesher needs at least one point, not 0", e.Message, StringComparison.Ordinal);
    }
}
