// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/exercise.hpp ql/exercise.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>The type of an exercise: QuantLib's <c>Exercise::Type</c>.</summary>
/// <remarks>QuantLib has no stream operator for it, so it has no text of its own.</remarks>
public enum ExerciseType
{
    /// <summary>Exercisable at any time between two dates.</summary>
    American = 0,

    /// <summary>Exercisable on a set of fixed dates.</summary>
    Bermudan = 1,

    /// <summary>Exercisable on one date.</summary>
    European = 2,
}
