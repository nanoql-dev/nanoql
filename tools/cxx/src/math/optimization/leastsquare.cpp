// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/leastsquare.hpp, .cpp, which the wheel doesn't expose, on a harness problem: the fit of
// a·exp(b·x) to points (x, y), its gradient in (a, b). "LeastSquareFunction" takes (xs, ys, point): the cost function's
// value, values and gradient at the point. "NonLinearLeastSquare.perform" takes (xs, ys, initial, accuracy, maxiter):
// the fit by the default conjugate gradient without a constraint, its results, residual norm, last value and exit flag.

#include "convert.hpp"

#include <ql/math/optimization/constraint.hpp>
#include <ql/math/optimization/leastsquare.hpp>

#include <cmath>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        class ExponentialFit : public LeastSquareProblem {
          public:
            ExponentialFit(std::vector<Real> xs, std::vector<Real> ys) : xs_(std::move(xs)), ys_(std::move(ys)) {}
            Size size() override { return xs_.size(); }
            void targetAndValue(const Array& x, Array& target, Array& fct2fit) override {
                for (Size i = 0; i < xs_.size(); ++i) {
                    target[i] = ys_[i];
                    fct2fit[i] = x[0] * std::exp(x[1] * xs_[i]);
                }
            }
            void targetValueAndGradient(const Array& x, Matrix& grad, Array& target, Array& fct2fit) override {
                for (Size i = 0; i < xs_.size(); ++i) {
                    Real e = std::exp(x[1] * xs_[i]);
                    target[i] = ys_[i];
                    fct2fit[i] = x[0] * e;
                    grad[i][0] = e;
                    grad[i][1] = x[0] * xs_[i] * e;
                }
            }

          private:
            std::vector<Real> xs_, ys_;
        };

        json::value array(const Array& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return out;
        }

        void points(const std::vector<Real>& xs, const std::vector<Real>& ys) {
            if (xs.empty() || xs.size() != ys.size())
                throw InvalidParams("expected points (x, y)");
        }

        const Register unit({
            {"LeastSquareFunction",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 std::vector<Real> xs = reals(a[0]), ys = reals(a[1]), p = reals(a[2]);
                 points(xs, ys);
                 if (p.size() != 2)
                     throw InvalidParams("expected a point (a, b)");
                 return [=] {
                     ExponentialFit problem(xs, ys);
                     LeastSquareFunction f(problem);
                     Array x(p.begin(), p.end()), grad(2);
                     json::object out;
                     out["value"] = f.value(x);
                     out["values"] = array(f.values(x));
                     f.gradient(grad, x);
                     out["gradient"] = array(grad);
                     return json::value(out);
                 };
             }},
            {"NonLinearLeastSquare.perform",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::vector<Real> xs = reals(a[0]), ys = reals(a[1]), p = reals(a[2]);
                 points(xs, ys);
                 if (p.size() != 2)
                     throw InvalidParams("expected an initial (a, b)");
                 Real accuracy = real(a[3]);
                 Integer maxiter = integer(a[4]);
                 if (maxiter < 0)
                     throw InvalidParams("expected a non-negative iteration count");
                 return [=] {
                     ExponentialFit problem(xs, ys);
                     NoConstraint c;
                     NonLinearLeastSquare nnls(c, accuracy, static_cast<Size>(maxiter));
                     nnls.setInitialValue(Array(p.begin(), p.end()));
                     Array results = nnls.perform(problem);
                     json::object out;
                     out["results"] = array(results);
                     out["residualNorm"] = nnls.residualNorm();
                     out["lastValue"] = nnls.lastValue();
                     out["exitFlag"] = static_cast<Integer>(nnls.exitFlag());
                     return json::value(out);
                 };
             }},
        });

    }

}
