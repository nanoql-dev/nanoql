// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/statistics/gaussianstatistics.hpp: GenericGaussianStatistics, which the wheel doesn't expose, over
// GeneralStatistics, IncrementalStatistics and StatsHolder. "GaussianStatistics" takes (kind, data, weights, member,
// argument): kind "general" or "incremental" adds every sample with its weight, and "holder" builds a StatsHolder from
// data's two elements (mean, standard deviation); member is gaussianDownsideVariance, gaussianDownsideDeviation,
// gaussianRegret, gaussianPercentile, gaussianTopPercentile, gaussianPotentialUpside, gaussianValueAtRisk,
// gaussianExpectedShortfall, gaussianShortfall or gaussianAverageShortfall, the argument its target or percentile
// (null for the two that take none).

#include "convert.hpp"

#include <ql/math/statistics/gaussianstatistics.hpp>
#include <ql/math/statistics/generalstatistics.hpp>
#include <ql/math/statistics/incrementalstatistics.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        template <class Stat>
        Real member(const GenericGaussianStatistics<Stat>& s, const std::string& m, Real x) {
            if (m == "gaussianDownsideVariance")
                return s.gaussianDownsideVariance();
            if (m == "gaussianDownsideDeviation")
                return s.gaussianDownsideDeviation();
            if (m == "gaussianRegret")
                return s.gaussianRegret(x);
            if (m == "gaussianPercentile")
                return s.gaussianPercentile(x);
            if (m == "gaussianTopPercentile")
                return s.gaussianTopPercentile(x);
            if (m == "gaussianPotentialUpside")
                return s.gaussianPotentialUpside(x);
            if (m == "gaussianValueAtRisk")
                return s.gaussianValueAtRisk(x);
            if (m == "gaussianExpectedShortfall")
                return s.gaussianExpectedShortfall(x);
            if (m == "gaussianShortfall")
                return s.gaussianShortfall(x);
            return s.gaussianAverageShortfall(x);
        }

        template <class Stat>
        GenericGaussianStatistics<Stat> accumulated(const std::vector<Real>& data, const std::vector<Real>& weights) {
            GenericGaussianStatistics<Stat> s;
            for (Size i = 0; i < data.size(); ++i)
                s.add(data[i], weights[i]);
            return s;
        }

        const Register unit({
            {"GaussianStatistics",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::string kind = text(a[0]);
                 std::vector<Real> data = reals(a[1]), weights = reals(a[2]);
                 std::string m = text(a[3]);
                 Real x = real(a[4]);
                 if (kind != "general" && kind != "incremental" && kind != "holder")
                     throw InvalidParams("unknown statistics kind " + kind);
                 if (kind == "holder" ? data.size() != 2 : data.size() != weights.size())
                     throw InvalidParams("a holder's mean and standard deviation, or a weight for each sample");
                 static const char* known[] = {"gaussianDownsideVariance", "gaussianDownsideDeviation", "gaussianRegret",
                                               "gaussianPercentile", "gaussianTopPercentile", "gaussianPotentialUpside",
                                               "gaussianValueAtRisk", "gaussianExpectedShortfall", "gaussianShortfall",
                                               "gaussianAverageShortfall"};
                 bool found = false;
                 for (const char* k : known)
                     found = found || m == k;
                 if (!found)
                     throw InvalidParams("unknown member " + m);
                 return [=] {
                     if (kind == "general")
                         return json::value(member(accumulated<GeneralStatistics>(data, weights), m, x));
                     if (kind == "incremental")
                         return json::value(member(accumulated<IncrementalStatistics>(data, weights), m, x));
                     GenericGaussianStatistics<StatsHolder> h(StatsHolder(data[0], data[1]));
                     return json::value(member(h, m, x));
                 };
             }},
        });

    }

}
