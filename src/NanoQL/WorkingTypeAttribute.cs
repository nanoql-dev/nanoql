// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL;

/// <summary>
/// Marks the types the playbook calls working types (docs/porting-playbook.md §2.5): mutable, owned by one
/// computation and not thread-safe, as an array is. A type deriving from a marked class, or implementing a marked
/// interface, is one of them too. The bindings keep every such type off their handles, which any thread may share:
/// <c>Matrix</c> and <c>Vector</c> cross as arrays, a generator as an immutable snapshot drawn in batches, and the
/// rest stay inside nanoql (docs/bindings.md).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = true)]
public sealed class WorkingTypeAttribute : Attribute
{
}
