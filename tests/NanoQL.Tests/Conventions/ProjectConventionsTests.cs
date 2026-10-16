// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace NanoQL.Tests.Conventions;

/// <summary>The project's conventions that no other test exercises: the aliases, the assembly metadata, sealing.</summary>
public class ProjectConventionsTests
{
    [Fact]
    public void TheLibrarysAliasesReachTheTestProject()
    {
        // Types.cs is linked into the test project, so ported tests keep QuantLib's type names.
        Assert.Equal(typeof(double), typeof(Real));
        Assert.Equal(typeof(double), typeof(Rate));
        Assert.Equal(typeof(double), typeof(Spread));
        Assert.Equal(typeof(double), typeof(DiscountFactor));
        Assert.Equal(typeof(double), typeof(Volatility));
        Assert.Equal(typeof(double), typeof(Probability));
        Assert.Equal(typeof(int), typeof(Size));
        Assert.Equal(typeof(int), typeof(Integer));
        Assert.Equal(typeof(int), typeof(Natural));
        Assert.Equal(typeof(long), typeof(BigInteger));
    }

    [Fact]
    public void TheAssemblyRecordsTheQuantLibVersion()
    {
        AssemblyMetadataAttribute metadata = Assert.Single(
            typeof(NanoQLException).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>(),
            a => a.Key == "QuantLibVersion");

        Assert.Equal("1.44", metadata.Value);
    }

    [Fact]
    public void NanoQLExceptionIsSealed()
    {
        Assert.True(typeof(NanoQLException).IsSealed);
    }
}
