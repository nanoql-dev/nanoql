// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009, 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/randomdefaultlatentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Experimental;

/// <summary>
/// A simulated default: the name, its day counted from the evaluation date and its recovery. QuantLib's
/// <c>simEvent</c> specializations pack these into bit fields (16 bits for the default model's name and day; 12, 12 and
/// 8 for the loss model's name, day and recovery in 256ths); here they are values.
/// </summary>
/// <param name="NameIdx">The name's position in the simulated basket.</param>
/// <param name="DayFromRef">The default's day from the evaluation date.</param>
/// <param name="Recovery">The recovery.</param>
internal readonly record struct RandomLMEvent(int NameIdx, int DayFromRef, double Recovery);
