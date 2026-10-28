// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// A Distribution's buckets as the vectors read them: [size, x, dx, density, cumulative, excess, cumulativeExcess,
// average], the densities read first, so that the distribution is normalized (QuantLib's density(k) normalizes, its
// average(k) doesn't).
#ifndef NANOQL_CXX_EXPERIMENTAL_CREDIT_DISTRIBUTIONSPEC_HPP
#define NANOQL_CXX_EXPERIMENTAL_CREDIT_DISTRIBUTIONSPEC_HPP

#include "convert.hpp"

#include <ql/experimental/credit/distribution.hpp>

namespace harness::credit {

    inline json::value summary(QuantLib::Distribution& d) {
        json::array density, cumulative, excess, cumulativeExcess, average, x, dx;
        for (QuantLib::Size k = 0; k < d.size(); k++) {
            density.emplace_back(d.density(k));
            cumulative.emplace_back(d.cumulative(k));
            excess.emplace_back(d.excess(k));
            cumulativeExcess.emplace_back(d.cumulativeExcess(k));
        }
        for (QuantLib::Size k = 0; k < d.size(); k++) {
            average.emplace_back(d.average(k));
            x.emplace_back(d.x(k));
            dx.emplace_back(d.dx(k));
        }
        json::array out;
        out.emplace_back(static_cast<std::int64_t>(d.size()));
        for (auto* a : {&x, &dx, &density, &cumulative, &excess, &cumulativeExcess, &average})
            out.emplace_back(std::move(*a));
        return json::value(std::move(out));
    }

}

#endif
