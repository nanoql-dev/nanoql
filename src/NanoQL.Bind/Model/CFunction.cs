// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace NanoQL.Bind.Model;

/// <summary>
/// One C function of the surface: the member it calls, its kind, its parameters and result, and its C name. Kinds:
/// <c>new</c>, <c>method</c>, <c>static</c>, <c>get</c>, <c>set</c> (a static setter), <c>with</c> (a record's
/// init-only property), <c>field</c>, <c>indexer</c>, <c>operator</c>, and the bindings' own: <c>draws</c> (a
/// generator snapshot's batch), <c>bind</c> and <c>bindon</c> (an engine bound with its inputs).
/// </summary>
internal sealed class CFunction
{
    /// <summary>Gets or sets the C names' prefix.</summary>
    public required string Prefix { get; set; }

    /// <summary>Gets or sets the declaring type's C name.</summary>
    public required string TypeName { get; set; }

    /// <summary>Gets or sets the member's base C name (<c>new</c>, <c>get_Name</c>, <c>op_Addition</c>, the method's name).</summary>
    public required string BaseName { get; set; }

    /// <summary>Gets or sets the function's kind.</summary>
    public required string Kind { get; set; }

    /// <summary>Gets or sets the type the function belongs to.</summary>
    public required Type Type { get; set; }

    /// <summary>Gets or sets the type the member is declared on (a named class's closed generic base for its members).</summary>
    public required Type Holder { get; set; }

    /// <summary>Gets or sets the member the function calls; null for the bindings' own functions.</summary>
    public MemberInfo? Member { get; set; }

    /// <summary>Gets or sets a generic method's type arguments, when the function closes one.</summary>
    public IReadOnlyList<Type> TypeArguments { get; set; } = [];

    /// <summary>Gets or sets the function's C name.</summary>
    public string CName { get; set; } = string.Empty;

    /// <summary>Gets or sets the member's identity: its metadata key, with a closed generic method's arguments.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the function is static (no <c>self</c>).</summary>
    public bool Static { get; set; }

    /// <summary>Gets or sets whether the function is experimental (NQL0001).</summary>
    public bool Experimental { get; set; }

    /// <summary>Gets or sets the member's <c>[Obsolete]</c> message, when nanoql deprecates it.</summary>
    public string? Deprecated { get; set; }

    /// <summary>Gets or sets whether the member's C name carries an overload suffix.</summary>
    public bool Overloaded { get; set; }

    /// <summary>Gets the parameters, <c>self</c> first for an instance member.</summary>
    public List<CParam> Params { get; } = [];

    /// <summary>Gets or sets the result's kind, or null for none.</summary>
    public CKind? Return { get; set; }

    /// <summary>Gets or sets the names the overload rule reads, one per parameter after <c>self</c>.</summary>
    public List<string> SignatureNames { get; set; } = [];

    /// <summary>Gets or sets the documented exception types the C ABI's table doesn't name, each with its code.</summary>
    public IReadOnlyList<(string Exception, int Code)> DocumentedCodes { get; set; } = [];

    /// <summary>Gets or sets a <c>bind</c> or <c>bindon</c> function's engine input set.</summary>
    public EngineInputSet? InputSet { get; set; }

    /// <summary>Gets or sets an optional parameter dropped from the C signature, its default passed (a <c>TimeProvider</c>).</summary>
    public int DroppedParameter { get; set; } = -1;
}
