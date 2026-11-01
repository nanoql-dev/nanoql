// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Native;

/// <summary>
/// A refusal the C ABI itself makes: a handle released, stale, foreign or never issued (<c>NQL_ERROR_HANDLE</c>), or of
/// the wrong type, or a bound engine that can't serve a member (<c>NQL_ERROR_TYPE</c>).
/// </summary>
internal sealed class HandleException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="HandleException"/> class.</summary>
    /// <param name="code">The status code.</param>
    /// <param name="message">The message.</param>
    /// <param name="param">The parameter refused, or null.</param>
    public HandleException(int code, string message, string? param)
        : base(message)
    {
        Code = code;
        Param = param;
    }

    /// <summary>Gets the status code.</summary>
    public int Code { get; }

    /// <summary>Gets the parameter refused, or null.</summary>
    public string? Param { get; }
}
