// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL;

/// <summary>
/// The exception nanoql throws when a calculation fails for a reason other than a bad argument: QuantLib's
/// <c>QL_REQUIRE</c>, <c>QL_FAIL</c> and <c>QL_ENSURE</c>, apart from a public method's checks of its own arguments,
/// which throw <see cref="ArgumentException"/>. The message keeps QuantLib's wording.
/// </summary>
public sealed class NanoQLException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="NanoQLException"/> class.</summary>
    public NanoQLException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NanoQLException"/> class with a message.</summary>
    /// <param name="message">The message that describes the error.</param>
    public NanoQLException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NanoQLException"/> class with a message and the exception that
    /// caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public NanoQLException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
