using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using LP_Solver.Core.Models;
using LP_Solver.IO;

namespace LP_Solver.Core
{
    internal class CuttingPlane : ISolver
    {
        private const double TOL = 1e-6;
        private const int MAX_ITER = 50;

        public LPResult Solve(LpModel model, SolverOptions options, IReporter reporter)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (reporter == null) reporter = new StringBuilderReporter();
            if (options == null) options = new SolverOptions();

            reporter.WriteLine("===============================================");
            reporter.WriteLine("=== Cutting Plane Algorithm ===");
            reporter.WriteLine("===============================================");
            reporter.WriteLine("Problem type: " + (model.IsMaximization ? "Maximization" : "Minimization"));
            reporter.WriteLine("Original variables (n): " + model.NumVars + ", Constraints (m): " + model.NumCons);
            reporter.WriteLine("");

            DisplayCanonicalForm(model, reporter);

            // Add bounds for binary/integer variables
            LpModel workingModel = PrepareModelWithBounds(model, reporter);

            double[] bestSolution = null;
            double bestObj = model.IsMaximization ? double.NegativeInfinity : double.PositiveInfinity;
            int iteration = 0;

            // Track added cuts to avoid duplicates
            var addedCuts = new HashSet<string>();

            while (iteration < MAX_ITER)
            {
                iteration++;
                reporter.WriteLine($"\n==== Iteration {iteration} ====");

                // Solve LP relaxation
                var lpSolver = new PrimalSimplex();
                var nodeReporter = new StringBuilderReporter();
                LPResult result;

                try
                {
                    result = lpSolver.Solve(workingModel, options, nodeReporter);
                }
                catch (Exception ex)
                {
                    reporter.WriteLine("LP Solver error: " + ex.Message);
                    break;
                }

                // Display solver output
                reporter.WriteLine("LP Relaxation:");
                foreach (var line in nodeReporter.GetLog().Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
                    reporter.WriteLine("  " + line);

                if (result.Status != LPStatus.Optimal)
                {
                    reporter.WriteLine($"LP not optimal: {result.Status}");

                    // If infeasible, the problem has no integer solution
                    if (result.Status == LPStatus.Infeasible)
                    {
                        return new LPResult
                        {
                            Status = LPStatus.Infeasible,
                            ObjectiveValue = double.NaN,
                            VariableValues = new double[model.NumVars],
                            Log = reporter.GetLog()
                        };
                    }
                    break;
                }

                double[] solution = result.VariableValues ?? new double[workingModel.NumVars];
                double objective = result.ObjectiveValue;

                reporter.WriteLine($"Current LP Objective: {FormatNumber(objective)}");
                reporter.WriteLine("Variable values:");
                for (int i = 0; i < Math.Min(model.NumVars, solution.Length); i++)
                {
                    reporter.WriteLine($"  x{i + 1} = {FormatNumber(solution[i])}");
                }

                // Find fractional binary/integer variables
                var fractionalVars = new List<(int index, double fractionality)>();

                reporter.WriteLine("\nIntegrality check:");
                for (int i = 0; i < model.NumVars && i < solution.Length; i++)
                {
                    if (IsIntegerOrBinaryVariable(model, i))
                    {
                        double val = solution[i];
                        double roundedVal = Math.Round(val);
                        double fractionality = Math.Abs(val - roundedVal);

                        if (IsBinaryVariable(model, i))
                        {
                            // Binary: must be 0 or 1
                            bool isBinary = (Math.Abs(val) <= TOL) || (Math.Abs(val - 1.0) <= TOL);
                            reporter.WriteLine($"  x{i + 1} = {FormatNumber(val)} (binary) -> {(isBinary ? "OK" : "VIOLATES")}");
                            if (!isBinary)
                            {
                                // For binary, fractionality is distance from nearest valid value (0 or 1)
                                fractionality = Math.Min(Math.Abs(val), Math.Abs(val - 1.0));
                                fractionalVars.Add((i, fractionality));
                            }
                        }
                        else
                        {
                            // Integer: must be whole number
                            bool isInteger = fractionality <= TOL;
                            reporter.WriteLine($"  x{i + 1} = {FormatNumber(val)} (integer) -> {(isInteger ? "OK" : "VIOLATES")}");
                            if (!isInteger) fractionalVars.Add((i, fractionality));
                        }
                    }
                }

                if (fractionalVars.Count == 0)
                {
                    reporter.WriteLine("\n*** ALL INTEGER CONSTRAINTS SATISFIED ***");
                    bestSolution = new double[model.NumVars];
                    for (int i = 0; i < model.NumVars && i < solution.Length; i++)
                    {
                        bestSolution[i] = Math.Round(solution[i]); // Round to ensure clean integer values
                    }
                    bestObj = objective;
                    break;
                }

                // Sort by fractionality and try to add cuts
                fractionalVars.Sort((a, b) => b.fractionality.CompareTo(a.fractionality));

                bool cutAdded = false;
                foreach (var (varIndex, _) in fractionalVars)
                {
                    string cutKey = GenerateCutKey(varIndex, solution[varIndex], model);
                    if (!addedCuts.Contains(cutKey))
                    {
                        cutAdded = AddGomoryMixedIntegerCut(workingModel, varIndex, solution, model, reporter);
                        if (cutAdded)
                        {
                            addedCuts.Add(cutKey);
                            break;
                        }
                    }
                }

                if (!cutAdded)
                {
                    reporter.WriteLine("No valid cuts can be added - terminating");

                    // Try rounding heuristic
                    var roundedSol = RoundSolution(solution, model);
                    if (IsFeasible(roundedSol, model))
                    {
                        double roundedObj = CalculateObjective(roundedSol, model);
                        if (IsBetterSolution(roundedObj, bestObj, model.IsMaximization))
                        {
                            bestSolution = roundedSol;
                            bestObj = roundedObj;
                            reporter.WriteLine($"Found feasible rounded solution with objective: {FormatNumber(bestObj)}");
                        }
                    }
                    break;
                }
            }

            if (iteration >= MAX_ITER)
            {
                reporter.WriteLine($"\nReached maximum iterations ({MAX_ITER})");
            }

            if (bestSolution == null)
            {
                reporter.WriteLine("\n*** NO INTEGER SOLUTION FOUND ***");
                return new LPResult
                {
                    Status = LPStatus.Infeasible,
                    ObjectiveValue = double.NaN,
                    VariableValues = new double[model.NumVars],
                    Log = reporter.GetLog()
                };
            }

            reporter.WriteLine("\n=== OPTIMAL SOLUTION ===");
            reporter.WriteLine($"Objective: {FormatNumber(bestObj)}");
            reporter.WriteLine("Variables:");
            for (int i = 0; i < model.NumVars; i++)
            {
                reporter.WriteLine($"  x{i + 1} = {FormatNumber(bestSolution[i])}");
            }

            return new LPResult
            {
                Status = LPStatus.Optimal,
                ObjectiveValue = bestObj,
                VariableValues = bestSolution,
                Log = reporter.GetLog()
            };
        }

        private LpModel PrepareModelWithBounds(LpModel original, IReporter reporter)
        {
            reporter.WriteLine("Adding bounds for binary/integer variables...");

            var model = CloneModel(original);
            int addedBounds = 0;

            // Add both lower and upper bounds for binary variables
            for (int i = 0; i < model.NumVars; i++)
            {
                if (IsBinaryVariable(model, i))
                {
                    // Add constraint x_i >= 0
                    var lowerBound = new Constraint
                    {
                        Relation = ">=",
                        RHS = 0.0
                    };

                    for (int j = 0; j < model.NumVars; j++)
                    {
                        lowerBound.Coefficients.Add(j == i ? 1.0 : 0.0);
                    }

                    model.Constraints.Add(lowerBound);

                    // Add constraint x_i <= 1
                    var upperBound = new Constraint
                    {
                        Relation = "<=",
                        RHS = 1.0
                    };

                    for (int j = 0; j < model.NumVars; j++)
                    {
                        upperBound.Coefficients.Add(j == i ? 1.0 : 0.0);
                    }

                    model.Constraints.Add(upperBound);
                    addedBounds += 2;
                    reporter.WriteLine($"  Added bounds: 0 <= x{i + 1} <= 1");
                }
                else if (IsIntegerVariable(model, i))
                {
                    // Add non-negativity for integer variables if not already present
                    var lowerBound = new Constraint
                    {
                        Relation = ">=",
                        RHS = 0.0
                    };

                    for (int j = 0; j < model.NumVars; j++)
                    {
                        lowerBound.Coefficients.Add(j == i ? 1.0 : 0.0);
                    }

                    model.Constraints.Add(lowerBound);
                    addedBounds++;
                    reporter.WriteLine($"  Added lower bound: x{i + 1} >= 0");
                }
            }

            reporter.WriteLine($"Added {addedBounds} bound constraints");
            return model;
        }

        private bool AddGomoryMixedIntegerCut(LpModel model, int varIndex, double[] solution, LpModel originalModel, IReporter reporter)
        {
            double currentValue = solution[varIndex];
            reporter.WriteLine($"\nAdding Gomory cut for x{varIndex + 1} = {FormatNumber(currentValue)}");

            if (IsBinaryVariable(originalModel, varIndex))
            {
                // For binary variables, use simple disjunctive cuts
                if (currentValue < 0.5)
                {
                    // Force to 0
                    AddSimpleConstraint(model, varIndex, "<=", 0.0);
                    reporter.WriteLine($"  Added: x{varIndex + 1} <= 0 (forcing to 0)");
                }
                else
                {
                    // Force to 1
                    AddSimpleConstraint(model, varIndex, ">=", 1.0);
                    reporter.WriteLine($"  Added: x{varIndex + 1} >= 1 (forcing to 1)");
                }
            }
            else
            {
                // For integer variables, use Gomory fractional cut
                double floorVal = Math.Floor(currentValue);
                double fracPart = currentValue - floorVal;

                if (fracPart > TOL)
                {
                    // Add both upper and lower cuts for stronger formulation
                    if (fracPart <= 0.5)
                    {
                        // Closer to floor - add upper bound
                        AddSimpleConstraint(model, varIndex, "<=", floorVal);
                        reporter.WriteLine($"  Added: x{varIndex + 1} <= {FormatNumber(floorVal)}");
                    }
                    else
                    {
                        // Closer to ceiling - add lower bound
                        AddSimpleConstraint(model, varIndex, ">=", Math.Ceiling(currentValue));
                        reporter.WriteLine($"  Added: x{varIndex + 1} >= {FormatNumber(Math.Ceiling(currentValue))}");
                    }
                }
            }

            return true;
        }

        private string GenerateCutKey(int varIndex, double value, LpModel model)
        {
            if (IsBinaryVariable(model, varIndex))
            {
                return $"B{varIndex}_{(value < 0.5 ? "L" : "U")}";
            }
            else
            {
                double floorVal = Math.Floor(value);
                double fracPart = value - floorVal;
                return $"I{varIndex}_{(fracPart <= 0.5 ? "U" : "L")}{floorVal}";
            }
        }

        private double[] RoundSolution(double[] solution, LpModel model)
        {
            var rounded = new double[solution.Length];
            for (int i = 0; i < solution.Length; i++)
            {
                if (i < model.NumVars && IsIntegerOrBinaryVariable(model, i))
                {
                    if (IsBinaryVariable(model, i))
                    {
                        rounded[i] = solution[i] >= 0.5 ? 1.0 : 0.0;
                    }
                    else
                    {
                        rounded[i] = Math.Round(solution[i]);
                    }
                }
                else
                {
                    rounded[i] = solution[i];
                }
            }
            return rounded;
        }

        private bool IsFeasible(double[] solution, LpModel model)
        {
            foreach (var constraint in model.Constraints)
            {
                double lhs = 0;
                for (int i = 0; i < Math.Min(solution.Length, constraint.Coefficients.Count); i++)
                {
                    lhs += constraint.Coefficients[i] * solution[i];
                }

                bool satisfied = false;
                switch (constraint.Relation)
                {
                    case "<=":
                        satisfied = lhs <= constraint.RHS + TOL;
                        break;
                    case ">=":
                        satisfied = lhs >= constraint.RHS - TOL;
                        break;
                    case "=":
                        satisfied = Math.Abs(lhs - constraint.RHS) <= TOL;
                        break;
                }

                if (!satisfied) return false;
            }
            return true;
        }

        private double CalculateObjective(double[] solution, LpModel model)
        {
            double obj = 0;
            for (int i = 0; i < Math.Min(solution.Length, model.ObjectiveCoeffs.Count); i++)
            {
                obj += model.ObjectiveCoeffs[i] * solution[i];
            }
            return obj;
        }

        private bool IsBetterSolution(double newObj, double currentBest, bool isMaximization)
        {
            if (double.IsNaN(currentBest)) return true;
            return isMaximization ? newObj > currentBest : newObj < currentBest;
        }

        private void AddSimpleConstraint(LpModel model, int varIndex, string relation, double rhs)
        {
            var constraint = new Constraint
            {
                Relation = relation,
                RHS = rhs
            };

            for (int i = 0; i < model.NumVars; i++)
            {
                constraint.Coefficients.Add(i == varIndex ? 1.0 : 0.0);
            }

            model.Constraints.Add(constraint);
        }

        private bool IsIntegerOrBinaryVariable(LpModel model, int index)
        {
            if (model.VariableTypes == null || index >= model.VariableTypes.Count) return false;
            string type = model.VariableTypes[index]?.ToLower()?.Trim() ?? "";
            return type == "int" || type == "integer" || type == "bin" || type == "binary";
        }

        private bool IsIntegerVariable(LpModel model, int index)
        {
            if (model.VariableTypes == null || index >= model.VariableTypes.Count) return false;
            string type = model.VariableTypes[index]?.ToLower()?.Trim() ?? "";
            return type == "int" || type == "integer";
        }

        private bool IsBinaryVariable(LpModel model, int index)
        {
            if (model.VariableTypes == null || index >= model.VariableTypes.Count) return false;
            string type = model.VariableTypes[index]?.ToLower()?.Trim() ?? "";
            return type == "bin" || type == "binary";
        }

        private static LpModel CloneModel(LpModel src)
        {
            var copy = new LpModel { ProblemType = src.ProblemType };
            copy.ObjectiveCoeffs.AddRange(src.ObjectiveCoeffs);

            if (src.VariableTypes != null)
                copy.VariableTypes.AddRange(src.VariableTypes.Select(s => s == null ? null : string.Copy(s)));

            foreach (var constraint in src.Constraints)
            {
                var newConstraint = new Constraint
                {
                    Relation = constraint.Relation,
                    RHS = constraint.RHS
                };
                newConstraint.Coefficients.AddRange(constraint.Coefficients);
                copy.Constraints.Add(newConstraint);
            }

            return copy;
        }

        private void DisplayCanonicalForm(LpModel model, IReporter reporter)
        {
            reporter.WriteLine("=== Problem Formulation ===");

            // Objective
            var objStr = new StringBuilder(model.IsMaximization ? "Maximize: " : "Minimize: ");
            bool firstTerm = true;
            for (int i = 0; i < model.ObjectiveCoeffs.Count; i++)
            {
                double coeff = model.ObjectiveCoeffs[i];
                if (Math.Abs(coeff) < TOL) continue; // Skip zero coefficients

                if (!firstTerm)
                {
                    if (coeff >= 0) objStr.Append(" + ");
                    else objStr.Append(" - ");
                }
                else if (coeff < 0)
                {
                    objStr.Append("-");
                }

                objStr.Append($"{FormatNumber(Math.Abs(coeff))}x{i + 1}");
                firstTerm = false;
            }
            reporter.WriteLine(objStr.ToString());

            // Constraints
            reporter.WriteLine("Subject to:");
            for (int i = 0; i < model.Constraints.Count; i++)
            {
                var constraint = model.Constraints[i];
                var consStr = new StringBuilder("  ");
                bool firstTermInConstraint = true;

                for (int j = 0; j < constraint.Coefficients.Count; j++)
                {
                    double coeff = constraint.Coefficients[j];
                    if (Math.Abs(coeff) < TOL) continue; // Skip zero coefficients

                    if (!firstTermInConstraint)
                    {
                        if (coeff >= 0) consStr.Append(" + ");
                        else consStr.Append(" - ");
                    }
                    else if (coeff < 0)
                    {
                        consStr.Append("-");
                    }

                    consStr.Append($"{FormatNumber(Math.Abs(coeff))}x{j + 1}");
                    firstTermInConstraint = false;
                }

                consStr.Append($" {constraint.Relation} {FormatNumber(constraint.RHS)}");
                reporter.WriteLine(consStr.ToString());
            }

            // Variable types
            if (model.VariableTypes != null && model.VariableTypes.Any(t => !string.IsNullOrEmpty(t)))
            {
                reporter.WriteLine("Variable types:");
                for (int i = 0; i < model.VariableTypes.Count; i++)
                {
                    string type = model.VariableTypes[i];
                    if (!string.IsNullOrEmpty(type))
                    {
                        reporter.WriteLine($"  x{i + 1}: {type}");
                    }
                }
            }
            reporter.WriteLine("");
        }

        private static string FormatNumber(double val)
        {
            if (Math.Abs(val) < 1e-9) return "0";
            if (Math.Abs(val - Math.Round(val)) < 1e-9)
                return Math.Round(val).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return val.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}