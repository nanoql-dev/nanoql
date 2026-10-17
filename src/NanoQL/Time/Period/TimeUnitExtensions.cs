// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007, 2008, 2014 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/period.hpp ql/time/period.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Own-tested: TimeUnit * int

namespace NanoQL.Time;

/// <summary>QuantLib's <c>Integer * TimeUnit</c> operators (<c>period.hpp</c>), so that <c>3 * TimeUnit.Months</c> is a <see cref="Period"/>.</summary>
public static class TimeUnitExtensions
{
    extension(TimeUnit)
    {
        /// <summary>A period of the given length in the given unit.</summary>
        /// <param name="n">The length.</param>
        /// <param name="units">The time unit.</param>
        /// <returns>The period.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The unit isn't a member of the enum.</exception>
        public static Period operator *(int n, TimeUnit units) => new(n, units);

        /// <summary>A period of the given length in the given unit.</summary>
        /// <param name="units">The time unit.</param>
        /// <param name="n">The length.</param>
        /// <returns>The period.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The unit isn't a member of the enum.</exception>
        public static Period operator *(TimeUnit units, int n) => new(n, units);
    }
}
