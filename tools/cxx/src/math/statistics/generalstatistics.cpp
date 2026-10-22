// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/statistics/generalstatistics.hpp, .cpp: the members the wheel's Statistics doesn't expose. Each method takes
// (data, weights, ...) and adds every sample with its weight first: "GeneralStatistics::percentile" and
// "GeneralStatistics::topPercentile" take y and return the percentile; "GeneralStatistics::data" takes whether to sort
// first and returns the samples as (value, weight) pairs, in the set's order.

#include "convert.hpp"

#include <ql/math/statistics/generalstatistics.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct Set {
            std::vector<Real> data, weights;
        };

        Set set(const json::value& data, const json::value& weights) {
            Set s{reals(data), reals(weights)};
            if (s.data.size() != s.weights.size())
                throw InvalidParams("a weight for each sample");
            return s;
        }

        GeneralStatistics make(const Set& s) {
            GeneralStatistics g;
            for (Size i = 0; i < s.data.size(); ++i)
                g.add(s.data[i], s.weights[i]);
            return g;
        }

        Call percentile(const Args& a, bool top) {
            arity(a, 3);
            Set s = set(a[0], a[1]);
            Real y = real(a[2]);
            return [=] {
                GeneralStatistics g = make(s);
                return json::value(top ? g.topPercentile(y) : g.percentile(y));
            };
        }

        const Register unit({
            {"GeneralStatistics::percentile", [](const Args& a) -> Call { return percentile(a, false); }},
            {"GeneralStatistics::topPercentile", [](const Args& a) -> Call { return percentile(a, true); }},
            {"GeneralStatistics::data",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Set s = set(a[0], a[1]);
                 bool sortFirst = boolean(a[2]);
                 return [=] {
                     GeneralStatistics g = make(s);
                     if (sortFirst)
                         g.sort();
                     json::array out;
                     for (const auto& p : g.data())
                         out.emplace_back(json::array{json::value(p.first), json::value(p.second)});
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
