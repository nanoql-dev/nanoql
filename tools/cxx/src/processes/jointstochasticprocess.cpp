// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/processes/jointstochasticprocess.hpp, .cpp: the joint process's members, through the harness's own derived
// process, since QuantLib derives none the harness could reach; tests/NanoQL.Tests/Vectors/Cxx/Processes/
// JointStochasticProcessCxxVectorTests.cs mirrors it.
//
// A process is {"gbm", "heston", "ou", "correlation", "stateDependent", "factors"}: the joint process of a
// GeometricBrownianMotionProcess (gbm [initialValue, mue, sigma]), models/equity/hestonspec.hpp's Heston process and an
// OrnsteinUhlenbeckProcess (ou [speed, vol, x0, level]), in that order, with the factors given (null for QuantLib's
// default, the constituents' sum). Its preEvolve does nothing; its postEvolve returns the constituents' states with
// 1e-3 times the first correlated increment added to the first; its numeraire is exp(0.03 t); correlationIsStateDependent
// returns stateDependent; and crossModelCorrelation returns the correlation, a matrix of the process's size given as its
// rows, at every time and state.
//
// Each method takes the process first: "JointStochasticProcess::summary" () returns {"size", "factors",
// "initialValues"}; "::drift" (t, x), "::expectation" (t0, x0, dt), "::apply" (x0, dx), "::evolve" (t0, x0, dt, dw),
// each a fresh process's, return the state; "::diffusion" (t, x), "::covariance" (t0, x0, dt) and "::stdDeviation" (t0,
// x0, dt) return the matrix's rows flattened; "::numeraire" (t, x) returns the numeraire. A refusal is QuantLib's.

#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/math/array.hpp>
#include <ql/math/matrix.hpp>
#include <ql/processes/geometricbrownianprocess.hpp>
#include <ql/processes/jointstochasticprocess.hpp>
#include <ql/processes/ornsteinuhlenbeckprocess.hpp>

#include <cmath>
#include <initializer_list>
#include <string>
#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        class HarnessJointProcess : public JointStochasticProcess {
          public:
            HarnessJointProcess(std::vector<ext::shared_ptr<StochasticProcess>> l, Size factors, Matrix correlation,
                                bool stateDependent)
            : JointStochasticProcess(std::move(l), factors), correlation_(std::move(correlation)),
              stateDependent_(stateDependent) {}
            void preEvolve(Time, const Array&, Time, const Array&) const override {}
            Array postEvolve(Time, const Array&, Time, const Array& dw, const Array& y0) const override {
                Array y = y0;
                y[0] += 1e-3 * dw[0];
                return y;
            }
            DiscountFactor numeraire(Time t, const Array&) const override { return std::exp(0.03 * t); }
            bool correlationIsStateDependent() const override { return stateDependent_; }
            Matrix crossModelCorrelation(Time, const Array&) const override { return correlation_; }

          private:
            Matrix correlation_;
            bool stateDependent_;
        };

        struct ProcessSpec {
            std::vector<Real> gbm, ou;
            equity::ProcessSpec heston;
            std::vector<std::vector<Real>> correlation;
            bool stateDependent = false;
            Size factors = Null<Size>();
        };

        ProcessSpec process(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a joint process spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : {"gbm", "heston", "ou", "correlation", "stateDependent", "factors"})
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams("a joint process takes no key " + std::string(kv.key()));
            }
            const auto key = [&](const char* name) -> const json::value& {
                const json::value* x = o.if_contains(name);
                if (x == nullptr)
                    throw InvalidParams(std::string("a joint process spec needs ") + name);
                return *x;
            };
            ProcessSpec s;
            s.gbm = reals(key("gbm"));
            s.ou = reals(key("ou"));
            if (s.gbm.size() != 3 || s.ou.size() != 4)
                throw InvalidParams("gbm takes three parameters and ou four");
            s.heston = equity::process(key("heston"));
            if (!key("correlation").is_array())
                throw InvalidParams("expected the correlation's rows");
            for (const json::value& row : key("correlation").get_array())
                s.correlation.push_back(reals(row));
            s.stateDependent = boolean(key("stateDependent"));
            if (!key("factors").is_null()) {
                long long f = int64(key("factors"));
                if (f < 0)
                    throw InvalidParams("factors take a non-negative value");
                s.factors = static_cast<Size>(f);
            }
            return s;
        }

        ext::shared_ptr<HarnessJointProcess> makeProcess(const ProcessSpec& s) {
            std::vector<ext::shared_ptr<StochasticProcess>> l = {
                ext::make_shared<GeometricBrownianMotionProcess>(s.gbm[0], s.gbm[1], s.gbm[2]),
                equity::makeProcess(s.heston),
                ext::make_shared<OrnsteinUhlenbeckProcess>(s.ou[0], s.ou[1], s.ou[2], s.ou[3])};
            Matrix c(s.correlation.size(), s.correlation.empty() ? 0 : s.correlation[0].size());
            for (Size i = 0; i < c.rows(); ++i) {
                if (s.correlation[i].size() != c.columns())
                    throw InvalidParams("the correlation's rows have one length");
                for (Size j = 0; j < c.columns(); ++j)
                    c[i][j] = s.correlation[i][j];
            }
            return ext::make_shared<HarnessJointProcess>(l, s.factors, c, s.stateDependent);
        }

        json::value values(const Array& a) {
            json::array out;
            for (Real x : a)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        json::value flat(const Matrix& m) {
            json::array out;
            for (Size i = 0; i < m.rows(); ++i)
                for (Size j = 0; j < m.columns(); ++j)
                    out.emplace_back(m[i][j]);
            return json::value(std::move(out));
        }

        Array array(const std::vector<Real>& x) {
            return Array(x.begin(), x.end());
        }

        const Register unit({
            {"JointStochasticProcess::summary",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 ProcessSpec s = process(a[0]);
                 return [=] {
                     auto p = makeProcess(s);
                     json::object out;
                     out["size"] = p->size();
                     out["factors"] = p->factors();
                     out["initialValues"] = values(p->initialValues());
                     return json::value(std::move(out));
                 };
             }},
            {"JointStochasticProcess::drift",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 ProcessSpec s = process(a[0]);
                 Real t = real(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 return [=] { return values(makeProcess(s)->drift(t, array(x))); };
             }},
            {"JointStochasticProcess::expectation",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 ProcessSpec s = process(a[0]);
                 Real t0 = real(a[1]);
                 std::vector<Real> x0 = reals(a[2]);
                 Real dt = real(a[3]);
                 return [=] { return values(makeProcess(s)->expectation(t0, array(x0), dt)); };
             }},
            {"JointStochasticProcess::diffusion",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 ProcessSpec s = process(a[0]);
                 Real t = real(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 return [=] { return flat(makeProcess(s)->diffusion(t, array(x))); };
             }},
            {"JointStochasticProcess::covariance",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 ProcessSpec s = process(a[0]);
                 Real t0 = real(a[1]);
                 std::vector<Real> x0 = reals(a[2]);
                 Real dt = real(a[3]);
                 return [=] { return flat(makeProcess(s)->covariance(t0, array(x0), dt)); };
             }},
            {"JointStochasticProcess::stdDeviation",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 ProcessSpec s = process(a[0]);
                 Real t0 = real(a[1]);
                 std::vector<Real> x0 = reals(a[2]);
                 Real dt = real(a[3]);
                 return [=] { return flat(makeProcess(s)->stdDeviation(t0, array(x0), dt)); };
             }},
            {"JointStochasticProcess::apply",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 ProcessSpec s = process(a[0]);
                 std::vector<Real> x0 = reals(a[1]), dx = reals(a[2]);
                 return [=] { return values(makeProcess(s)->apply(array(x0), array(dx))); };
             }},
            {"JointStochasticProcess::evolve",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 ProcessSpec s = process(a[0]);
                 Real t0 = real(a[1]);
                 std::vector<Real> x0 = reals(a[2]);
                 Real dt = real(a[3]);
                 std::vector<Real> dw = reals(a[4]);
                 return [=] { return values(makeProcess(s)->evolve(t0, array(x0), dt, array(dw))); };
             }},
            {"JointStochasticProcess::numeraire",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 ProcessSpec s = process(a[0]);
                 Real t = real(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 return [=] { return json::value(makeProcess(s)->numeraire(t, array(x))); };
             }},
        });

    }

}
