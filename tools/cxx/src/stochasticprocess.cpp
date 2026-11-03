// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/stochasticprocess.hpp: the members of StochasticProcess the wheel doesn't expose, called through a
// StochasticProcess reference: apply(x0, dx), which StochasticProcess1D implements on arrays of size 1, and time(d);
// and the base class's own defaults, evolve(t0, x0, dt, dw), apply(x0, dx) and factors(), which no 1-D process
// reaches, on the harness's multi-dimensional process below. A process argument is a spec: {"type":
// "OrnsteinUhlenbeckProcess", "speed", "volatility", "x0", "level"}, or {"type": "LinearProcess", "x0", "a",
// "sigma"}. An Array is a list of reals, a Matrix a list of its rows, a date its serial.
//
// QuantLib has no multi-dimensional process that keeps the base's evolve and apply and that nanoql ports, so the
// harness defines one, which tests/NanoQL.Tests defines too: LinearProcess, n independent geometric drifts x_i' =
// a_i x_i with a constant n x n diffusion sigma; its expectation is x0_i exp(a_i dt), its standard deviation sigma
// sqrt(dt) and its covariance that times its transpose, each hard-coded, so it needs no discretization.

#include "convert.hpp"

#include <ql/math/matrix.hpp>
#include <ql/processes/ornsteinuhlenbeckprocess.hpp>
#include <ql/stochasticprocess.hpp>

#include <cmath>

using namespace QuantLib;

namespace harness {

    namespace {

        class LinearProcess : public StochasticProcess {
          public:
            LinearProcess(Array x0, Array a, Matrix sigma)
            : x0_(std::move(x0)), a_(std::move(a)), sigma_(std::move(sigma)) {}
            Size size() const override { return x0_.size(); }
            Array initialValues() const override { return x0_; }
            Array drift(Time, const Array& x) const override {
                Array r(x.size());
                for (Size i = 0; i < x.size(); ++i)
                    r[i] = a_[i] * x[i];
                return r;
            }
            Matrix diffusion(Time, const Array&) const override { return sigma_; }
            Array expectation(Time, const Array& x0, Time dt) const override {
                Array r(x0.size());
                for (Size i = 0; i < x0.size(); ++i)
                    r[i] = x0[i] * std::exp(a_[i] * dt);
                return r;
            }
            Matrix stdDeviation(Time, const Array&, Time dt) const override { return sigma_ * std::sqrt(dt); }
            Matrix covariance(Time t0, const Array& x0, Time dt) const override {
                Matrix s = stdDeviation(t0, x0, dt);
                return s * transpose(s);
            }

          private:
            Array x0_, a_;
            Matrix sigma_;
        };

        struct ProcessSpec {
            bool linear;
            Real speed, volatility, x0, level;
            std::vector<Real> initial, a;
            std::vector<std::vector<Real>> sigma;
        };

        const json::value& key(const json::object& o, const char* name, const std::string& type) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(type + " needs " + name);
            return *v;
        }

        ProcessSpec processSpec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a process spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const json::value* type = o.if_contains("type");
            if (type == nullptr || !type->is_string())
                throw InvalidParams("a process spec needs a type: " + json::serialize(v));
            const std::string t(type->get_string());
            if (t == "OrnsteinUhlenbeckProcess") {
                for (const auto& kv : o) {
                    if (kv.key() != "type" && kv.key() != "speed" && kv.key() != "volatility" && kv.key() != "x0" &&
                        kv.key() != "level")
                        throw InvalidParams(t + " takes no key " + std::string(kv.key()));
                }
                return {false, real(key(o, "speed", t)), real(key(o, "volatility", t)), real(key(o, "x0", t)),
                        real(key(o, "level", t)), {}, {}, {}};
            }
            if (t == "LinearProcess") {
                for (const auto& kv : o) {
                    if (kv.key() != "type" && kv.key() != "x0" && kv.key() != "a" && kv.key() != "sigma")
                        throw InvalidParams(t + " takes no key " + std::string(kv.key()));
                }
                std::vector<Real> initial = reals(key(o, "x0", t)), a = reals(key(o, "a", t));
                const json::value& rows = key(o, "sigma", t);
                if (!rows.is_array())
                    throw InvalidParams("a LinearProcess's sigma is a list of rows, got " + json::serialize(rows));
                std::vector<std::vector<Real>> sigma;
                for (const json::value& row : rows.get_array())
                    sigma.push_back(reals(row));
                // The harness's own process: its shapes are the harness's to check, not QuantLib's.
                if (a.size() != initial.size() || sigma.size() != initial.size())
                    throw InvalidParams("a LinearProcess's x0, a and sigma have one row per dimension");
                for (const auto& row : sigma) {
                    if (row.size() != initial.size())
                        throw InvalidParams("a LinearProcess's sigma is square");
                }
                return {true, 0.0, 0.0, 0.0, 0.0, initial, a, sigma};
            }
            throw InvalidParams("unknown process " + t);
        }

        Array makeArray(const std::vector<Real>& x) { return Array(x.begin(), x.end()); }

        ext::shared_ptr<StochasticProcess> makeProcess(const ProcessSpec& s) {
            if (!s.linear)
                return ext::make_shared<OrnsteinUhlenbeckProcess>(s.speed, s.volatility, s.x0, s.level);
            const Size n = s.initial.size();
            Matrix sigma(n, n);
            for (Size i = 0; i < n; ++i)
                for (Size j = 0; j < n; ++j)
                    sigma[i][j] = s.sigma[i][j];
            return ext::make_shared<LinearProcess>(makeArray(s.initial), makeArray(s.a), sigma);
        }

        json::value arrayResult(const Array& x) {
            json::array out;
            for (Real r : x)
                out.emplace_back(r);
            return json::value(std::move(out));
        }

        const Register unit({
            {"StochasticProcess::size",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 ProcessSpec s = processSpec(a[0]);
                 return [=] { return json::value(static_cast<std::int64_t>(makeProcess(s)->size())); };
             }},
            {"StochasticProcess::factors",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 ProcessSpec s = processSpec(a[0]);
                 return [=] { return json::value(static_cast<std::int64_t>(makeProcess(s)->factors())); };
             }},
            {"StochasticProcess::evolve",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 ProcessSpec s = processSpec(a[0]);
                 Time t0 = real(a[1]);
                 std::vector<Real> x0 = reals(a[2]);
                 Time dt = real(a[3]);
                 std::vector<Real> dw = reals(a[4]);
                 return [=] { return arrayResult(makeProcess(s)->evolve(t0, makeArray(x0), dt, makeArray(dw))); };
             }},
            {"StochasticProcess::apply",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 ProcessSpec s = processSpec(a[0]);
                 std::vector<Real> x0 = reals(a[1]), dx = reals(a[2]);
                 return [=] { return arrayResult(makeProcess(s)->apply(makeArray(x0), makeArray(dx))); };
             }},
            {"StochasticProcess::time",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 ProcessSpec s = processSpec(a[0]);
                 DateSpec d = date(a[1]);
                 return [=] { return json::value(makeProcess(s)->time(makeDate(d))); };
             }},
        });

    }

}
