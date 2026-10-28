// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/statistics/discrepancystatistics.hpp, which the wheel doesn't bind: a DiscrepancyStatistics of a dimension
// given (not 0, whose lazy path QuantLib gets wrong, and not 1, which it refuses), and its discrepancy after each
// sample of a list is added, each sample's weight 1.
//
//     DiscrepancyStatistics::discrepancy  (dimension, samples): [after the first, after the second, ...]

#include "convert.hpp"

#include <ql/math/statistics/discrepancystatistics.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"DiscrepancyStatistics::discrepancy",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Integer dimension = integer(a[0]);
                 if (dimension < 2)
                     throw InvalidParams("the dimension is given, and at least 2");
                 if (!a[1].is_array())
                     throw InvalidParams("expected an array of samples, got " + json::serialize(a[1]));
                 std::vector<std::vector<Real>> samples;
                 for (const json::value& s : a[1].get_array())
                     samples.push_back(reals(s));
                 return [=] {
                     DiscrepancyStatistics stat(static_cast<Size>(dimension));
                     json::array out;
                     for (const std::vector<Real>& s : samples) {
                         stat.add(s);
                         out.emplace_back(stat.discrepancy());
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
