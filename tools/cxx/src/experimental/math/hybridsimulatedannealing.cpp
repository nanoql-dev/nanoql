// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/math/hybridsimulatedannealing.hpp and hybridsimulatedannealingfunctors.hpp: the annealing on the
// functors the wheel doesn't name, each output computed by the template here.
//
// "HybridSimulatedAnnealing::minimize" takes (combination, localOptimizer, maxIterations, maxStationary): the
// combination "cauchy" (SamplerCauchy, ProbabilityAlwaysDownhill, TemperatureCauchy, ReannealingTrivial), "ring"
// (SamplerRingGaussian, ProbabilityBoltzmann, TemperatureBoltzmann, ReannealingFiniteDifferences), "veryFast"
// (SamplerVeryFastAnnealing, ProbabilityBoltzmannDownhill, TemperatureVeryFastAnnealing, ReannealingTrivial) or
// "cauchy1D" (SamplerGaussian, ProbabilityBoltzmannDownhill, TemperatureCauchy1D, ReannealingFiniteDifferences); the
// samplers at seed 42 and the probabilities at 43 within [−5, 5]², each temperature from 1 in two dimensions (the very
// fast one to 0.01 in 100 steps), the finite differences on the bounds; starting at (3, 2) at temperature 1, ending at
// 0.01, reannealing every 5 steps and resetting to the best point every 7; the local optimizer "none" or "lm",
// Levenberg-Marquardt at every best point. It minimizes the sphere centred at (1.2, −0.7), its values the residuals,
// and returns {"x", "value", "evaluations", "endCriteria", "log"}, the log every point the cost function evaluates.

#include "convert.hpp"

#include <ql/experimental/math/hybridsimulatedannealing.hpp>
#include <ql/math/optimization/constraint.hpp>
#include <ql/math/optimization/costfunction.hpp>
#include <ql/math/optimization/endcriteria.hpp>
#include <ql/math/optimization/levenbergmarquardt.hpp>
#include <ql/math/optimization/problem.hpp>

#include <cstdint>
#include <string>
#include <utility>

using namespace QuantLib;

namespace harness {

    namespace {

        class LoggedSphere : public CostFunction {
          public:
            Real value(const Array& x) const override {
                Array r = values(x);
                return r[0] * r[0] + r[1] * r[1];
            }
            Array values(const Array& x) const override {
                for (Real xi : x)
                    log_.emplace_back(xi);
                Array r(2);
                r[0] = x[0] - 1.2;
                r[1] = x[1] + 0.7;
                return r;
            }
            mutable json::array log_;
        };

        template <class S, class P, class T, class R>
        json::value run(const S& sampler, const P& probability, T temperature, const R& reannealing, bool lm,
                        Size maxIterations, Size maxStationary) {
            using Annealing = HybridSimulatedAnnealing<S, P, T, R>;
            ext::shared_ptr<OptimizationMethod> local;
            if (lm)
                local = ext::make_shared<LevenbergMarquardt>();
            Annealing annealing(sampler, probability, std::move(temperature), reannealing, 1.0, 0.01, 5,
                                Annealing::ResetToBestPoint, 7, local,
                                lm ? Annealing::EveryBestPoint : Annealing::NoLocalOptimize);
            LoggedSphere cost;
            NoConstraint constraint;
            Array start(2);
            start[0] = 3.0;
            start[1] = 2.0;
            Problem problem(cost, constraint, start);
            EndCriteria ec(maxIterations, maxStationary, 1e-8, 1e-8, 1e-8);
            EndCriteria::Type type = annealing.minimize(problem, ec);
            json::object out;
            json::array x;
            for (Real xi : problem.currentValue())
                x.emplace_back(xi);
            out["x"] = x;
            out["value"] = problem.functionValue();
            out["evaluations"] = static_cast<std::int64_t>(problem.functionEvaluation());
            out["endCriteria"] = static_cast<std::int64_t>(type);
            out["log"] = cost.log_;
            return json::value(out);
        }

        const Register unit({
            {"HybridSimulatedAnnealing::minimize",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::string combination = text(a[0]);
                 std::string local = text(a[1]);
                 Size maxIterations = static_cast<Size>(integer(a[2])), maxStationary = static_cast<Size>(integer(a[3]));
                 if (local != "none" && local != "lm")
                     throw InvalidParams("unknown local optimizer " + local);
                 bool lm = local == "lm";
                 return [=] {
                     Array lower(2, -5.0), upper(2, 5.0);
                     if (combination == "cauchy")
                         return run(SamplerCauchy(42), ProbabilityAlwaysDownhill(), TemperatureCauchy(1.0, 2),
                                    ReannealingTrivial(), lm, maxIterations, maxStationary);
                     if (combination == "ring")
                         return run(SamplerRingGaussian(lower, upper, 42), ProbabilityBoltzmann(43),
                                    TemperatureBoltzmann(1.0, 2), ReannealingFiniteDifferences(1.0, 2, lower, upper),
                                    lm, maxIterations, maxStationary);
                     if (combination == "veryFast")
                         return run(SamplerVeryFastAnnealing(lower, upper, 42), ProbabilityBoltzmannDownhill(43),
                                    TemperatureVeryFastAnnealing(1.0, 0.01, 100.0, 2), ReannealingTrivial(), lm,
                                    maxIterations, maxStationary);
                     if (combination == "cauchy1D")
                         return run(SamplerGaussian(42), ProbabilityBoltzmannDownhill(43), TemperatureCauchy1D(1.0, 2),
                                    ReannealingFiniteDifferences(1.0, 2, lower, upper), lm, maxIterations,
                                    maxStationary);
                     throw InvalidParams("unknown combination " + combination);
                 };
             }},
        });

    }

}
