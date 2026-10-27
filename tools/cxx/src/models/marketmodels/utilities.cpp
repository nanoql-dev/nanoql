// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/utilities.hpp, .cpp, which the wheel doesn't expose.
//
// "mergeTimes" takes (times), sets of sorted times without a NaN: {"mergedTimes", "isPresent"}, QuantLib's merged
// times appended to an empty vector and each set's presence over the merged times (QuantLib's own sized to the times
// before their duplicates go, its tail past the merged ones unspecified, left out). "isInSubset" takes (set, subset).
// "checkIncreasingTimes" takes (times): null, or QuantLib's throw. "checkIncreasingTimesAndCalculateTaus" takes
// (times): the taus.

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/utilities.hpp>

#include <algorithm>
#include <cmath>
#include <valarray>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        json::value booleans(const std::valarray<bool>& v, Size n) {
            json::array a;
            for (Size i = 0; i < n; ++i)
                a.emplace_back(v[i]);
            return a;
        }

        const Register unit({
            {"mergeTimes",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 if (!a[0].is_array())
                     throw InvalidParams("expected an array of sets of times");
                 std::vector<std::vector<Time>> times;
                 for (const json::value& set : a[0].get_array()) {
                     Vec t = reals(set);
                     for (Size i = 0; i < t.size(); ++i)
                         if (std::isnan(t[i]) || (i > 0 && t[i] < t[i - 1]))
                             throw InvalidParams("each set must be sorted, without a NaN");
                     times.push_back(t);
                 }
                 return [=] {
                     std::vector<Time> merged;
                     std::vector<std::valarray<bool>> isPresent;
                     mergeTimes(times, merged, isPresent);
                     json::array present;
                     for (const auto& p : isPresent)
                         present.emplace_back(booleans(p, merged.size()));
                     json::object out;
                     out["mergedTimes"] = result(merged);
                     out["isPresent"] = present;
                     return json::value(out);
                 };
             }},
            {"isInSubset",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Vec set = reals(a[0]), subset = reals(a[1]);
                 return [=] {
                     std::valarray<bool> r = isInSubset(set, subset);
                     return booleans(r, r.size());
                 };
             }},
            {"checkIncreasingTimes",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Vec times = reals(a[0]);
                 return [=] {
                     checkIncreasingTimes(times);
                     return json::value(nullptr);
                 };
             }},
            {"checkIncreasingTimesAndCalculateTaus",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Vec times = reals(a[0]);
                 return [=] {
                     std::vector<Time> taus;
                     checkIncreasingTimesAndCalculateTaus(times, taus);
                     return result(taus);
                 };
             }},
        });

    }

}
