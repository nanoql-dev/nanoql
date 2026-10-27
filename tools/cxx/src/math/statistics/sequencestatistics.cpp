// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/statistics/sequencestatistics.hpp: the lifted members of GenericSequenceStatistics the wheel doesn't bind.
//
// "GenericSequenceStatistics.members" takes (statistics, samples, weights, percentile, target): the statistics
// "Statistics" (QuantLib's SequenceStatistics) or "IncrementalStatistics" (SequenceStatisticsInc), each sample (a row)
// added with its weight; the result each lifted member's vector, {"throws": true} where QuantLib refuses it: for
// Statistics the Gaussian members (gaussianPercentile, gaussianPotentialUpside, gaussianValueAtRisk and
// gaussianExpectedShortfall at the percentile, gaussianShortfall and gaussianAverageShortfall at the target) and the
// empirical ones (percentile, potentialUpside, valueAtRisk and expectedShortfall at the percentile, regret, shortfall
// and averageShortfall at the target, semiVariance, semiDeviation, downsideVariance, downsideDeviation); for
// IncrementalStatistics its own downsideVariance and downsideDeviation.

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/errors.hpp>
#include <ql/math/statistics/sequencestatistics.hpp>

#include <functional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness::math {

    namespace {

        json::value attempt(const std::function<std::vector<Real>()>& f) {
            try {
                return marketmodels::result(f());
            } catch (Error&) {
                json::object t;
                t["throws"] = true;
                return t;
            }
        }

        template <class S>
        S filled(const marketmodels::Mat& samples, const std::vector<Real>& weights) {
            S s(samples.empty() ? 0 : samples[0].size());
            for (Size i = 0; i < samples.size(); ++i)
                s.add(samples[i], weights[i]);
            return s;
        }

        const Register unit({
            {"GenericSequenceStatistics.members",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::string type = text(a[0]);
                 if (type != "Statistics" && type != "IncrementalStatistics")
                     throw InvalidParams("unknown statistics " + type);
                 marketmodels::Mat samples = marketmodels::matrix(a[1]);
                 std::vector<Real> weights = reals(a[2]);
                 Real p = real(a[3]), t = real(a[4]);
                 if (samples.empty() || weights.size() != samples.size())
                     throw InvalidParams("a sample at least, a weight a sample");
                 for (const auto& x : samples)
                     if (x.size() != samples[0].size() || x.empty())
                         throw InvalidParams("samples of one positive dimension");
                 return [=] {
                     json::object out;
                     if (type == "Statistics") {
                         SequenceStatistics s = filled<SequenceStatistics>(samples, weights);
                         out["gaussianPercentile"] = attempt([&] { return s.gaussianPercentile(p); });
                         out["gaussianPotentialUpside"] = attempt([&] { return s.gaussianPotentialUpside(p); });
                         out["gaussianValueAtRisk"] = attempt([&] { return s.gaussianValueAtRisk(p); });
                         out["gaussianExpectedShortfall"] = attempt([&] { return s.gaussianExpectedShortfall(p); });
                         out["gaussianShortfall"] = attempt([&] { return s.gaussianShortfall(t); });
                         out["gaussianAverageShortfall"] = attempt([&] { return s.gaussianAverageShortfall(t); });
                         out["percentile"] = attempt([&] { return s.percentile(p); });
                         out["potentialUpside"] = attempt([&] { return s.potentialUpside(p); });
                         out["valueAtRisk"] = attempt([&] { return s.valueAtRisk(p); });
                         out["expectedShortfall"] = attempt([&] { return s.expectedShortfall(p); });
                         out["regret"] = attempt([&] { return s.regret(t); });
                         out["shortfall"] = attempt([&] { return s.shortfall(t); });
                         out["averageShortfall"] = attempt([&] { return s.averageShortfall(t); });
                         out["semiVariance"] = attempt([&] { return s.semiVariance(); });
                         out["semiDeviation"] = attempt([&] { return s.semiDeviation(); });
                         out["downsideVariance"] = attempt([&] { return s.downsideVariance(); });
                         out["downsideDeviation"] = attempt([&] { return s.downsideDeviation(); });
                     } else {
                         SequenceStatisticsInc s = filled<SequenceStatisticsInc>(samples, weights);
                         out["downsideVariance"] = attempt([&] { return s.downsideVariance(); });
                         out["downsideDeviation"] = attempt([&] { return s.downsideDeviation(); });
                     }
                     return json::value(out);
                 };
             }},
        });

    }

}
