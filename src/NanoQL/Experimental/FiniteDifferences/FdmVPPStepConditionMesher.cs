// SPDX-FileCopyrightText: Copyright (C) 2011, 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmvppstepcondition.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>The mesher a virtual power plant's step condition runs on, and the direction of the plant's states.</summary>
/// <param name="StateDirection">The direction whose coordinates are the plant's states.</param>
/// <param name="Mesher">The mesher.</param>
[Experimental("NQL0001")]
public sealed record FdmVPPStepConditionMesher(int StateDirection, FdmMesher Mesher);
