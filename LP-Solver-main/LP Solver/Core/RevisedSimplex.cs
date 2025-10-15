using LP_Solver.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LP_Solver.Core
{
    internal class RevisedSimplex : ISolver
    {
        public LPResult Solve(LpModel model, SolverOptions options, IReporter reporter)
        {
            // Clear console at the start of each solve
            Console.Clear();

            if (reporter == null) reporter = new ConsoleReporter();
            if (options == null) options = new SolverOptions();

            int m = model.NumCons;
            int n = model.NumVars;
            int totalVars = n + m; // include slack variables

            // Build augmented matrix
            double[,] A = new double[m, totalVars];
            double[] b = new double[m];
            double[] c = new double[totalVars];

            for (int i = 0; i < m; i++)
            {
                var cons = model.Constraints[i];
                for (int j = 0; j < n; j++)
                    A[i, j] = cons.Coefficients[j];
                A[i, n + i] = 1; // slack variable
                b[i] = cons.RHS;
            }

            for (int j = 0; j < n; j++)
                c[j] = model.IsMaximization ? -model.ObjectiveCoeffs[j] : model.ObjectiveCoeffs[j];

            // Print canonical form once
            reporter.WriteLine("=== Revised Simplex Method ===");
            reporter.WriteLine("---- === Canonical Form === ----");
            reporter.WriteLine(TableauToString(A, b, c, m, totalVars));

            // Initial basis = slack variables
            List<int> basis = new List<int>();
            for (int i = 0; i < m; i++) basis.Add(n + i);

            double[] xB = new double[m];
            for (int i = 0; i < m; i++) xB[i] = b[i];

            int iter = 0;

            while (true)
            {
                iter++;
                reporter.WriteLine($"\n--- Iteration {iter} ---");

                double[,] B = ExtractColumns(A, basis);
                double[,] Binv = Inverse(B);

                xB = Multiply(Binv, b);

                double[] cB = basis.Select(idx => c[idx]).ToArray();
                double[] lambda = Multiply(Transpose(Binv), cB);

                double[] reducedCosts = new double[totalVars];
                for (int j = 0; j < totalVars; j++)
                    reducedCosts[j] = c[j] - Dot(lambda, GetColumn(A, j));

                reporter.WriteLine("Basic solution xB: " + string.Join(", ", xB.Select(FormatNumber)));
                reporter.WriteLine("Price Out (?): " + string.Join(", ", lambda.Select(FormatNumber)));
                reporter.WriteLine("Reduced costs: " + string.Join(", ", reducedCosts.Select(FormatNumber)));

                int entering = -1;
                double mostNegative = -1e-9;
                for (int j = 0; j < totalVars; j++)
                {
                    if (!basis.Contains(j) && reducedCosts[j] < mostNegative)
                    {
                        mostNegative = reducedCosts[j];
                        entering = j;
                    }
                }

                if (entering == -1) break; // Optimal solution found

                double[] d = Multiply(Binv, GetColumn(A, entering));

                double minRatio = double.PositiveInfinity;
                int leavingIndex = -1;
                for (int i = 0; i < m; i++)
                {
                    if (d[i] > 1e-9)
                    {
                        double ratio = xB[i] / d[i];
                        if (ratio < minRatio)
                        {
                            minRatio = ratio;
                            leavingIndex = i;
                        }
                    }
                }

                if (leavingIndex == -1)
                {
                    reporter.WriteLine("Unbounded problem.");
                    return new LPResult
                    {
                        Status = LPStatus.Unbounded,
                        ObjectiveValue = double.PositiveInfinity,
                        VariableValues = null,
                        Log = reporter.GetLog()
                    };
                }

                int leaving = basis[leavingIndex];
                reporter.WriteLine($"Entering variable: x{entering + 1}");
                reporter.WriteLine($"Leaving variable: x{leaving + 1}");

                basis[leavingIndex] = entering;
            }

            // Construct final solution
            double[] solution = new double[totalVars];
            for (int i = 0; i < m; i++)
                solution[basis[i]] = xB[i];

            double obj = 0;
            for (int j = 0; j < n; j++)
                obj += -c[j] * solution[j]; // reverse sign for maximization

            reporter.WriteLine("\n---- Optimal Solution ----");
            reporter.WriteLine($"Objective value (z) = {FormatNumber(obj)}");
            reporter.WriteLine("Variable values:");
            for (int i = 0; i < n; i++)
                reporter.WriteLine($"x{i + 1} = {FormatNumber(solution[i])}");

            return new LPResult
            {
                Status = LPStatus.Optimal,
                ObjectiveValue = obj,
                VariableValues = solution.Take(n).ToArray(),
                Log = reporter.GetLog()
            };
        }

        // --- Helper Methods ---
        private string FormatNumber(double value)
        {
            value = Math.Abs(value) < 1e-9 ? 0.0 : value;
            return Math.Abs(value - Math.Round(value)) < 1e-9
                ? Math.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }

        private double[,] ExtractColumns(double[,] A, List<int> cols)
        {
            int m = A.GetLength(0);
            int k = cols.Count;
            double[,] result = new double[m, k];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < k; j++)
                    result[i, j] = A[i, cols[j]];
            return result;
        }

        private double[] GetColumn(double[,] A, int col)
        {
            int m = A.GetLength(0);
            double[] result = new double[m];
            for (int i = 0; i < m; i++) result[i] = A[i, col];
            return result;
        }

        private double Dot(double[] a, double[] b)
        {
            double sum = 0;
            for (int i = 0; i < a.Length; i++) sum += a[i] * b[i];
            return sum;
        }

        private double[] Multiply(double[,] A, double[] x)
        {
            int m = A.GetLength(0);
            int n = A.GetLength(1);
            double[] result = new double[m];
            for (int i = 0; i < m; i++)
            {
                double sum = 0;
                for (int j = 0; j < n; j++) sum += A[i, j] * x[j];
                result[i] = sum;
            }
            return result;
        }

        private double[,] Transpose(double[,] A)
        {
            int m = A.GetLength(0);
            int n = A.GetLength(1);
            double[,] T = new double[n, m];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++)
                    T[j, i] = A[i, j];
            return T;
        }

        private double[,] Inverse(double[,] A)
        {
            int n = A.GetLength(0);
            double[,] result = new double[n, n];
            double[,] temp = new double[n, n * 2];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) temp[i, j] = A[i, j];
                temp[i, n + i] = 1;
            }

            for (int i = 0; i < n; i++)
            {
                double diag = temp[i, i];
                if (Math.Abs(diag) < 1e-9) throw new Exception("Matrix is singular");
                for (int j = 0; j < n * 2; j++) temp[i, j] /= diag;
                for (int k = 0; k < n; k++)
                {
                    if (k == i) continue;
                    double factor = temp[k, i];
                    for (int j = 0; j < n * 2; j++)
                        temp[k, j] -= factor * temp[i, j];
                }
            }

            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    result[i, j] = temp[i, n + j];

            return result;
        }

        private string TableauToString(double[,] A, double[] b, double[] c, int m, int totalVars)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("   z:");
            for (int j = 0; j < c.Length; j++)
                sb.AppendFormat("{0,8}", FormatNumber(c[j]));
            sb.AppendLine();

            for (int i = 0; i < m; i++)
            {
                sb.Append("row" + (i + 1) + ":");
                for (int j = 0; j < totalVars; j++)
                    sb.AppendFormat("{0,8}", FormatNumber(A[i, j]));
                sb.AppendFormat("{0,8}", FormatNumber(b[i]));
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}