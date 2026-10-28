// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/statistics/histogram.hpp, which the wheel doesn't bind: a Histogram of data built with a number of breaks,
// an algorithm ("Sturges", "FD" or "Scott") or the breaks themselves, and what it holds.
//
//     Histogram  (data, {"breaks": n} | {"algorithm": name} | {"breaksGiven": [...]}):
//         {"bins": n, "breaks": [...], "algorithm": k, "empty": b, "counts": [...], "frequency": [...]}

#include "convert.hpp"

#include <ql/math/statistics/histogram.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        Histogram make(const std::vector<Real>& data, const json::value& how) {
            if (!how.is_object() || how.get_object().size() != 1)
                throw InvalidParams("expected {breaks}, {algorithm} or {breaksGiven}, got " + json::serialize(how));
            const json::object& o = how.get_object();
            if (const json::value* n = o.if_contains("breaks")) {
                Integer k = integer(*n);
                if (k < 0)
                    throw InvalidParams("a number of breaks is non-negative");
                return Histogram(data.begin(), data.end(), static_cast<Size>(k));
            }
            if (const json::value* name = o.if_contains("algorithm")) {
                std::string s = text(*name);
                Histogram::Algorithm algorithm = s == "Sturges" ? Histogram::Sturges
                                               : s == "FD"      ? Histogram::FD
                                               : s == "Scott"   ? Histogram::Scott
                                               : throw InvalidParams("expected Sturges, FD or Scott, got " + s);
                return Histogram(data.begin(), data.end(), algorithm);
            }
            if (const json::value* given = o.if_contains("breaksGiven")) {
                std::vector<Real> b = reals(*given);
                return Histogram(data.begin(), data.end(), b.begin(), b.end());
            }
            throw InvalidParams("expected {breaks}, {algorithm} or {breaksGiven}, got " + json::serialize(how));
        }

        const Register unit({
            {"Histogram",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<Real> data = reals(a[0]);
                 json::value how = a[1];
                 return [=] {
                     Histogram h = make(data, how);
                     json::array breaks, counts, frequency;
                     for (Real b : h.breaks())
                         breaks.emplace_back(b);
                     for (Size i = 0; i < h.bins(); ++i) {
                         counts.emplace_back(static_cast<std::int64_t>(h.counts(i)));
                         frequency.emplace_back(h.frequency(i));
                     }
                     json::object out;
                     out["bins"] = static_cast<std::int64_t>(h.bins());
                     out["breaks"] = std::move(breaks);
                     out["algorithm"] = static_cast<std::int64_t>(h.algorithm());
                     out["empty"] = h.empty();
                     out["counts"] = std::move(counts);
                     out["frequency"] = std::move(frequency);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
