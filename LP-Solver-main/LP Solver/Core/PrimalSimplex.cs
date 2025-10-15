using System;
using System.Text;
using LP_Solver.Core.Models;

namespace LP_Solver.Core
{
    public class PrimalSimplex : ISolver
    {
        private bool headerPrinted = false;

        // ========= ORIGINAL PRIMAL SIMPLEX (used by menu option 1) =========
        public LPResult Solve(LpModel model, SolverOptions options, IReporter reporter)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (reporter == null) reporter = new LP_Solver.IO.StringBuilderReporter();
            if (options == null) options = new SolverOptions();

            // print the solver header only once per process
            if (!headerPrinted)
            {
                reporter.WriteLine("Problem type: " + (model.IsMaximization ? "Maximization" : "Minimization"));
                reporter.WriteLine("Original variables (n): " + model.NumVars + ", Constraints (m): " + model.NumCons);
                reporter.WriteLine("");
                headerPrinted = true;
            }

            int n = model.NumVars;
            int m = model.NumCons;

            double[,] A = new double[m, n];
            double[] b = new double[m];
            string[] rel = new string[m];

            // Prepare constraints
            for (int i = 0; i < m; i++)
            {
                var cons = model.Constraints[i];
                for (int j = 0; j < n; j++) A[i, j] = cons.Coefficients[j];
                b[i] = cons.RHS;
                rel[i] = (cons.Relation ?? "<=").Trim();

                // ensure non-negative RHS
                if (b[i] < 0)
                {
                    for (int j = 0; j < n; j++) A[i, j] = -A[i, j];
                    b[i] = -b[i];
                    if (rel[i] == "<=") rel[i] = ">=";
                    else if (rel[i] == ">=") rel[i] = "<=";
                }
            }

            // Add slack/surplus
            int slackCount = 0;
            for (int i = 0; i < m; i++)
                if (rel[i] == "<=" || rel[i] == ">=") slackCount++;

            int totalCols = n + slackCount;
            double[,] Aext = new double[m, totalCols];

            int slackIndex = 0;
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++) Aext[i, j] = A[i, j];
                if (rel[i] == "<=")
                {
                    Aext[i, n + slackIndex] = 1.0;
                    slackIndex++;
                }
                else if (rel[i] == ">=")
                {
                    Aext[i, n + slackIndex] = -1.0;
                    slackIndex++;
                }
            }

            // Tableau: rows = m + 1 (objective row at index m), cols = totalCols + 1 (RHS)
            double[,] T = new double[m + 1, totalCols + 1];

            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < totalCols; j++) T[i, j] = Aext[i, j];
                T[i, totalCols] = b[i];
            }

            // Objective row in canonical form
            for (int j = 0; j < n; j++)
                T[m, j] = model.IsMaximization ? -model.ObjectiveCoeffs[j] : model.ObjectiveCoeffs[j];
            for (int j = n; j < totalCols; j++) T[m, j] = 0.0;
            T[m, totalCols] = 0.0;

            // Show canonical form
            reporter.WriteLine("---- === Canonical Form === ----");
            reporter.WriteLine(TableauToString(T, m, totalCols));

            // Initial basis: one slack per row (no artificials handled here)
            int[] basis = new int[m];
            for (int i = 0; i < m; i++) basis[i] = n + i;

            // Iterate
            int iter = 0;
            int maxIter = options.MaxIterations > 0 ? options.MaxIterations : 1000;
            const double EPS = 1e-12;

            while (true)
            {
                iter++;
                if (iter > maxIter)
                {
                    // save artifacts for diagnostics
                    SaveArtifactsForSpecialCases(T, basis, m, totalCols, model.IsMaximization);

                    return new LPResult
                    {
                        Status = LPStatus.IterationLimit,
                        ObjectiveValue = double.NaN,
                        VariableValues = new double[n],
                        Log = reporter.GetLog()
                    };
                }

                // Entering: most negative c̄j
                int entering = -1;
                double mostNeg = -EPS;
                for (int j = 0; j < totalCols; j++)
                {
                    if (T[m, j] < mostNeg)
                    {
                        mostNeg = T[m, j];
                        entering = j;
                    }
                }
                if (entering == -1) break; // optimal

                // Leaving: min ratio
                double minRatio = double.PositiveInfinity;
                int leaving = -1;
                for (int i = 0; i < m; i++)
                {
                    if (T[i, entering] > EPS)
                    {
                        double ratio = T[i, totalCols] / T[i, entering];
                        if (ratio < minRatio)
                        {
                            minRatio = ratio;
                            leaving = i;
                        }
                    }
                }
                if (leaving == -1)
                {
                    // save artifacts (unbounded)
                    SaveArtifactsForSpecialCases(T, basis, m, totalCols, model.IsMaximization);

                    return new LPResult
                    {
                        Status = LPStatus.Unbounded,
                        ObjectiveValue = double.PositiveInfinity,
                        VariableValues = new double[n],
                        Log = reporter.GetLog()
                    };
                }

                // Pivot
                double pivot = T[leaving, entering];
                for (int j = 0; j <= totalCols; j++) T[leaving, j] /= pivot;

                for (int i = 0; i <= m; i++)
                {
                    if (i == leaving) continue;
                    double factor = T[i, entering];
                    for (int j = 0; j <= totalCols; j++)
                        T[i, j] -= factor * T[leaving, j];
                }

                basis[leaving] = entering;

                if (options.ShowIterations)
                    reporter.WriteStep("Tableau Iteration " + iter, TableauToString(T, m, totalCols));
            }

            // Extract solution
            double[] xSol = new double[n];
            for (int i = 0; i < m; i++)
                if (basis[i] < n) xSol[basis[i]] = T[i, totalCols];

            // Objective value
            double objVal = 0.0;
            for (int j = 0; j < n; j++)
                objVal += model.ObjectiveCoeffs[j] * xSol[j];

            // Print final
            reporter.WriteLine("---- Optimal Solution ----");
            reporter.WriteLine("Objective value (z) = " + FormatNumber(objVal));
            reporter.WriteLine("Variable values:");
            for (int i = 0; i < n; i++)
                reporter.WriteLine("x" + (i + 1) + " = " + FormatNumber(xSol[i]));

            // save artifacts for Special Cases menu (bounded/optimal)
            SaveArtifactsForSpecialCases(T, basis, m, totalCols, model.IsMaximization);

            return new LPResult
            {
                Status = LPStatus.Optimal,
                ObjectiveValue = objVal,
                VariableValues = xSol,
                Log = reporter.GetLog()
            };
        }

        // ========= SENSITIVITY SOLVER (used only by menu option 6) =========
        public LPREsults SolveForSensitivity(LpModel model, SolverOptions options, IReporter reporter)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (reporter == null) reporter = new LP_Solver.IO.StringBuilderReporter();
            if (options == null) options = new SolverOptions();

            int n = model.NumVars;
            int m = model.NumCons;

            double[,] A = new double[m, n];
            double[] b = new double[m];
            string[] rel = new string[m];

            for (int i = 0; i < m; i++)
            {
                var cons = model.Constraints[i];
                for (int j = 0; j < n; j++)
                    A[i, j] = cons.Coefficients[j];

                b[i] = cons.RHS;
                rel[i] = (cons.Relation ?? "<=").Trim();

                if (b[i] < 0)
                {
                    for (int j = 0; j < n; j++) A[i, j] = -A[i, j];
                    b[i] = -b[i];
                    if (rel[i] == "<=") rel[i] = ">=";
                    else if (rel[i] == ">=") rel[i] = "<=";
                }
            }

            int slackCount = 0;
            for (int i = 0; i < m; i++)
                if (rel[i] == "<=" || rel[i] == ">=") slackCount++;

            int totalCols = n + slackCount;
            double[,] Aext = new double[m, totalCols];

            int slackK = 0;
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                    Aext[i, j] = A[i, j];

                if (rel[i] == "<=")
                {
                    Aext[i, n + slackK] = 1.0;
                    slackK++;
                }
                else if (rel[i] == ">=")
                {
                    Aext[i, n + slackK] = -1.0;
                    slackK++;
                }
            }

            double[,] T = new double[m + 1, totalCols + 1];
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < totalCols; j++) T[i, j] = Aext[i, j];
                T[i, totalCols] = b[i];
            }

            for (int j = 0; j < n; j++)
                T[m, j] = model.IsMaximization ? -model.ObjectiveCoeffs[j] : model.ObjectiveCoeffs[j];
            for (int j = n; j < totalCols; j++) T[m, j] = 0.0;
            T[m, totalCols] = 0.0;

            int[] basis = new int[m];
            for (int i = 0; i < m; i++) basis[i] = n + i;

            int iter = 0;
            int maxIter = options.MaxIterations > 0 ? options.MaxIterations : 1000;
            const double EPS = 1e-12;

            while (true)
            {
                iter++;
                if (iter > maxIter)
                {
                    return new LPREsults
                    {
                        Status = LPStatus.IterationLimit,
                        ObjectiveValue = double.NaN,
                        VariableValues = new double[n],
                        Log = reporter.GetLog(),
                        FinalTableau = BuildFinalTableau(T, basis, m, totalCols)
                    };
                }

                int entering = -1;
                double mostNeg = -EPS;
                for (int j = 0; j < totalCols; j++)
                {
                    if (T[m, j] < mostNeg)
                    {
                        mostNeg = T[m, j];
                        entering = j;
                    }
                }
                if (entering == -1) break;

                double minRatio = double.PositiveInfinity;
                int leaving = -1;
                for (int i = 0; i < m; i++)
                {
                    if (T[i, entering] > EPS)
                    {
                        double ratio = T[i, totalCols] / T[i, entering];
                        if (ratio < minRatio)
                        {
                            minRatio = ratio;
                            leaving = i;
                        }
                    }
                }
                if (leaving == -1)
                {
                    return new LPREsults
                    {
                        Status = LPStatus.Unbounded,
                        ObjectiveValue = double.PositiveInfinity,
                        VariableValues = new double[n],
                        Log = reporter.GetLog(),
                        FinalTableau = BuildFinalTableau(T, basis, m, totalCols)
                    };
                }

                double pivot = T[leaving, entering];
                for (int j = 0; j <= totalCols; j++) T[leaving, j] /= pivot;

                for (int i = 0; i <= m; i++)
                {
                    if (i == leaving) continue;
                    double factor = T[i, entering];
                    for (int j = 0; j <= totalCols; j++)
                        T[i, j] -= factor * T[leaving, j];
                }

                basis[leaving] = entering;
            }

            double[] xSol = new double[n];
            for (int i = 0; i < m; i++)
                if (basis[i] < n) xSol[basis[i]] = T[i, totalCols];

            double objVal = 0.0;
            for (int j = 0; j < n; j++)
                objVal += model.ObjectiveCoeffs[j] * xSol[j];

            return new LPREsults
            {
                Status = LPStatus.Optimal,
                ObjectiveValue = objVal,
                VariableValues = xSol,
                Log = reporter.GetLog(),
                FinalTableau = BuildFinalTableau(T, basis, m, totalCols)
            };
        }

        // =================== helpers ===================
        private string TableauToString(double[,] T, int m, int totalCols)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("   z:");
            for (int j = 0; j <= totalCols; j++)
                sb.AppendFormat("{0,8}", FormatNumber(T[m, j]));
            sb.AppendLine();
            for (int i = 0; i < m; i++)
            {
                sb.Append("row" + (i + 1) + ":");
                for (int j = 0; j <= totalCols; j++)
                    sb.AppendFormat("{0,8}", FormatNumber(T[i, j]));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private string FormatNumber(double value)
        {
            if (Math.Abs(value) < 1e-9) value = 0.0;
            if (Math.Abs(value - Math.Round(value)) < 1e-9)
                return Math.Round(value).ToString();
            else
                return value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static SimplexTableau BuildFinalTableau(double[,] T, int[] basis, int m, int totalCols)
        {
            var snap = new SimplexTableau(m + 1, totalCols + 1);
            for (int r = 0; r < m + 1; r++)
                for (int c = 0; c < totalCols + 1; c++)
                    snap.Table[r, c] = T[r, c];

            for (int i = 0; i < snap.BasicVars.Length && i < basis.Length; i++)
                snap.BasicVars[i] = basis[i];

            return snap;
        }

        /// <summary>
        /// Save artifacts to a standard-form tableau expected by SpecialCasesHandler:
        /// row 0 = objective row, rows 1..m = constraints, last column = RHS.
        /// </summary>
        private static void SaveArtifactsForSpecialCases(double[,] T, int[] basis, int m, int totalCols, bool isMax)
        {
            // T currently has objective in row m. Build a copy with objective in row 0.
            var rows = m + 1;
            var cols = totalCols + 1;
            var std = new double[rows, cols];

            // objective row -> row 0
            for (int j = 0; j < cols; j++) std[0, j] = T[m, j];
            // constraint rows -> rows 1..m
            for (int i = 0; i < m; i++)
                for (int j = 0; j < cols; j++)
                    std[i + 1, j] = T[i, j];

            // Save artifacts
            SolverArtifacts.LastTableau = std;

            // basic variables correspond to constraint rows; pass as-is
            var b = new int[m];
            for (int i = 0; i < m; i++) b[i] = basis[i];
            SolverArtifacts.LastBasicVars = b;

            SolverArtifacts.IsMaximization = isMax;
        }
    }
}
