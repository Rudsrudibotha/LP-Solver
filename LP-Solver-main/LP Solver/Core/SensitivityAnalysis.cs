using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LP_Solver.Core.Models;
using LP_Solver.IO;

namespace LP_Solver.Core
{
    public class SensitivityAnalyzer
    {
        private const double TOL = 1e-9;
        private LpModel originalModel;
        private LPResult optimalSolution;
        private SimplexTableau finalTableau;
        private int[] basicVariables;
        private int[] nonBasicVariables;
        private double[,] inverseB;
        private IReporter reporter;

        public SensitivityAnalyzer(LpModel model, LPResult solution, SimplexTableau tableau, IReporter reporter)
        {
            this.originalModel = model;
            this.optimalSolution = solution;
            this.finalTableau = tableau;
            this.reporter = reporter ?? new StringBuilderReporter();

            ExtractBasisInformation();
            CalculateInverseBasis();
        }

        private void ExtractBasisInformation()
        {
            int m = originalModel.NumCons;
            int n = originalModel.NumVars;

            basicVariables = new int[m];
            var nonBasicList = new List<int>();

            // Extract basic variables from final tableau
            for (int i = 0; i < m; i++)
            {
                basicVariables[i] = finalTableau.BasicVars[i];
            }

            // Extract non-basic variables
            for (int j = 0; j < n + m; j++)
            {
                if (!basicVariables.Contains(j))
                {
                    nonBasicList.Add(j);
                }
            }

            nonBasicVariables = nonBasicList.ToArray();
        }

        private void CalculateInverseBasis()
        {
            int m = originalModel.NumCons;
            inverseB = new double[m, m];

            // Extract B^-1 from the tableau (columns corresponding to original slack variables)
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    inverseB[i, j] = finalTableau.Table[i + 1, originalModel.NumVars + j];
                }
            }
        }

        // 1. Display the range of a selected Non-Basic Variable
        public void DisplayNonBasicVariableRange(int varIndex)
        {
            reporter.WriteLine("\n=== NON-BASIC VARIABLE RANGE ANALYSIS ===");
            reporter.WriteLine($"Variable x{varIndex + 1} (Non-Basic)");

            double currentCoeff = originalModel.ObjectiveCoeffs[varIndex];
            double reducedCost = finalTableau.Table[0, varIndex];

            reporter.WriteLine($"Current objective coefficient: {FormatNumber(currentCoeff)}");
            reporter.WriteLine($"Reduced cost: {FormatNumber(reducedCost)}");

            // For non-basic variable at lower bound (0)
            double allowableIncrease = double.PositiveInfinity;
            double allowableDecrease = Math.Abs(reducedCost);

            if (originalModel.IsMaximization)
            {
                // For maximization: can increase up to reduced cost becoming 0
                allowableIncrease = Math.Abs(reducedCost);
                allowableDecrease = double.PositiveInfinity;
            }

            reporter.WriteLine($"Allowable increase: {FormatNumber(allowableIncrease)}");
            reporter.WriteLine($"Allowable decrease: {FormatNumber(allowableDecrease)}");
            reporter.WriteLine($"Range: [{FormatNumber(currentCoeff - allowableDecrease)}, {FormatNumber(currentCoeff + allowableIncrease)}]");
            reporter.WriteLine("Note: Variable remains non-basic (at 0) within this range");
        }

        // 2. Apply and display a change of a selected Non-Basic Variable
        public void ApplyNonBasicVariableChange(int varIndex, double newCoeff)
        {
            reporter.WriteLine("\n=== APPLYING NON-BASIC VARIABLE CHANGE ===");
            reporter.WriteLine($"Changing x{varIndex + 1} coefficient from {FormatNumber(originalModel.ObjectiveCoeffs[varIndex])} to {FormatNumber(newCoeff)}");

            double delta = newCoeff - originalModel.ObjectiveCoeffs[varIndex];
            double newReducedCost = finalTableau.Table[0, varIndex] - delta;

            reporter.WriteLine($"New reduced cost: {FormatNumber(newReducedCost)}");

            bool optimalityMaintained = (originalModel.IsMaximization && newReducedCost <= TOL) ||
                                       (!originalModel.IsMaximization && newReducedCost >= -TOL);

            if (optimalityMaintained)
            {
                reporter.WriteLine("Current solution remains optimal");
                reporter.WriteLine($"Objective value unchanged: {FormatNumber(optimalSolution.ObjectiveValue)}");
            }
            else
            {
                reporter.WriteLine("Current solution is no longer optimal");
                reporter.WriteLine("Re-optimization required - variable may enter the basis");

                // Estimate new objective if variable enters basis
                double unitContribution = -newReducedCost;
                reporter.WriteLine($"If x{varIndex + 1} enters basis, objective improves by {FormatNumber(Math.Abs(unitContribution))} per unit");
            }
        }

        // 3. Display the range of a selected Basic Variable
        public void DisplayBasicVariableRange(int varIndex)
        {
            reporter.WriteLine("\n=== BASIC VARIABLE RANGE ANALYSIS ===");
            reporter.WriteLine($"Variable x{varIndex + 1} (Basic)");

            int basicIndex = Array.IndexOf(basicVariables, varIndex);
            if (basicIndex == -1)
            {
                reporter.WriteLine("Error: Variable is not in the basis");
                return;
            }

            double currentCoeff = originalModel.ObjectiveCoeffs[varIndex];
            double currentValue = optimalSolution.VariableValues[varIndex];

            reporter.WriteLine($"Current coefficient: {FormatNumber(currentCoeff)}");
            reporter.WriteLine($"Current value: {FormatNumber(currentValue)}");

            // Calculate allowable changes based on maintaining optimality
            double allowableIncrease = double.PositiveInfinity;
            double allowableDecrease = double.PositiveInfinity;

            // Check each non-basic variable's reduced cost sensitivity
            foreach (int j in nonBasicVariables)
            {
                if (j >= originalModel.NumVars) continue; // Skip slack variables

                double aij = finalTableau.Table[basicIndex + 1, j];
                if (Math.Abs(aij) < TOL) continue;

                double reducedCost = finalTableau.Table[0, j];
                double ratio = -reducedCost / aij;

                if (aij > 0)
                {
                    allowableIncrease = Math.Min(allowableIncrease, ratio);
                }
                else
                {
                    allowableDecrease = Math.Min(allowableDecrease, -ratio);
                }
            }

            reporter.WriteLine($"Allowable increase: {FormatNumber(allowableIncrease)}");
            reporter.WriteLine($"Allowable decrease: {FormatNumber(allowableDecrease)}");
            reporter.WriteLine($"Range: [{FormatNumber(currentCoeff - allowableDecrease)}, {FormatNumber(currentCoeff + allowableIncrease)}]");
            reporter.WriteLine("Note: Basis remains unchanged within this range");
        }

        // 4. Apply and display a change of a selected Basic Variable
        public void ApplyBasicVariableChange(int varIndex, double newCoeff)
        {
            reporter.WriteLine("\n=== APPLYING BASIC VARIABLE CHANGE ===");
            reporter.WriteLine($"Changing x{varIndex + 1} coefficient from {FormatNumber(originalModel.ObjectiveCoeffs[varIndex])} to {FormatNumber(newCoeff)}");

            int basicIndex = Array.IndexOf(basicVariables, varIndex);
            if (basicIndex == -1)
            {
                reporter.WriteLine("Error: Variable is not in the basis");
                return;
            }

            double delta = newCoeff - originalModel.ObjectiveCoeffs[varIndex];
            double currentValue = optimalSolution.VariableValues[varIndex];

            // Update objective value
            double newObjective = optimalSolution.ObjectiveValue + delta * currentValue;
            reporter.WriteLine($"New objective value: {FormatNumber(newObjective)}");

            // Check if optimality is maintained
            bool optimal = true;
            foreach (int j in nonBasicVariables)
            {
                if (j >= originalModel.NumVars) continue;

                double aij = finalTableau.Table[basicIndex + 1, j];
                double newReducedCost = finalTableau.Table[0, j] + delta * aij;

                bool violatesOptimality = (originalModel.IsMaximization && newReducedCost > TOL) ||
                                         (!originalModel.IsMaximization && newReducedCost < -TOL);

                if (violatesOptimality)
                {
                    optimal = false;
                    reporter.WriteLine($"Variable x{j + 1} violates optimality with reduced cost {FormatNumber(newReducedCost)}");
                }
            }

            if (optimal)
            {
                reporter.WriteLine("Solution remains optimal with same basis");
            }
            else
            {
                reporter.WriteLine("Current basis is no longer optimal - re-optimization required");
            }
        }

        // 5. Display the range of a selected constraint right-hand-side value
        public void DisplayRHSRange(int constraintIndex)
        {
            reporter.WriteLine("\n=== CONSTRAINT RHS RANGE ANALYSIS ===");
            reporter.WriteLine($"Constraint {constraintIndex + 1}: {GetConstraintString(constraintIndex)}");

            double currentRHS = originalModel.Constraints[constraintIndex].RHS;
            reporter.WriteLine($"Current RHS: {FormatNumber(currentRHS)}");

            // Calculate shadow price (dual variable)
            double shadowPrice = CalculateShadowPrice(constraintIndex);
            reporter.WriteLine($"Shadow price: {FormatNumber(shadowPrice)}");

            // Calculate allowable changes
            double allowableIncrease = double.PositiveInfinity;
            double allowableDecrease = double.PositiveInfinity;

            // Check feasibility constraints for basic variables
            for (int i = 0; i < basicVariables.Length; i++)
            {
                if (basicVariables[i] >= originalModel.NumVars) continue; // Skip slack variables

                double bij = inverseB[i, constraintIndex];
                if (Math.Abs(bij) < TOL) continue;

                double currentBasicValue = optimalSolution.VariableValues[basicVariables[i]];

                if (bij > 0)
                {
                    allowableDecrease = Math.Min(allowableDecrease, currentBasicValue / bij);
                }
                else
                {
                    allowableIncrease = Math.Min(allowableIncrease, -currentBasicValue / bij);
                }
            }

            reporter.WriteLine($"Allowable increase: {FormatNumber(allowableIncrease)}");
            reporter.WriteLine($"Allowable decrease: {FormatNumber(allowableDecrease)}");
            reporter.WriteLine($"Range: [{FormatNumber(currentRHS - allowableDecrease)}, {FormatNumber(currentRHS + allowableIncrease)}]");
            reporter.WriteLine("Note: Basis remains optimal and feasible within this range");
        }

        // 6. Apply and display a change of a selected constraint right-hand-side value
        public void ApplyRHSChange(int constraintIndex, double newRHS)
        {
            reporter.WriteLine("\n=== APPLYING RHS CHANGE ===");
            reporter.WriteLine($"Changing constraint {constraintIndex + 1} RHS from {FormatNumber(originalModel.Constraints[constraintIndex].RHS)} to {FormatNumber(newRHS)}");

            double delta = newRHS - originalModel.Constraints[constraintIndex].RHS;
            double shadowPrice = CalculateShadowPrice(constraintIndex);

            // Calculate new objective value
            double newObjective = optimalSolution.ObjectiveValue + shadowPrice * delta;
            reporter.WriteLine($"Shadow price: {FormatNumber(shadowPrice)}");
            reporter.WriteLine($"Change in objective: {FormatNumber(shadowPrice * delta)}");
            reporter.WriteLine($"New objective value: {FormatNumber(newObjective)}");

            // Update basic variable values
            reporter.WriteLine("\nNew basic variable values:");
            bool feasible = true;

            for (int i = 0; i < basicVariables.Length; i++)
            {
                double bij = inverseB[i, constraintIndex];
                double oldValue = basicVariables[i] < originalModel.NumVars ?
                    optimalSolution.VariableValues[basicVariables[i]] :
                    finalTableau.Table[i + 1, finalTableau.Table.GetLength(1) - 1];

                double newValue = oldValue + bij * delta;

                reporter.WriteLine($"  x{basicVariables[i] + 1}: {FormatNumber(oldValue)} -> {FormatNumber(newValue)}");

                if (newValue < -TOL)
                {
                    feasible = false;
                    reporter.WriteLine($"    WARNING: Negative value - infeasible!");
                }
            }

            if (feasible)
            {
                reporter.WriteLine("\nSolution remains feasible and optimal");
            }
            else
            {
                reporter.WriteLine("\nSolution becomes infeasible - dual simplex required");
            }
        }

        // 7. Display the range of a selected variable in a Non-Basic Variable column
        public void DisplayNonBasicColumnRange(int nonBasicVar, int rowIndex)
        {
            reporter.WriteLine("\n=== NON-BASIC COLUMN COEFFICIENT RANGE ===");
            reporter.WriteLine($"Analyzing coefficient of constraint {rowIndex + 1} for non-basic variable x{nonBasicVar + 1}");

            if (!nonBasicVariables.Contains(nonBasicVar))
            {
                reporter.WriteLine("Error: Variable is not non-basic");
                return;
            }

            double currentCoeff = originalModel.Constraints[rowIndex].Coefficients[nonBasicVar];
            reporter.WriteLine($"Current coefficient: {FormatNumber(currentCoeff)}");

            // Calculate how changes affect reduced cost
            double reducedCost = finalTableau.Table[0, nonBasicVar];
            double shadowPrice = CalculateShadowPrice(rowIndex);

            // Range where variable remains non-basic
            double allowableIncrease = double.PositiveInfinity;
            double allowableDecrease = double.PositiveInfinity;

            if (Math.Abs(shadowPrice) > TOL)
            {
                if (originalModel.IsMaximization)
                {
                    if (shadowPrice > 0)
                        allowableIncrease = reducedCost / shadowPrice;
                    else
                        allowableDecrease = -reducedCost / shadowPrice;
                }
                else
                {
                    if (shadowPrice > 0)
                        allowableDecrease = reducedCost / shadowPrice;
                    else
                        allowableIncrease = -reducedCost / shadowPrice;
                }
            }

            reporter.WriteLine($"Shadow price of constraint: {FormatNumber(shadowPrice)}");
            reporter.WriteLine($"Allowable increase: {FormatNumber(allowableIncrease)}");
            reporter.WriteLine($"Allowable decrease: {FormatNumber(allowableDecrease)}");
            reporter.WriteLine($"Range: [{FormatNumber(currentCoeff - allowableDecrease)}, {FormatNumber(currentCoeff + allowableIncrease)}]");
        }

        // 8. Apply and display a change of a selected variable in a Non-Basic Variable column
        public void ApplyNonBasicColumnChange(int nonBasicVar, int rowIndex, double newCoeff)
        {
            reporter.WriteLine("\n=== APPLYING NON-BASIC COLUMN COEFFICIENT CHANGE ===");
            double oldCoeff = originalModel.Constraints[rowIndex].Coefficients[nonBasicVar];
            reporter.WriteLine($"Changing coefficient of x{nonBasicVar + 1} in constraint {rowIndex + 1} from {FormatNumber(oldCoeff)} to {FormatNumber(newCoeff)}");

            double delta = newCoeff - oldCoeff;
            double shadowPrice = CalculateShadowPrice(rowIndex);

            // Calculate new reduced cost
            double oldReducedCost = finalTableau.Table[0, nonBasicVar];
            double newReducedCost = oldReducedCost - shadowPrice * delta;

            reporter.WriteLine($"Shadow price: {FormatNumber(shadowPrice)}");
            reporter.WriteLine($"Old reduced cost: {FormatNumber(oldReducedCost)}");
            reporter.WriteLine($"New reduced cost: {FormatNumber(newReducedCost)}");

            bool remainsOptimal = (originalModel.IsMaximization && newReducedCost <= TOL) ||
                                 (!originalModel.IsMaximization && newReducedCost >= -TOL);

            if (remainsOptimal)
            {
                reporter.WriteLine("Solution remains optimal - variable stays non-basic");
            }
            else
            {
                reporter.WriteLine("Variable becomes attractive to enter basis");
                reporter.WriteLine("Re-optimization required");
            }
        }

        // 9. Add a new activity to an optimal solution
        public void AddNewActivity(double[] coefficients, double objectiveCoeff, string name = "new")
        {
            reporter.WriteLine("\n=== ADDING NEW ACTIVITY ===");
            reporter.WriteLine($"New activity: x_{name}");
            reporter.WriteLine($"Objective coefficient: {FormatNumber(objectiveCoeff)}");
            reporter.WriteLine("Constraint coefficients:");

            for (int i = 0; i < coefficients.Length && i < originalModel.NumCons; i++)
            {
                reporter.WriteLine($"  Constraint {i + 1}: {FormatNumber(coefficients[i])}");
            }

            // Calculate reduced cost for new activity
            double reducedCost = objectiveCoeff;

            for (int i = 0; i < originalModel.NumCons; i++)
            {
                double shadowPrice = CalculateShadowPrice(i);
                reducedCost -= shadowPrice * coefficients[i];
            }

            if (originalModel.IsMaximization)
                reducedCost = -reducedCost;

            reporter.WriteLine($"\nReduced cost of new activity: {FormatNumber(reducedCost)}");

            bool attractive = (originalModel.IsMaximization && reducedCost > TOL) ||
                            (!originalModel.IsMaximization && reducedCost < -TOL);

            if (attractive)
            {
                reporter.WriteLine("New activity is attractive - should be included");
                reporter.WriteLine($"Potential improvement per unit: {FormatNumber(Math.Abs(reducedCost))}");

                // Calculate maximum quantity that can be produced
                double maxQuantity = double.PositiveInfinity;
                for (int i = 0; i < originalModel.NumCons; i++)
                {
                    if (coefficients[i] > TOL)
                    {
                        double slack = GetSlackValue(i);
                        maxQuantity = Math.Min(maxQuantity, slack / coefficients[i]);
                    }
                }

                reporter.WriteLine($"Maximum quantity: {FormatNumber(maxQuantity)}");
                reporter.WriteLine($"Potential objective improvement: {FormatNumber(Math.Abs(reducedCost) * maxQuantity)}");
            }
            else
            {
                reporter.WriteLine("New activity is not attractive - should not be included");
                reporter.WriteLine("Current solution remains optimal");
            }
        }

        // 10. Add a new constraint to an optimal solution
        public void AddNewConstraint(double[] coefficients, string relation, double rhs)
        {
            reporter.WriteLine("\n=== ADDING NEW CONSTRAINT ===");

            StringBuilder constraintStr = new StringBuilder();
            for (int i = 0; i < coefficients.Length && i < originalModel.NumVars; i++)
            {
                if (i > 0 && coefficients[i] >= 0) constraintStr.Append(" + ");
                else if (coefficients[i] < 0) constraintStr.Append(" - ");
                constraintStr.Append($"{FormatNumber(Math.Abs(coefficients[i]))}x{i + 1}");
            }
            constraintStr.Append($" {relation} {FormatNumber(rhs)}");

            reporter.WriteLine($"New constraint: {constraintStr}");

            // Check if current solution satisfies new constraint
            double lhs = 0;
            for (int i = 0; i < originalModel.NumVars; i++)
            {
                lhs += coefficients[i] * optimalSolution.VariableValues[i];
            }

            reporter.WriteLine($"Current solution LHS: {FormatNumber(lhs)}");
            reporter.WriteLine($"RHS: {FormatNumber(rhs)}");

            bool satisfied = false;
            switch (relation)
            {
                case "<=":
                    satisfied = lhs <= rhs + TOL;
                    break;
                case ">=":
                    satisfied = lhs >= rhs - TOL;
                    break;
                case "=":
                    satisfied = Math.Abs(lhs - rhs) <= TOL;
                    break;
            }

            if (satisfied)
            {
                reporter.WriteLine("Current solution satisfies new constraint");
                reporter.WriteLine("Solution remains optimal");

                double slack = relation == "<=" ? rhs - lhs : lhs - rhs;
                if (Math.Abs(slack) > TOL)
                {
                    reporter.WriteLine($"Slack in new constraint: {FormatNumber(Math.Abs(slack))}");
                }
                else
                {
                    reporter.WriteLine("New constraint is binding at current solution");
                }
            }
            else
            {
                reporter.WriteLine("Current solution violates new constraint");
                reporter.WriteLine($"Violation amount: {FormatNumber(Math.Abs(lhs - rhs))}");
                reporter.WriteLine("Re-optimization required with new constraint");

                // Estimate impact using dual simplex
                reporter.WriteLine("\nDual simplex method would be used to restore feasibility");
            }
        }

        // 11. Display shadow prices
        public void DisplayShadowPrices()
        {
            reporter.WriteLine("\n=== SHADOW PRICES (DUAL VARIABLES) ===");
            reporter.WriteLine("These represent the marginal value of each constraint's RHS:\n");

            for (int i = 0; i < originalModel.NumCons; i++)
            {
                double shadowPrice = CalculateShadowPrice(i);
                string constraintStr = GetConstraintString(i);

                reporter.WriteLine($"Constraint {i + 1}: {constraintStr}");
                reporter.WriteLine($"  Shadow price: {FormatNumber(shadowPrice)}");

                if (Math.Abs(shadowPrice) > TOL)
                {
                    string interpretation = originalModel.IsMaximization ?
                        (shadowPrice > 0 ? "increases" : "decreases") :
                        (shadowPrice < 0 ? "increases" : "decreases");

                    reporter.WriteLine($"  Interpretation: Objective {interpretation} by {FormatNumber(Math.Abs(shadowPrice))} per unit increase in RHS");

                    // Check if constraint is binding
                    double slack = GetSlackValue(i);
                    if (Math.Abs(slack) < TOL)
                    {
                        reporter.WriteLine("  Status: BINDING (slack = 0)");
                    }
                    else
                    {
                        reporter.WriteLine($"  Status: Non-binding (slack = {FormatNumber(slack)})");
                    }
                }
                else
                {
                    reporter.WriteLine("  Interpretation: Constraint has no marginal value (not binding)");
                }
                reporter.WriteLine("");
            }
        }

        // 12. Duality Analysis
        public void PerformDualityAnalysis()
        {
            reporter.WriteLine("\n=== DUALITY ANALYSIS ===");

            // Construct dual problem
            reporter.WriteLine("PRIMAL PROBLEM:");
            DisplayPrimalProblem();

            reporter.WriteLine("\nDUAL PROBLEM:");
            LpModel dualModel = ConstructDualModel();
            DisplayDualProblem(dualModel);

            // Get dual solution from shadow prices
            reporter.WriteLine("\nDUAL SOLUTION:");
            double[] dualSolution = new double[originalModel.NumCons];
            for (int i = 0; i < originalModel.NumCons; i++)
            {
                dualSolution[i] = Math.Abs(CalculateShadowPrice(i));
                reporter.WriteLine($"  y{i + 1} = {FormatNumber(dualSolution[i])}");
            }

            // Calculate dual objective
            double dualObjective = 0;
            for (int i = 0; i < originalModel.NumCons; i++)
            {
                dualObjective += dualSolution[i] * originalModel.Constraints[i].RHS;
            }

            reporter.WriteLine($"\nDual objective value: {FormatNumber(dualObjective)}");
            reporter.WriteLine($"Primal objective value: {FormatNumber(optimalSolution.ObjectiveValue)}");

            // Check strong vs weak duality
            double gap = Math.Abs(dualObjective - optimalSolution.ObjectiveValue);
            reporter.WriteLine($"Duality gap: {FormatNumber(gap)}");

            if (gap < TOL)
            {
                reporter.WriteLine("\n*** STRONG DUALITY HOLDS ***");
                reporter.WriteLine("Primal and dual objectives are equal");
                reporter.WriteLine("Both solutions are optimal");

                // Verify complementary slackness
                reporter.WriteLine("\nCOMPLEMENTARY SLACKNESS CONDITIONS:");
                bool allSatisfied = true;

                // Primal complementary slackness
                for (int j = 0; j < originalModel.NumVars; j++)
                {
                    double xj = optimalSolution.VariableValues[j];
                    double reducedCost = finalTableau.Table[0, j];

                    if (xj > TOL && Math.Abs(reducedCost) > TOL)
                    {
                        reporter.WriteLine($"  Violation: x{j + 1} = {FormatNumber(xj)} > 0 but reduced cost = {FormatNumber(reducedCost)} ≠ 0");
                        allSatisfied = false;
                    }
                }

                // Dual complementary slackness
                for (int i = 0; i < originalModel.NumCons; i++)
                {
                    double yi = dualSolution[i];
                    double slack = GetSlackValue(i);

                    if (yi > TOL && Math.Abs(slack) > TOL)
                    {
                        reporter.WriteLine($"  Violation: y{i + 1} = {FormatNumber(yi)} > 0 but slack = {FormatNumber(slack)} ≠ 0");
                        allSatisfied = false;
                    }
                }

                if (allSatisfied)
                {
                    reporter.WriteLine("  All complementary slackness conditions satisfied ✓");
                }
            }
            else
            {
                reporter.WriteLine("\n*** WEAK DUALITY ***");
                reporter.WriteLine("There is a gap between primal and dual objectives");
                reporter.WriteLine("Possible reasons: numerical errors or infeasibility");
            }
        }

        // Helper Methods
        private double CalculateShadowPrice(int constraintIndex)
        {
            // Shadow price is the reduced cost of the slack variable
            int slackVarIndex = originalModel.NumVars + constraintIndex;
            return -finalTableau.Table[0, slackVarIndex];
        }

        private double GetSlackValue(int constraintIndex)
        {
            // Calculate slack as RHS - LHS
            double lhs = 0;
            var constraint = originalModel.Constraints[constraintIndex];

            for (int j = 0; j < originalModel.NumVars; j++)
            {
                lhs += constraint.Coefficients[j] * optimalSolution.VariableValues[j];
            }

            switch (constraint.Relation)
            {
                case "<=":
                    return constraint.RHS - lhs;
                case ">=":
                    return lhs - constraint.RHS;
                default:
                    return 0;
            }
        }

        private string GetConstraintString(int index)
        {
            var constraint = originalModel.Constraints[index];
            var sb = new StringBuilder();

            bool first = true;
            for (int j = 0; j < constraint.Coefficients.Count && j < originalModel.NumVars; j++)
            {
                double coeff = constraint.Coefficients[j];
                if (Math.Abs(coeff) < TOL) continue;

                if (!first)
                {
                    sb.Append(coeff >= 0 ? " + " : " - ");
                    sb.Append(FormatNumber(Math.Abs(coeff)));
                }
                else
                {
                    if (coeff < 0) sb.Append("-");
                    sb.Append(FormatNumber(Math.Abs(coeff)));
                    first = false;
                }
                sb.Append($"x{j + 1}");
            }

            sb.Append($" {constraint.Relation} {FormatNumber(constraint.RHS)}");
            return sb.ToString();
        }

        private void DisplayPrimalProblem()
        {
            StringBuilder obj = new StringBuilder(originalModel.IsMaximization ? "Maximize: " : "Minimize: ");

            for (int i = 0; i < originalModel.NumVars; i++)
            {
                if (i > 0) obj.Append(" + ");
                obj.Append($"{FormatNumber(originalModel.ObjectiveCoeffs[i])}x{i + 1}");
            }
            reporter.WriteLine($"  {obj}");

            reporter.WriteLine("  Subject to:");
            for (int i = 0; i < originalModel.NumCons; i++)
            {
                reporter.WriteLine($"    {GetConstraintString(i)}");
            }
        }

        private LpModel ConstructDualModel()
        {
            var dual = new LpModel();
            dual.ProblemType = originalModel.IsMaximization ? "min" : "max";

            // Dual variables for each constraint
            for (int i = 0; i < originalModel.NumCons; i++)
            {
                dual.ObjectiveCoeffs.Add(originalModel.Constraints[i].RHS);
            }

            // Dual constraints for each primal variable
            for (int j = 0; j < originalModel.NumVars; j++)
            {
                var dualConstraint = new Constraint();

                for (int i = 0; i < originalModel.NumCons; i++)
                {
                    dualConstraint.Coefficients.Add(originalModel.Constraints[i].Coefficients[j]);
                }

                // Determine constraint type based on primal
                if (originalModel.IsMaximization)
                    dualConstraint.Relation = ">=";
                else
                    dualConstraint.Relation = "<=";

                dualConstraint.RHS = originalModel.ObjectiveCoeffs[j];
                dual.Constraints.Add(dualConstraint);
            }

            return dual;
        }

        private void DisplayDualProblem(LpModel dual)
        {
            StringBuilder obj = new StringBuilder(dual.IsMaximization ? "Maximize: " : "Minimize: ");

            for (int i = 0; i < dual.NumVars; i++)
            {
                if (i > 0) obj.Append(" + ");
                obj.Append($"{FormatNumber(dual.ObjectiveCoeffs[i])}y{i + 1}");
            }
            reporter.WriteLine($"  {obj}");

            reporter.WriteLine("  Subject to:");
            for (int j = 0; j < dual.NumCons; j++)
            {
                StringBuilder cons = new StringBuilder("    ");
                for (int i = 0; i < dual.Constraints[j].Coefficients.Count; i++)
                {
                    if (i > 0) cons.Append(" + ");
                    cons.Append($"{FormatNumber(dual.Constraints[j].Coefficients[i])}y{i + 1}");
                }
                cons.Append($" {dual.Constraints[j].Relation} {FormatNumber(dual.Constraints[j].RHS)}");
                reporter.WriteLine(cons.ToString());
            }
        }

        private static string FormatNumber(double value)
        {
            if (Math.Abs(value) < TOL) return "0.000";
            if (double.IsInfinity(value)) return value > 0 ? "∞" : "-∞";
            return value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // SimplexTableau class to hold the final tableau information
    public class SimplexTableau
    {
        public double[,] Table { get; set; }
        public int[] BasicVars { get; set; }
        public int Rows { get; set; }
        public int Cols { get; set; }

        public SimplexTableau(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
            Table = new double[rows, cols];
            BasicVars = new int[rows - 1]; // Excluding objective row
        }
    }
}