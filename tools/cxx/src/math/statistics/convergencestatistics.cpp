// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/statistics/convergencestatistics.hpp: the convergence table, which the wheel doesn't expose.
//
// "ConvergenceStatistics.table" takes (statistics, values, weights, resetAfter): ConvergenceStatistics on
// "IncrementalStatistics" or "Statistics" with the doubling rule, each value added with its weight, after resetAfter
// values (none if null) a reset; the result {"table" ([[samples, mean], ...]), "samples", "mean"}.

#include "convert.hpp"

#include <ql/math/statistics/convergencestatistics.hpp>
#include <ql/math/statistics/incrementalstatistics.hpp>
#include <ql/math/statistics/statistics.hpp>

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness::math {

    namespace {

        template <class S>
        json::value table(const std::vector<Real>& values, const std::vector<Real>& weights,
                          std::optional<Size> resetAfter) {
            ConvergenceStatistics<S> stats;
            for (Size i = 0; i < values.size(); ++i) {
                if (resetAfter && i == *resetAfter)
                    stats.reset();
                stats.add(values[i], weights[i]);
            }
            json::array t;
            for (const auto& row : stats.convergenceTable()) {
                json::array r;
                r.emplace_back(static_cast<std::int64_t>(row.first));
                r.emplace_back(row.second);
                t.emplace_back(r);
            }
            json::object out;
            out["table"] = t;
            out["samples"] = static_cast<std::int64_t>(stats.samples());
            out["mean"] = stats.mean();
            return json::value(out);
        }

        const Register unit({
            {"ConvergenceStatistics.table",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::string type = text(a[0]);
                 if (type != "IncrementalStatistics" && type != "Statistics")
                     throw InvalidParams("unknown statistics " + type);
                 std::vector<Real> values = reals(a[1]), weights = reals(a[2]);
                 if (weights.size() != values.size())
                     throw InvalidParams("a weight a value");
                 std::optional<Size> resetAfter;
                 if (!a[3].is_null()) {
                     QuantLib::Integer n = integer(a[3]);
                     if (n < 0)
                         throw InvalidParams("resetAfter is a count");
                     resetAfter = static_cast<Size>(n);
                 }
                 return [=] {
                     return type == "Statistics" ? table<Statistics>(values, weights, resetAfter)
                                                 : table<IncrementalStatistics>(values, weights, resetAfter);
                 };
             }},
        });

    }

}
