// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Conventions;

public class NanoQLExceptionTests
{
    [Fact]
    public void ParameterlessConstructorGivesTheDefaultMessage()
    {
        var e = new NanoQLException();

        Assert.Contains(typeof(NanoQLException).FullName!, e.Message, StringComparison.Ordinal);
        Assert.Null(e.InnerException);
    }

    [Fact]
    public void MessageConstructorKeepsTheMessage()
    {
        var e = new NanoQLException("negative time (-1) given");

        Assert.Equal("negative time (-1) given", e.Message);
        Assert.Null(e.InnerException);
    }

    [Fact]
    public void InnerExceptionConstructorKeepsBoth()
    {
        var inner = new InvalidOperationException("inner");
        var e = new NanoQLException("1st leg: inner", inner);

        Assert.Equal("1st leg: inner", e.Message);
        Assert.Same(inner, e.InnerException);
    }
}
