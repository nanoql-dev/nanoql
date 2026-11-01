// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures;

/// <summary>A user's marked struct, a working type: kept off handles taken directly or nullable.</summary>
[WorkingType]
public readonly record struct MarkedValue(double X);
