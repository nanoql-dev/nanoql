// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Selecting;

/// <summary>
/// The generator stops: a case its rules leave to a person, such as a value a member supplies to an engine with no
/// entry in the roles table, or two calculations tied for one member.
/// </summary>
internal sealed class BindException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="BindException"/> class.</summary>
    public BindException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BindException"/> class with a message.</summary>
    /// <param name="message">What stopped the generator.</param>
    public BindException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BindException"/> class with a message and a cause.</summary>
    /// <param name="message">What stopped the generator.</param>
    /// <param name="innerException">The cause.</param>
    public BindException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
