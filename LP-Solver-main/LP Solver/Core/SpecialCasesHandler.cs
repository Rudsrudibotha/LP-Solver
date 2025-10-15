using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LP_Solver.Core.Models;
using LP_Solver.IO;

namespace LP_Solver.Core
{
    /// <summary>
    /// Handles special cases in Linear Programming: Infeasibility, Unboundedness, Degeneracy
    /// </summary>
    public class SpecialCasesHandler
    {
        private const double TOL = 1e-9;
        private IReporter reporter;

        public SpecialCasesHandler(IReporter reporter = null)
        {
            this.reporter = reporter ?? new StringBuilderReporter();
        }

        /// <summary>
        /// Detects and handles infeasibility in the initial tableau
        /// </summary>
        public InfeasibilityResult CheckInfeasibility(LpModel model)
        {
            reporter.WriteLine("\n=== INFEASIBILITY DETECTION ===");

            var result = new InfeasibilityResult();

            // Check for obvious contradictions
            for (int i = 0; i < model.Constraints.Count; i++)
            {
                var constraint = model.Constraints[i];

                // Check for impossible constraints like 0 >= positive or 0 <= negative
                bool allZeroCoeffs = constraint.Coefficients.All(c => Math.Abs(c) < TOL);

                if (allZeroCoeffs)
                {
                    bool impossible = false;
                    switch (constraint.Relation)
                    {
                        case "<=":
                            impossible = constraint.RHS < -TOL;
                            break;
                        case ">=":
                            impossible = constraint.RHS > TOL;
                            break;
                        case "=":
                            impossible = Math.Abs(constraint.RHS) > TOL;
                            break;
                    }

                    if (impossible)
                    {
                        result.IsInfeasible = true;
                        result.ConflictingConstraints.Add(i);
                        reporter.WriteLine($"Constraint {i + 1} is impossible: 0 {constraint.Relation} {constraint.RHS}");
                    }
                }
            }

            // Check for conflicting equality constraints
            for (int i = 0; i < model.Constraints.Count - 1; i++)
            {
                if (model.Constraints[i].Relation != "=") continue;

                for (int j = i + 1; j < model.Constraints.Count; j++)
                {
                    if (model.Constraints[j].Relation != "=") continue;

                    if (AreConstraintsConflicting(model.Constraints[i], model.Constraints[j], model.NumVars))
                    {
                        result.IsInfeasible = true;
                        result.ConflictingConstraints.Add(i);
                        result.ConflictingConstraints.Add(j);
                        reporter.WriteLine($"Constraints {i + 1} and {j + 1} are conflicting equality constraints");
                    }
                }
            }

            if (!result.IsInfeasible)
            {
                reporter.WriteLine("No obvious infeasibility detected in initial formulation");
            }

            return result;
        }

        /// <summary>
        /// Implements Phase I of the Two-Phase Simplex Method to detect infeasibility
        /// </summary>
        public PhaseOneResult SolvePhaseOne(LpModel model)
        {
            reporter.WriteLine("\n=== PHASE I - FINDING INITIAL FEASIBLE SOLUTION ===");

            // Create auxiliary problem with artificial variables
            var auxModel = CreateAuxiliaryProblem(model);

            reporter.WriteLine("Auxiliary problem created with artificial variables");
            reporter.WriteLine($"Original variables: {model.NumVars}");
            reporter.WriteLine($"Artificial variables: {auxModel.NumVars - model.NumVars}");

            // Solve auxiliary problem
            var solver = new PrimalSimplex();
            var auxReporter = new StringBuilderReporter();
            var auxResult = solver.Solve(auxModel, new SolverOptions(), auxReporter);

            reporter.WriteLine("\nPhase I Solution:");
            reporter.WriteLine(auxReporter.GetLog());

            var result = new PhaseOneResult();
            result.AuxiliaryObjective = auxResult.ObjectiveValue;
            result.Solution = auxResult.VariableValues;

            // Check if original problem is feasible
            if (Math.Abs(auxResult.ObjectiveValue) < TOL)
            {
                result.IsFeasible = true;
                reporter.WriteLine("\n*** FEASIBLE SOLUTION FOUND ***");
                reporter.WriteLine("All artificial variables are zero");

                // Extract basic feasible solution for original problem
                result.BasicFeasibleSolution = new double[model.NumVars];
                Array.Copy(auxResult.VariableValues, result.BasicFeasibleSolution, model.NumVars);
            }
            else
            {
                result.IsFeasible = false;
                reporter.WriteLine("\n*** PROBLEM IS INFEASIBLE ***");
                reporter.WriteLine($"Minimum sum of artificial variables: {auxResult.ObjectiveValue}");

                // Identify which constraints cannot be satisfied
                IdentifyInfeasibleConstraints(model, auxResult.VariableValues);
            }

            return result;
        }

        /// <summary>
        /// Detects unboundedness in the simplex tableau
        /// </summary>
        public UnboundednessResult CheckUnboundedness(double[,] tableau, int[] basicVars, bool isMax)
        {
            reporter.WriteLine("\n=== UNBOUNDEDNESS DETECTION ===");

            var result = new UnboundednessResult();
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);

            // Check each non-basic variable
            for (int j = 0; j < cols - 1; j++)
            {
                // Skip basic variables
                if (basicVars.Contains(j)) continue;

                // Check if variable can improve objective
                double reducedCost = tableau[0, j];
                bool canImprove = (isMax && reducedCost > TOL) || (!isMax && reducedCost < -TOL);

                if (canImprove)
                {
                    // Check if all coefficients in column are non-positive
                    bool allNonPositive = true;
                    for (int i = 1; i < rows; i++)
                    {
                        if (tableau[i, j] > TOL)
                        {
                            allNonPositive = false;
                            break;
                        }
                    }

                    if (allNonPositive)
                    {
                        result.IsUnbounded = true;
                        result.UnboundedVariable = j;
                        result.UnboundedDirection = new double[cols - 1];
                        result.UnboundedDirection[j] = 1.0;

                        reporter.WriteLine($"*** PROBLEM IS UNBOUNDED ***");
                        reporter.WriteLine($"Variable x{j + 1} can increase indefinitely");
                        reporter.WriteLine($"Reduced cost: {reducedCost}");
                        reporter.WriteLine("All constraint coefficients are non-positive");

                        // Calculate unbounded ray
                        for (int i = 1; i < rows; i++)
                        {
                            if (basicVars[i - 1] < cols - 1)
                            {
                                result.UnboundedDirection[basicVars[i - 1]] = -tableau[i, j];
                            }
                        }

                        break;
                    }
                }
            }

            if (!result.IsUnbounded)
            {
                reporter.WriteLine("Problem is bounded");
            }

            return result;
        }

        /// <summary>
        /// Handles degeneracy in the simplex method
        /// </summary>
        public DegeneracyResult HandleDegeneracy(double[,] tableau, int[] basicVars)
        {
            reporter.WriteLine("\n=== DEGENERACY DETECTION ===");

            var result = new DegeneracyResult();
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);

            // Check for degenerate basic variables (value = 0)
            for (int i = 1; i < rows; i++)
            {
                double value = tableau[i, cols - 1]; // RHS value
                if (Math.Abs(value) < TOL)
                {
                    result.IsDegenarate = true;
                    result.DegenerateVariables.Add(basicVars[i - 1]);
                    reporter.WriteLine($"Basic variable x{basicVars[i - 1] + 1} is degenerate (value = 0)");
                }
            }

            if (result.IsDegenarate)
            {
                reporter.WriteLine("\n*** DEGENERATE SOLUTION DETECTED ***");
                reporter.WriteLine("Using Bland's rule to prevent cycling:");
                reporter.WriteLine("1. Choose entering variable with smallest index among those with favorable reduced cost");
                reporter.WriteLine("2. Choose leaving variable with smallest index among tied ratios");

                result.UseBlandsRule = true;
            }
            else
            {
                reporter.WriteLine("Solution is non-degenerate");
            }

            return result;
        }

        /// <summary>
        /// Detects and handles multiple optimal solutions
        /// </summary>
        public MultipleOptimalResult CheckMultipleOptimal(double[,] tableau, int[] basicVars, bool isMax)
        {
            reporter.WriteLine("\n=== MULTIPLE OPTIMAL SOLUTIONS DETECTION ===");

            var result = new MultipleOptimalResult();
            int cols = tableau.GetLength(1);

            // Check for non-basic variables with zero reduced cost
            for (int j = 0; j < cols - 1; j++)
            {
                // Skip basic variables
                if (basicVars.Contains(j)) continue;

                double reducedCost = tableau[0, j];
                if (Math.Abs(reducedCost) < TOL)
                {
                    result.HasMultipleOptimal = true;
                    result.AlternateEnteringVars.Add(j);
                    reporter.WriteLine($"Non-basic variable x{j + 1} has zero reduced cost");
                }
            }

            if (result.HasMultipleOptimal)
            {
                reporter.WriteLine("\n*** MULTIPLE OPTIMAL SOLUTIONS EXIST ***");
                reporter.WriteLine("Any convex combination of these solutions is also optimal");
                reporter.WriteLine("Alternate optimal solutions can be found by pivoting on variables with zero reduced cost");
            }
            else
            {
                reporter.WriteLine("Unique optimal solution");
            }

            return result;
        }

        /// <summary>
        /// Provides detailed analysis when no feasible solution exists
        /// </summary>
        public void AnalyzeInfeasibility(LpModel model, InfeasibilityResult infeasResult)
        {
            reporter.WriteLine("\n=== INFEASIBILITY ANALYSIS ===");
            reporter.WriteLine("The problem has no feasible solution due to conflicting constraints.");

            if (infeasResult.ConflictingConstraints.Count > 0)
            {
                reporter.WriteLine("\nConflicting constraints identified:");
                foreach (int index in infeasResult.ConflictingConstraints)
                {
                    reporter.WriteLine($"  Constraint {index + 1}: {GetConstraintString(model.Constraints[index], model.NumVars)}");
                }
            }

            reporter.WriteLine("\nPossible remedies:");
            reporter.WriteLine("1. Review constraint formulation for errors");
            reporter.WriteLine("2. Relax constraint bounds if possible");
            reporter.WriteLine("3. Remove redundant or contradictory constraints");
            reporter.WriteLine("4. Consider using goal programming for soft constraints");

            // Suggest minimal constraint relaxation
            SuggestMinimalRelaxation(model, infeasResult);
        }

        /// <summary>
        /// Provides detailed analysis when problem is unbounded
        /// </summary>
        public void AnalyzeUnboundedness(LpModel model, UnboundednessResult unboundedResult)
        {
            reporter.WriteLine("\n=== UNBOUNDEDNESS ANALYSIS ===");
            reporter.WriteLine("The objective function can be improved indefinitely.");

            if (unboundedResult.UnboundedVariable >= 0)
            {
                reporter.WriteLine($"\nUnbounded variable: x{unboundedResult.UnboundedVariable + 1}");
                reporter.WriteLine("Unbounded ray direction:");

                for (int i = 0; i < unboundedResult.UnboundedDirection.Length; i++)
                {
                    if (Math.Abs(unboundedResult.UnboundedDirection[i]) > TOL)
                    {
                        reporter.WriteLine($"  x{i + 1}: {unboundedResult.UnboundedDirection[i]:F3}");
                    }
                }
            }

            reporter.WriteLine("\nPossible causes:");
            reporter.WriteLine("1. Missing constraints that should limit variables");
            reporter.WriteLine("2. Incorrect constraint formulation");
            reporter.WriteLine("3. Model does not accurately represent the real problem");

            reporter.WriteLine("\nRemedies:");
            reporter.WriteLine("1. Add upper bounds on variables");
            reporter.WriteLine("2. Review problem formulation");
            reporter.WriteLine("3. Check for missing resource constraints");
        }

        // Helper Methods

        private LpModel CreateAuxiliaryProblem(LpModel original)
        {
            var aux = new LpModel();
            aux.ProblemType = "min"; // Always minimize sum of artificial variables

            // Original variables have zero cost
            for (int i = 0; i < original.NumVars; i++)
            {
                aux.ObjectiveCoeffs.Add(0);
            }

            // Add artificial variables with cost 1
            int artificialCount = 0;
            foreach (var constraint in original.Constraints)
            {
                if (constraint.Relation == ">=" || constraint.Relation == "=")
                {
                    aux.ObjectiveCoeffs.Add(1);
                    artificialCount++;
                }
            }

            // Copy and modify constraints
            int artificialIndex = 0;
            foreach (var constraint in original.Constraints)
            {
                var newConstraint = new Constraint();
                newConstraint.Coefficients.AddRange(constraint.Coefficients);

                // Add coefficients for artificial variables
                for (int i = 0; i < artificialCount; i++)
                {
                    if (i == artificialIndex && (constraint.Relation == ">=" || constraint.Relation == "="))
                    {
                        newConstraint.Coefficients.Add(1);
                    }
                    else
                    {
                        newConstraint.Coefficients.Add(0);
                    }
                }

                // All constraints become equalities in Phase I
                newConstraint.Relation = "=";
                newConstraint.RHS = constraint.RHS;
                aux.Constraints.Add(newConstraint);

                if (constraint.Relation == ">=" || constraint.Relation == "=")
                {
                    artificialIndex++;
                }
            }

            return aux;
        }

        private bool AreConstraintsConflicting(Constraint c1, Constraint c2, int numVars)
        {
            // Check if constraints are parallel but with different RHS
            bool parallel = true;
            double ratio = 0;
            bool ratioSet = false;

            for (int i = 0; i < numVars && i < c1.Coefficients.Count && i < c2.Coefficients.Count; i++)
            {
                if (Math.Abs(c1.Coefficients[i]) < TOL && Math.Abs(c2.Coefficients[i]) < TOL)
                    continue;

                if (Math.Abs(c1.Coefficients[i]) < TOL || Math.Abs(c2.Coefficients[i]) < TOL)
                {
                    parallel = false;
                    break;
                }

                double currentRatio = c1.Coefficients[i] / c2.Coefficients[i];
                if (!ratioSet)
                {
                    ratio = currentRatio;
                    ratioSet = true;
                }
                else if (Math.Abs(currentRatio - ratio) > TOL)
                {
                    parallel = false;
                    break;
                }
            }

            if (parallel && ratioSet)
            {
                // Check if RHS values are consistent
                double expectedRHS = c2.RHS * ratio;
                return Math.Abs(c1.RHS - expectedRHS) > TOL;
            }

            return false;
        }

        private void IdentifyInfeasibleConstraints(LpModel model, double[] phaseOneSolution)
        {
            reporter.WriteLine("\nConstraints causing infeasibility:");

            int artificialStart = model.NumVars;
            for (int i = 0; i < model.Constraints.Count; i++)
            {
                if (model.Constraints[i].Relation == ">=" || model.Constraints[i].Relation == "=")
                {
                    if (artificialStart < phaseOneSolution.Length && phaseOneSolution[artificialStart] > TOL)
                    {
                        reporter.WriteLine($"  Constraint {i + 1}: {GetConstraintString(model.Constraints[i], model.NumVars)}");
                        reporter.WriteLine($"    Artificial variable value: {phaseOneSolution[artificialStart]:F3}");
                    }
                    artificialStart++;
                }
            }
        }

        private void SuggestMinimalRelaxation(LpModel model, InfeasibilityResult result)
        {
            reporter.WriteLine("\nSuggested minimal relaxation:");

            foreach (int index in result.ConflictingConstraints)
            {
                var constraint = model.Constraints[index];

                switch (constraint.Relation)
                {
                    case "<=":
                        reporter.WriteLine($"  Constraint {index + 1}: Consider increasing RHS from {constraint.RHS:F3}");
                        break;
                    case ">=":
                        reporter.WriteLine($"  Constraint {index + 1}: Consider decreasing RHS from {constraint.RHS:F3}");
                        break;
                    case "=":
                        reporter.WriteLine($"  Constraint {index + 1}: Consider changing to inequality (<=/>= {constraint.RHS:F3})");
                        break;
                }
            }
        }

        private string GetConstraintString(Constraint constraint, int numVars)
        {
            var sb = new StringBuilder();
            bool first = true;

            for (int j = 0; j < constraint.Coefficients.Count && j < numVars; j++)
            {
                double coeff = constraint.Coefficients[j];
                if (Math.Abs(coeff) < TOL) continue;

                if (!first)
                {
                    sb.Append(coeff >= 0 ? " + " : " - ");
                    sb.Append($"{Math.Abs(coeff):F3}x{j + 1}");
                }
                else
                {
                    if (coeff < 0) sb.Append("-");
                    sb.Append($"{Math.Abs(coeff):F3}x{j + 1}");
                    first = false;
                }
            }

            sb.Append($" {constraint.Relation} {constraint.RHS:F3}");
            return sb.ToString();
        }
    }

    // Result classes for special cases

    public class InfeasibilityResult
    {
        public bool IsInfeasible { get; set; }
        public List<int> ConflictingConstraints { get; set; } = new List<int>();
        public string Explanation { get; set; }
    }

    public class PhaseOneResult
    {
        public bool IsFeasible { get; set; }
        public double AuxiliaryObjective { get; set; }
        public double[] Solution { get; set; }
        public double[] BasicFeasibleSolution { get; set; }
    }

    public class UnboundednessResult
    {
        public bool IsUnbounded { get; set; }
        public int UnboundedVariable { get; set; } = -1;
        public double[] UnboundedDirection { get; set; }
    }

    public class DegeneracyResult
    {
        public bool IsDegenarate { get; set; }
        public List<int> DegenerateVariables { get; set; } = new List<int>();
        public bool UseBlandsRule { get; set; }
    }

    public class MultipleOptimalResult
    {
        public bool HasMultipleOptimal { get; set; }
        public List<int> AlternateEnteringVars { get; set; } = new List<int>();
    }
}