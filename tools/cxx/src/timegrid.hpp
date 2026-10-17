// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The TimeGrid spec, shared by the units whose methods take a grid (timegrid.cpp, methods/montecarlo/path.cpp):
// {} for TimeGrid(); {"end", "steps"} for TimeGrid(end, steps); {"times"} for TimeGrid(begin, end) over the times;
// {"times", "steps"} for TimeGrid(begin, end, steps). The times are a list of reals, the steps a non-negative integer.

#ifndef NANOQL_CXX_TIMEGRID_HPP
#define NANOQL_CXX_TIMEGRID_HPP

#include "convert.hpp"

#include <ql/timegrid.hpp>

namespace harness {

    struct TimeGridSpec {
        enum Kind { Empty, Regular, Mandatory, MandatoryWithSteps } kind;
        QuantLib::Real end;
        QuantLib::Size steps;
        std::vector<QuantLib::Real> times;
    };

    // The check.
    TimeGridSpec timeGrid(const json::value& v);

    // The call.
    QuantLib::TimeGrid makeTimeGrid(const TimeGridSpec& s);

    // Results: a grid's times, as a list of reals.
    json::value timesResult(const QuantLib::TimeGrid& g);

}

#endif
