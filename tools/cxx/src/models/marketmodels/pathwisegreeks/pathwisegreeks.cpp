// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0
//
// The market-model Greeks (ql/models/marketmodels/pathwisediscounter, pathwiseaccountingengine, proxygreekengine and
// pathwisegreeks/): read back by tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MarketModelGreeksCxxInputs.cs.
//
// "MarketModelPathwiseDiscounter.factors" takes (paymentTime, rateTimes, discounts, step), the discounts a matrix of
// P(t_0, t_j) rows, and gives the factors. "PathwiseAccountingEngine.values" takes (model, generator, numeraires,
// product, initialNumeraireValue, paths), a log-normal Euler evolver on the model and a pathwise product, and gives
// {"prices", "priceErrors"}, the products' values' means and error estimates; "PathwiseVegasAccountingEngine.values"
// takes the same with the vega bumps (a list per step of matrices) before the initial numeraire value and "outer" after
// the paths, and gives the same of its results. "ProxyGreekEngine.values" takes (model, generator, numeraires, groups,
// diffWeights, startIndexOfConstraint, endIndexOfConstraint, product, initialNumeraireValue, paths), each group a list
// of bumped models for constrained Euler evolvers, and gives {"mean", "errorEstimate", "greeks"}, the greeks' means by
// group and weights; each constrained evolver's setConstraintType is called again before each step, so each step's
// covariances are its own pseudo-root's, nanoql's fix (every model's displacements equal).
// "RatePseudoRootJacobian.bumps" takes (kind, pseudoRoot, aliveIndex, numeraire, taus, pseudoBumps, displacements,
// oldRates, oneStepDFs, newRates, gaussians), kind "Numerical", "Analytic" or "AllElements", and gives B (a matrix, or
// a matrix a rate). "SwaptionPseudoDerivative.values" takes (model, startIndex, endIndex); "CapPseudoDerivative.values"
// (model, strike, startIndex, endIndex, firstDF); "VegaBumpCluster.checks" (cluster, comparee, model), each cluster six
// sizes; "VegaBumpCollection.bumps" (model, factorwise); "VolatilityBumpInstrumentJacobian.values" (model, factorwise,
// swaptions, caps), each swaption [start, end] and each cap [start, end, strike]; "OrthogonalizedBumpFinder.bumps" the
// same and (multiplierCutOff, tolerance).

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"
#include "models/marketmodels/products/productspec.hpp"

#include <ql/errors.hpp>
#include <ql/math/statistics/sequencestatistics.hpp>
#include <ql/models/marketmodels/constrainedevolver.hpp>
#include <ql/models/marketmodels/discounter.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdrateeuler.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdrateeulerconstrained.hpp>
#include <ql/models/marketmodels/pathwiseaccountingengine.hpp>
#include <ql/models/marketmodels/pathwisediscounter.hpp>
#include <ql/models/marketmodels/pathwisegreeks/bumpinstrumentjacobian.hpp>
#include <ql/models/marketmodels/pathwisegreeks/ratepseudorootjacobian.hpp>
#include <ql/models/marketmodels/pathwisegreeks/swaptionpseudojacobian.hpp>
#include <ql/models/marketmodels/pathwisegreeks/vegabumpcluster.hpp>
#include <ql/models/marketmodels/proxygreekengine.hpp>

#include <cstdint>
#include <memory>
#include <utility>
#include <valarray>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        std::vector<Matrix> matrices(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of matrices");
            std::vector<Matrix> out;
            for (const json::value& x : v.get_array())
                out.push_back(makeMatrix(matrix(x)));
            return out;
        }

        std::vector<std::vector<Matrix>> bumpLists(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a list of bumps per step");
            std::vector<std::vector<Matrix>> out;
            for (const json::value& x : v.get_array())
                out.push_back(matrices(x));
            return out;
        }

        json::value results(const std::vector<Matrix>& m) {
            json::array out;
            for (const Matrix& x : m)
                out.emplace_back(result(x));
            return out;
        }

        VegaBumpCluster cluster(const json::value& v) {
            std::vector<Size> c = sizes(v);
            if (c.size() != 6)
                throw InvalidParams("a cluster is six sizes");
            return {c[0], c[1], c[2], c[3], c[4], c[5]};
        }

        json::value clusterResult(const VegaBumpCluster& c) {
            return result(std::vector<Size>{c.factorBegin(), c.factorEnd(), c.rateBegin(), c.rateEnd(), c.stepBegin(),
                                            c.stepEnd()});
        }

        std::vector<VolatilityBumpInstrumentJacobian::Swaption> swaptions(const json::value& v) {
            std::vector<VolatilityBumpInstrumentJacobian::Swaption> out;
            for (const json::value& x : v.get_array()) {
                std::vector<Size> s = sizes(x);
                out.push_back({s[0], s[1]});
            }
            return out;
        }

        std::vector<VolatilityBumpInstrumentJacobian::Cap> caps(const json::value& v) {
            std::vector<VolatilityBumpInstrumentJacobian::Cap> out;
            for (const json::value& x : v.get_array()) {
                std::vector<Real> c = reals(x);
                out.push_back({static_cast<Size>(c[0]), static_cast<Size>(c[1]), c[2]});
            }
            return out;
        }

        // QuantLib's constrained Euler evolver with setConstraintType called again before each step: each step's
        // covariances its own pseudo-root's.
        class StepConstrained : public ConstrainedEvolver {
          public:
            explicit StepConstrained(ext::shared_ptr<LogNormalFwdRateEulerConstrained> e) : e_(std::move(e)) {}
            const std::vector<Size>& numeraires() const override { return e_->numeraires(); }
            Real startNewPath() override { return e_->startNewPath(); }
            Real advanceStep() override {
                e_->setConstraintType(start_, end_);
                return e_->advanceStep();
            }
            Size currentStep() const override { return e_->currentStep(); }
            const CurveState& currentState() const override { return e_->currentState(); }
            void setInitialState(const CurveState& cs) override { e_->setInitialState(cs); }
            void setConstraintType(const std::vector<Size>& start, const std::vector<Size>& end) override {
                start_ = start;
                end_ = end;
                e_->setConstraintType(start, end);
            }
            void setThisConstraint(const std::vector<Rate>& c, const std::valarray<bool>& active) override {
                e_->setThisConstraint(c, active);
            }

          private:
            ext::shared_ptr<LogNormalFwdRateEulerConstrained> e_;
            std::vector<Size> start_, end_;
        };

        ext::shared_ptr<LogNormalFwdRateEuler> euler(const ModelSpec& m, const GeneratorSpec& g,
                                                     const std::vector<Size>& numeraires) {
            std::unique_ptr<BrownianGeneratorFactory> f = makeFactory(g);
            return ext::make_shared<LogNormalFwdRateEuler>(makeModel(m), *f, numeraires);
        }

        // Means and error estimates of the prices: a product's value at its index times entries.
        json::value prices(const std::vector<Real>& means, const std::vector<Real>& errors, Size products,
                           Size entries) {
            std::vector<Real> p, e;
            for (Size i = 0; i < products; ++i) {
                p.push_back(means[i * entries]);
                e.push_back(errors[i * entries]);
            }
            json::object out;
            out["prices"] = result(p);
            out["priceErrors"] = result(e);
            return out;
        }

        const Register unit({
            {"MarketModelPathwiseDiscounter.factors",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Real t = real(a[0]);
                 std::vector<Real> rateTimes = reals(a[1]);
                 Matrix discounts = makeMatrix(matrix(a[2]));
                 Size step = size(a[3]);
                 if (discounts.columns() != rateTimes.size() || step >= discounts.rows())
                     throw InvalidParams("a row of discounts a step, one a rate time");
                 return [=] {
                     MarketModelPathwiseDiscounter d(t, rateTimes);
                     std::vector<Real> factors(rateTimes.size());
                     d.getFactors(Matrix(discounts.rows(), rateTimes.size() - 1, 0.0), discounts, step, factors);
                     return result(factors);
                 };
             }},
            {"PathwiseAccountingEngine.values",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 ModelSpec m = model(a[0]);
                 GeneratorSpec g = generator(a[1]);
                 std::vector<Size> numeraires = sizes(a[2]);
                 ProductSpec p = pathwiseProduct(a[3]);
                 Real initial = real(a[4]);
                 Size paths = size(a[5]);
                 return [=] {
                     std::unique_ptr<MarketModelPathwiseMultiProduct> product = makePathwiseProduct(p);
                     ext::shared_ptr<MarketModel> mm = makeModel(m);
                     PathwiseAccountingEngine engine(euler(m, g, numeraires), *product, mm, initial);
                     SequenceStatisticsInc stats(product->numberOfProducts() * (mm->numberOfRates() + 1));
                     engine.multiplePathValues(stats, paths);
                     return prices(stats.mean(), stats.errorEstimate(), product->numberOfProducts(), 1);
                 };
             }},
            {"PathwiseVegasAccountingEngine.values",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 ModelSpec m = model(a[0]);
                 GeneratorSpec g = generator(a[1]);
                 std::vector<Size> numeraires = sizes(a[2]);
                 ProductSpec p = pathwiseProduct(a[3]);
                 std::vector<std::vector<Matrix>> bumps = bumpLists(a[4]);
                 Real initial = real(a[5]);
                 Size paths = size(a[6]);
                 bool outer = boolean(a[7]);
                 return [=] {
                     std::unique_ptr<MarketModelPathwiseMultiProduct> product = makePathwiseProduct(p);
                     ext::shared_ptr<MarketModel> mm = makeModel(m);
                     std::vector<Real> means, errors;
                     if (outer) {
                         PathwiseVegasOuterAccountingEngine engine(euler(m, g, numeraires), *product, mm, bumps,
                                                                   initial);
                         engine.multiplePathValues(means, errors, paths);
                     } else {
                         PathwiseVegasAccountingEngine engine(euler(m, g, numeraires), *product, mm, bumps, initial);
                         engine.multiplePathValues(means, errors, paths);
                     }
                     Size entries = 1 + mm->numberOfRates() + bumps.front().size();
                     return prices(means, errors, product->numberOfProducts(), entries);
                 };
             }},
            {"ProxyGreekEngine.values",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 ModelSpec m = model(a[0]);
                 GeneratorSpec g = generator(a[1]);
                 std::vector<Size> numeraires = sizes(a[2]);
                 if (!a[3].is_array() || !a[4].is_array())
                     throw InvalidParams("expected the groups' models and weights");
                 std::vector<std::vector<ModelSpec>> groups;
                 for (const json::value& x : a[3].get_array()) {
                     groups.emplace_back();
                     for (const json::value& y : x.get_array())
                         groups.back().push_back(model(y));
                 }
                 std::vector<std::vector<std::vector<Real>>> weights;
                 for (const json::value& x : a[4].get_array()) {
                     weights.emplace_back();
                     for (const json::value& y : x.get_array())
                         weights.back().push_back(reals(y));
                 }
                 std::vector<Size> start = sizes(a[5]), end = sizes(a[6]);
                 ProductSpec p = product(a[7]);
                 Real initial = real(a[8]);
                 Size paths = size(a[9]);
                 return [=] {
                     std::unique_ptr<MarketModelMultiProduct> pr = makeProduct(p);
                     Size n = pr->numberOfProducts();
                     std::unique_ptr<BrownianGeneratorFactory> f = makeFactory(g);
                     std::vector<std::vector<ext::shared_ptr<ConstrainedEvolver>>> evolvers;
                     std::vector<std::vector<SequenceStatisticsInc>> greekStats;
                     for (Size j = 0; j < groups.size(); ++j) {
                         evolvers.emplace_back();
                         for (const ModelSpec& b : groups[j]) {
                             auto e = ext::make_shared<StepConstrained>(
                                 ext::make_shared<LogNormalFwdRateEulerConstrained>(makeModel(b), *f, numeraires));
                             e->setConstraintType(start, end);
                             evolvers.back().push_back(e);
                         }
                         greekStats.emplace_back(j < weights.size() ? weights[j].size() : 0,
                                                 SequenceStatisticsInc(n));
                     }
                     ProxyGreekEngine engine(ext::make_shared<LogNormalFwdRateEuler>(makeModel(m), *f, numeraires),
                                             evolvers, weights, start, end, *pr, initial);
                     SequenceStatisticsInc stats(n);
                     engine.multiplePathValues(stats, greekStats, paths);
                     json::array greeks;
                     for (Size j = 0; j < weights.size(); ++j) {
                         json::array group;
                         for (const SequenceStatisticsInc& s : greekStats[j])
                             group.emplace_back(result(s.mean()));
                         greeks.emplace_back(group);
                     }
                     json::object out;
                     out["mean"] = result(stats.mean());
                     out["errorEstimate"] = result(stats.errorEstimate());
                     out["greeks"] = greeks;
                     return json::value(out);
                 };
             }},
            {"RatePseudoRootJacobian.bumps",
             [](const Args& a) -> Call {
                 arity(a, 11);
                 std::string kind = text(a[0]);
                 Matrix root = makeMatrix(matrix(a[1]));
                 Size alive = size(a[2]), numeraire = size(a[3]);
                 std::vector<Real> taus = reals(a[4]);
                 std::vector<Matrix> bumps = matrices(a[5]);
                 std::vector<Real> displacements = reals(a[6]), oldRates = reals(a[7]), dfs = reals(a[8]),
                                   newRates = reals(a[9]), gaussians = reals(a[10]);
                 Size n = taus.size();
                 if (root.rows() != n || displacements.size() != n || oldRates.size() != n || newRates.size() != n ||
                     dfs.size() != n + 1 || gaussians.size() != root.columns() || alive >= n)
                     throw InvalidParams("the rates', the factors' and the discount factors' sizes");
                 return [=] {
                     if (kind == "AllElements") {
                         RatePseudoRootJacobianAllElements j(root, alive, numeraire, taus, displacements);
                         std::vector<Matrix> b(n, Matrix(n, root.columns(), 0.0));
                         j.getBumps(oldRates, dfs, newRates, gaussians, b);
                         return results(b);
                     }
                     Matrix b(bumps.size(), n, 0.0);
                     if (kind == "Numerical") {
                         RatePseudoRootJacobianNumerical j(root, alive, numeraire, taus, bumps, displacements);
                         j.getBumps(oldRates, dfs, newRates, gaussians, b);
                     } else {
                         RatePseudoRootJacobian j(root, alive, numeraire, taus, bumps, displacements);
                         j.getBumps(oldRates, dfs, newRates, gaussians, b);
                     }
                     return result(b);
                 };
             }},
            {"SwaptionPseudoDerivative.values",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 ModelSpec m = model(a[0]);
                 Size start = size(a[1]), end = size(a[2]);
                 return [=] {
                     ext::shared_ptr<MarketModel> mm = makeModel(m);
                     SwaptionPseudoDerivative d(mm, start, end);
                     std::vector<Matrix> variance, volatility;
                     for (Size i = 0; i < mm->numberOfSteps(); ++i) {
                         variance.push_back(d.varianceDerivative(i));
                         volatility.push_back(d.volatilityDerivative(i));
                     }
                     json::object out;
                     out["impliedVolatility"] = d.impliedVolatility();
                     out["variance"] = d.variance();
                     out["expiry"] = d.expiry();
                     out["varianceDerivatives"] = results(variance);
                     out["volatilityDerivatives"] = results(volatility);
                     return json::value(out);
                 };
             }},
            {"CapPseudoDerivative.values",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 ModelSpec m = model(a[0]);
                 Real strike = real(a[1]);
                 Size start = size(a[2]), end = size(a[3]);
                 Real firstDF = real(a[4]);
                 return [=] {
                     ext::shared_ptr<MarketModel> mm = makeModel(m);
                     CapPseudoDerivative d(mm, strike, start, end, firstDF);
                     std::vector<Matrix> price, volatility;
                     for (Size i = 0; i < mm->numberOfSteps(); ++i) {
                         price.push_back(d.priceDerivative(i));
                         volatility.push_back(d.volatilityDerivative(i));
                     }
                     json::object out;
                     out["impliedVolatility"] = d.impliedVolatility();
                     out["priceDerivatives"] = results(price);
                     out["volatilityDerivatives"] = results(volatility);
                     return json::value(out);
                 };
             }},
            {"VegaBumpCluster.checks",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 VegaBumpCluster c = cluster(a[0]), d = cluster(a[1]);
                 ModelSpec m = model(a[2]);
                 return [=] {
                     ext::shared_ptr<MarketModel> mm = makeModel(m);
                     json::object out;
                     out["doesIntersect"] = c.doesIntersect(d);
                     out["isCompatible"] = c.isCompatible(mm);
                     return json::value(out);
                 };
             }},
            {"VegaBumpCollection.bumps",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 ModelSpec m = model(a[0]);
                 bool factorwise = boolean(a[1]);
                 return [=] {
                     VegaBumpCollection c(makeModel(m), factorwise);
                     json::array bumps;
                     for (const VegaBumpCluster& b : c.allBumps())
                         bumps.emplace_back(clusterResult(b));
                     json::object out;
                     out["numberBumps"] = static_cast<std::int64_t>(c.numberBumps());
                     out["allBumps"] = bumps;
                     out["isFull"] = c.isFull();
                     out["isNonOverlapping"] = c.isNonOverlapping();
                     out["isSensible"] = c.isSensible();
                     return json::value(out);
                 };
             }},
            {"VolatilityBumpInstrumentJacobian.values",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 ModelSpec m = model(a[0]);
                 bool factorwise = boolean(a[1]);
                 auto s = swaptions(a[2]);
                 auto c = caps(a[3]);
                 return [=] {
                     VegaBumpCollection bumps(makeModel(m), factorwise);
                     VolatilityBumpInstrumentJacobian j(bumps, s, c);
                     json::array derivatives, onePercent;
                     for (Size i = 0; i < s.size() + c.size(); ++i) {
                         derivatives.emplace_back(result(j.derivativesVolatility(i)));
                         onePercent.emplace_back(result(j.onePercentBump(i)));
                     }
                     json::object out;
                     out["derivativesVolatility"] = derivatives;
                     out["onePercentBumps"] = onePercent;
                     out["allOnePercentBumps"] = result(j.getAllOnePercentBumps());
                     return json::value(out);
                 };
             }},
            {"OrthogonalizedBumpFinder.bumps",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 ModelSpec m = model(a[0]);
                 bool factorwise = boolean(a[1]);
                 auto s = swaptions(a[2]);
                 auto c = caps(a[3]);
                 Real cutOff = real(a[4]), tolerance = real(a[5]);
                 return [=] {
                     VegaBumpCollection bumps(makeModel(m), factorwise);
                     OrthogonalizedBumpFinder finder(bumps, s, c, cutOff, tolerance);
                     std::vector<std::vector<Matrix>> theBumps;
                     finder.GetVegaBumps(theBumps);
                     json::array out;
                     for (const auto& step : theBumps)
                         out.emplace_back(results(step));
                     return json::value(out);
                 };
             }},
        });

    }

}
