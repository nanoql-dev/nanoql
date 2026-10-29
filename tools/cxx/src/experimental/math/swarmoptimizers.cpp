// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/math/particleswarmoptimization.hpp, .cpp and fireflyalgorithm.hpp, .cpp: the particle swarm and the
// firefly algorithm, which the wheel doesn't bind, run where the S-xrt plan's fixes don't reach (XRR5 (xxviii)-(xxxii)
// and the run's R4-1 and R4-2), each cost function logging the points it evaluates.
//
// "ParticleSwarmOptimization::minimize" takes (M, form, coefficients, inertia, inertiaParameters, seed, lower, upper,
// center, maxIterations, maxStationary): the swarm on the topology below, the constriction form "constriction" [c1, c2]
// or the inertia form "inertia" [omega, c1, c2], the inertia "trivial" [], "simpleRandom" [threshold, seed] or
// "levyFlight" [alpha, threshold, seed], minimizing the sphere Σ(xᵢ − cᵢ)² centred at center within the bounds. The
// topology is the global topology as QuantLib's documents it, the best personal best every particle's social best,
// written here (and alike in the C# reader): QuantLib's own keeps the worst (XRR5 (xxviii)). It returns {"x", "value",
// "evaluations", "endCriteria", "log"}: the result, the count, EndCriteria::Type's integer and the evaluated points in
// turn.
//
// "FireflyAlgorithm::minimize" takes (M, Mde, mutation, crossover, seed, walk, walkParameters, intensity,
// intensityParameters, cost, lower, upper, center, maxIterations, maxStationary): the walk "gaussian" [sigma, delta,
// seed], "levy" [alpha, xm, delta, seed] or "decreasing" [sigma, delta, seed], the intensity "exponential" [beta0,
// betaMin, gamma] or "inverse" [beta0, betaMin], the cost "sphere" or "constant" (1 everywhere); it returns the same.
// One firefly and no differential evolution, or one firefly and three differential evolution members on the constant
// cost, keep the population sorted in its own order, where neither QuantLib's attraction loop nor its value updates
// differ from the fixed ones; the constant cost rejects every trial, which the log keeps.

#include "convert.hpp"

#include <ql/experimental/math/fireflyalgorithm.hpp>
#include <ql/experimental/math/particleswarmoptimization.hpp>
#include <ql/math/optimization/constraint.hpp>
#include <ql/math/optimization/costfunction.hpp>
#include <ql/math/optimization/endcriteria.hpp>
#include <ql/math/optimization/problem.hpp>

#include <cstdint>
#include <string>
#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        // the global topology as QuantLib's comment documents it
        class DocumentedGlobalTopology : public ParticleSwarmOptimization::Topology {
          public:
            void setSize(Size M) override { M_ = M; }
            void findSocialBest() override {
                Real bestF = (*pBF_)[0];
                Size bestP = 0;
                for (Size i = 1; i < M_; i++) {
                    if ((*pBF_)[i] < bestF) {
                        bestF = (*pBF_)[i];
                        bestP = i;
                    }
                }
                Array& x = (*pBX_)[bestP];
                for (Size i = 0; i < M_; i++) {
                    (*gBX_)[i] = x;
                    (*gBF_)[i] = bestF;
                }
            }

          private:
            Size M_ = 0;
        };

        class LoggedCost : public CostFunction {
          public:
            LoggedCost(Array center, bool constant) : center_(std::move(center)), constant_(constant) {}
            Real value(const Array& x) const override {
                for (Real xi : x)
                    log_.emplace_back(xi);
                if (constant_)
                    return 1.0;
                Real s = 0.0;
                for (Size i = 0; i < x.size(); ++i)
                    s += (x[i] - center_[i]) * (x[i] - center_[i]);
                return s;
            }
            Array values(const Array& x) const override { return Array(1, value(x)); }
            mutable json::array log_;

          private:
            Array center_;
            bool constant_;
        };

        Array array(const std::vector<Real>& v) { return Array(v.begin(), v.end()); }

        json::value run(OptimizationMethod& method, const std::vector<Real>& lower, const std::vector<Real>& upper,
                        const std::vector<Real>& center, bool constant, Size maxIterations, Size maxStationary) {
            LoggedCost cost(array(center), constant);
            NonhomogeneousBoundaryConstraint constraint(array(lower), array(upper));
            Problem problem(cost, constraint, array(center));
            EndCriteria ec(maxIterations, maxStationary, 1e-8, 1e-8, 1e-8);
            EndCriteria::Type type = method.minimize(problem, ec);
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
            {"ParticleSwarmOptimization::minimize",
             [](const Args& a) -> Call {
                 arity(a, 11);
                 Size M = static_cast<Size>(integer(a[0]));
                 std::string form = text(a[1]);
                 std::vector<Real> c = reals(a[2]);
                 std::string inertia = text(a[3]);
                 std::vector<Real> ip = reals(a[4]);
                 auto seed = static_cast<unsigned long>(integer(a[5]));
                 std::vector<Real> lower = reals(a[6]), upper = reals(a[7]), center = reals(a[8]);
                 Size maxIterations = static_cast<Size>(integer(a[9])), maxStationary = static_cast<Size>(integer(a[10]));
                 return [=] {
                     ext::shared_ptr<ParticleSwarmOptimization::Inertia> in;
                     if (inertia == "trivial")
                         in = ext::make_shared<TrivialInertia>();
                     else if (inertia == "simpleRandom")
                         in = ext::make_shared<SimpleRandomInertia>(ip[0], static_cast<unsigned long>(ip[1]));
                     else if (inertia == "levyFlight")
                         in = ext::make_shared<LevyFlightInertia>(ip[0], static_cast<Size>(ip[1]),
                                                                  static_cast<unsigned long>(ip[2]));
                     else
                         throw InvalidParams("unknown inertia " + inertia);
                     auto topology = ext::make_shared<DocumentedGlobalTopology>();
                     if (form == "constriction") {
                         ParticleSwarmOptimization pso(M, topology, in, c[0], c[1], seed);
                         return run(pso, lower, upper, center, false, maxIterations, maxStationary);
                     }
                     ParticleSwarmOptimization pso(M, topology, in, c[0], c[1], c[2], seed);
                     return run(pso, lower, upper, center, false, maxIterations, maxStationary);
                 };
             }},
            {"FireflyAlgorithm::minimize",
             [](const Args& a) -> Call {
                 arity(a, 15);
                 Size M = static_cast<Size>(integer(a[0])), Mde = static_cast<Size>(integer(a[1]));
                 Real mutation = real(a[2]), crossover = real(a[3]);
                 auto seed = static_cast<unsigned long>(integer(a[4]));
                 std::string walk = text(a[5]);
                 std::vector<Real> wp = reals(a[6]);
                 std::string intensity = text(a[7]);
                 std::vector<Real> ip = reals(a[8]);
                 std::string cost = text(a[9]);
                 std::vector<Real> lower = reals(a[10]), upper = reals(a[11]), center = reals(a[12]);
                 Size maxIterations = static_cast<Size>(integer(a[13])), maxStationary = static_cast<Size>(integer(a[14]));
                 return [=] {
                     ext::shared_ptr<FireflyAlgorithm::RandomWalk> w;
                     if (walk == "gaussian")
                         w = ext::make_shared<GaussianWalk>(wp[0], wp[1], static_cast<unsigned long>(wp[2]));
                     else if (walk == "levy")
                         w = ext::make_shared<LevyFlightWalk>(wp[0], wp[1], wp[2], static_cast<unsigned long>(wp[3]));
                     else if (walk == "decreasing")
                         w = ext::make_shared<DecreasingGaussianWalk>(wp[0], wp[1], static_cast<unsigned long>(wp[2]));
                     else
                         throw InvalidParams("unknown walk " + walk);
                     ext::shared_ptr<FireflyAlgorithm::Intensity> in;
                     if (intensity == "exponential")
                         in = ext::make_shared<ExponentialIntensity>(ip[0], ip[1], ip[2]);
                     else if (intensity == "inverse")
                         in = ext::make_shared<InverseLawSquareIntensity>(ip[0], ip[1]);
                     else
                         throw InvalidParams("unknown intensity " + intensity);
                     FireflyAlgorithm fa(M, in, w, Mde, mutation, crossover, seed);
                     return run(fa, lower, upper, center, cost == "constant", maxIterations, maxStationary);
                 };
             }},
        });

    }

}
