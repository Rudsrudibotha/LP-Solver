using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LP_Solver.Core.Models;

namespace LP_Solver.Core
{
    internal class BranchAndBoundSimplex : ISolver
    {
        private const double TOL = 1e-9;

        public LPResult Solve(LpModel model, SolverOptions options, IReporter reporter)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (reporter == null) reporter = new StringBuilderReporter();
            if (options == null) options = new SolverOptions();

            // Header for branch & bound
            reporter.WriteLine("===============================================");
            reporter.WriteLine("=== Branch & Bound Simplex Algorithm ===");
            reporter.WriteLine("===============================================");
            reporter.WriteLine("Problem type: " + (model.IsMaximization ? "Maximization" : "Minimization"));
            reporter.WriteLine("Original variables (n): " + model.NumVars + ", Constraints (m): " + model.NumCons);
            reporter.WriteLine("");

            // Display the original problem in canonical form
            DisplayCanonicalForm(model, reporter);

            // Add bounds for binary variables
            LpModel boundedModel = AddBinaryBounds(model, reporter);

            // Incumbent (best integer solution found so far)
            double bestObj = model.IsMaximization ? double.NegativeInfinity : double.PositiveInfinity;
            double[] bestX = null;
            bool foundInteger = false;

            // Priority queue for best-first search (better than DFS for finding optimal solutions)
            // Store (priority, model, nodeName, depth, nodeId)
            var pQueue = new SortedList<double, List<Tuple<LpModel, string, int, int>>>();
            int nodeIdCounter = 0;

            // Add root node
            AddToQueue(pQueue, 0.0, boundedModel, "Root", 0, nodeIdCounter++, model.IsMaximization);

            int nodeCounter = 0;
            int maxNodes = 100; // Prevent infinite loops
            var nodeResults = new List<string>(); // Store all node results for final summary

            reporter.WriteLine("===============================================");
            reporter.WriteLine("=== Starting Branch & Bound Tree Exploration ===");
            reporter.WriteLine("===============================================");

            while (pQueue.Count > 0 && nodeCounter < maxNodes)
            {
                // Get node with best bound
                var firstKey = pQueue.Keys[model.IsMaximization ? pQueue.Count - 1 : 0];
                var nodeList = pQueue[firstKey];
                var tuple = nodeList[0];
                nodeList.RemoveAt(0);
                if (nodeList.Count == 0)
                    pQueue.Remove(firstKey);

                var nodeModel = tuple.Item1;
                var nodeName = tuple.Item2;
                var depth = tuple.Item3;
                nodeCounter++;

                // Create indentation based on depth
                string indent = new string(' ', depth * 2);

                reporter.WriteLine("");
                reporter.WriteLine(indent + "====================================================");
                reporter.WriteLine(indent + "NODE " + nodeCounter + ": " + nodeName);
                reporter.WriteLine(indent + "Depth: " + depth);
                reporter.WriteLine(indent + "====================================================");

                // Solve LP relaxation for this node using primal simplex
                var nodeReporter = new StringBuilderReporter();
                var lpSolver = new PrimalSimplex();
                LPResult nodeResult;

                try
                {
                    nodeResult = lpSolver.Solve(nodeModel, options, nodeReporter);
                }
                catch (Exception ex)
                {
                    reporter.WriteLine(indent + "SOLVER ERROR: " + ex.Message);
                    nodeResults.Add("Node " + nodeCounter + " (" + nodeName + "): Solver Error");
                    continue;
                }

                // Display the LP relaxation solution with full details
                reporter.WriteLine(indent + "LP Relaxation Solution:");
                reporter.WriteLine(indent + "------------------------");

                string[] logLines = nodeReporter.GetLog().Split('\n');
                foreach (string line in logLines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        reporter.WriteLine(indent + "   " + line);
                }

                // Check solution status
                if (nodeResult == null || nodeResult.Status == LPStatus.Infeasible)
                {
                    reporter.WriteLine(indent + "FATHOMED: Node is infeasible");
                    nodeResults.Add("Node " + nodeCounter + " (" + nodeName + "): Infeasible - Fathomed");
                    continue;
                }

                if (nodeResult.Status == LPStatus.Unbounded)
                {
                    reporter.WriteLine(indent + "FATHOMED: Node is unbounded");
                    nodeResults.Add("Node " + nodeCounter + " (" + nodeName + "): Unbounded - Fathomed");
                    continue;
                }

                if (nodeResult.Status == LPStatus.Error)
                {
                    reporter.WriteLine(indent + "FATHOMED: Solver error");
                    nodeResults.Add("Node " + nodeCounter + " (" + nodeName + "): Error - Fathomed");
                    continue;
                }

                if (nodeResult.Status == LPStatus.IterationLimit)
                {
                    reporter.WriteLine(indent + "FATHOMED: Iteration limit reached");
                    nodeResults.Add("Node " + nodeCounter + " (" + nodeName + "): Iteration Limit - Fathomed");
                    continue;
                }

                double nodeObj = nodeResult.ObjectiveValue;
                double[] sol = nodeResult.VariableValues ?? new double[nodeModel.NumVars];

                reporter.WriteLine(indent + "LP Objective Value: " + FormatNumber(nodeObj));

                // Check bounding: prune if node bound cannot improve incumbent
                bool prunedByBound = false;
                if (foundInteger)
                {
                    if (model.IsMaximization && nodeObj <= bestObj + TOL)
                    {
                        reporter.WriteLine(indent + "FATHOMED BY BOUND: " + FormatNumber(nodeObj) + " <= " + FormatNumber(bestObj) + " (incumbent)");
                        nodeResults.Add("Node " + nodeCounter + " (" + nodeName + "): Bound " + FormatNumber(nodeObj) + " <= Incumbent " + FormatNumber(bestObj) + " - Fathomed");
                        prunedByBound = true;
                    }
                    else if (!model.IsMaximization && nodeObj >= bestObj - TOL)
                    {
                        reporter.WriteLine(indent + "FATHOMED BY BOUND: " + FormatNumber(nodeObj) + " >= " + FormatNumber(bestObj) + " (incumbent)");
                        nodeResults.Add("Node " + nodeCounter + " (" + nodeName + "): Bound " + FormatNumber(nodeObj) + " >= Incumbent " + FormatNumber(bestObj) + " - Fathomed");
                        prunedByBound = true;
                    }
                }

                if (prunedByBound) continue;

                // Check integer feasibility for integer variables
                bool allInteger = true;
                var fractionalVars = new List<Tuple<int, double>>(); // (index, fractionality)

                reporter.WriteLine(indent + "Integer Feasibility Check:");

                for (int j = 0; j < model.NumVars; j++)
                {
                    string vtype = (model.VariableTypes != null && j < model.VariableTypes.Count && model.VariableTypes[j] != null)
                        ? model.VariableTypes[j].ToLower().Trim()
                        : string.Empty;

                    bool isInt = (vtype == "int" || vtype == "integer");
                    bool isBin = (vtype == "bin" || vtype == "binary");

                    if (isInt || isBin)
                    {
                        double val = (j < sol.Length) ? sol[j] : 0.0;
                        double rounded = Math.Round(val);
                        bool isIntegerValue = Math.Abs(val - rounded) <= TOL;

                        // For binary variables, also check if value is within [0, 1]
                        if (isBin)
                        {
                            bool isValidBinary = isIntegerValue && (Math.Abs(val) <= TOL || Math.Abs(val - 1.0) <= TOL);
                            reporter.WriteLine(indent + "   x" + (j + 1) + " = " + FormatNumber(val) + " (binary) -> " + (isValidBinary ? "OK" : "FRACTIONAL"));

                            if (!isValidBinary)
                            {
                                allInteger = false;
                                // Calculate fractionality for binary variables
                                double frac = Math.Min(Math.Abs(val), Math.Abs(val - 1.0));
                                fractionalVars.Add(new Tuple<int, double>(j, frac));
                            }
                        }
                        else
                        {
                            reporter.WriteLine(indent + "   x" + (j + 1) + " = " + FormatNumber(val) + " (integer) -> " + (isIntegerValue ? "OK" : "FRACTIONAL"));

                            if (!isIntegerValue)
                            {
                                allInteger = false;
                                // Calculate fractionality for integer variables
                                double frac = val - Math.Floor(val);
                                frac = Math.Min(frac, 1.0 - frac); // Distance to nearest integer
                                fractionalVars.Add(new Tuple<int, double>(j, frac));
                            }
                        }
                    }
                }

                // If all integer variables have integer values -> feasible integer solution
                if (allInteger)
                {
                    reporter.WriteLine(indent + "INTEGER FEASIBLE SOLUTION FOUND!");

                    // Round solution to ensure clean integer values
                    double[] cleanSol = RoundIntegerSolution(sol, model);

                    // Update incumbent
                    if (!foundInteger)
                    {
                        bestObj = nodeObj;
                        bestX = cleanSol;
                        foundInteger = true;
                        reporter.WriteLine(indent + "NEW INCUMBENT: " + FormatNumber(bestObj));
                        nodeResults.Add("Node " + nodeCounter + " (" + nodeName + "): Integer Feasible - NEW INCUMBENT " + FormatNumber(bestObj));
                    }
                    else
                    {
                        bool better = model.IsMaximization ? (nodeObj > bestObj + TOL) : (nodeObj < bestObj - TOL);
                        if (better)
                        {
                            bestObj = nodeObj;
                            bestX = cleanSol;
                            reporter.WriteLine(indent + "NEW INCUMBENT: " + FormatNumber(bestObj) + " (improved from previous)");
                            nodeResults.Add("Node " + nodeCounter + " (" + nodeName + "): Integer Feasible - NEW INCUMBENT " + FormatNumber(bestObj));
                        }
                        else
                        {
                            reporter.WriteLine(indent + "Integer feasible but worse than incumbent (" + FormatNumber(nodeObj) + " vs " + FormatNumber(bestObj) + ")");
                            nodeResults.Add("Node " + nodeCounter + " (" + nodeName + "): Integer Feasible but worse than incumbent - Discarded");
                        }
                    }

                    // Fathom this node since it's integer feasible
                    continue;
                }

                // Not integer feasible - must branch
                if (fractionalVars.Count == 0)
                {
                    reporter.WriteLine(indent + "No fractional integer variable found but not integer-feasible -> fathomed");
                    nodeResults.Add("Node " + nodeCounter + " (" + nodeName + "): No fractional variable found - Fathomed");
                    continue;
                }

                // Prevent excessive branching depth
                if (depth >= 10)
                {
                    reporter.WriteLine(indent + "Maximum depth reached - stopping branching");
                    nodeResults.Add("Node " + nodeCounter + " (" + nodeName + "): Maximum depth reached - Fathomed");
                    continue;
                }

                // Select variable with maximum fractionality for branching (most fractional first)
                fractionalVars.Sort((a, b) => b.Item2.CompareTo(a.Item2));
                int fracIndex = fractionalVars[0].Item1;
                double valFrac = sol[fracIndex];

                // Determine branching values based on variable type
                string varType = (model.VariableTypes != null && fracIndex < model.VariableTypes.Count)
                    ? model.VariableTypes[fracIndex]?.ToLower()?.Trim() ?? ""
                    : "";

                double floorVal, ceilVal;
                if (varType == "bin" || varType == "binary")
                {
                    // Binary variable: branch on 0 and 1
                    floorVal = 0.0;
                    ceilVal = 1.0;
                }
                else
                {
                    // Integer variable: branch on floor and ceiling
                    floorVal = Math.Floor(valFrac);
                    ceilVal = Math.Ceiling(valFrac);
                }

                reporter.WriteLine(indent + "BRANCHING on x" + (fracIndex + 1) + " = " + FormatNumber(valFrac));
                reporter.WriteLine(indent + "   Left branch:  x" + (fracIndex + 1) + " <= " + FormatNumber(floorVal));
                reporter.WriteLine(indent + "   Right branch: x" + (fracIndex + 1) + " >= " + FormatNumber(ceilVal));

                // Create left and right child subproblems
                LpModel leftChild = CloneModel(nodeModel);
                AddSimpleBoundConstraint(leftChild, fracIndex, true, floorVal);

                LpModel rightChild = CloneModel(nodeModel);
                AddSimpleBoundConstraint(rightChild, fracIndex, false, ceilVal);

                // Create descriptive names
                string leftName = nodeName + ".L[x" + (fracIndex + 1) + "<=" + FormatNumber(floorVal) + "]";
                string rightName = nodeName + ".R[x" + (fracIndex + 1) + ">=" + FormatNumber(ceilVal) + "]";

                // Add children to priority queue
                // Use LP relaxation bound as priority
                AddToQueue(pQueue, nodeObj, leftChild, leftName, depth + 1, nodeIdCounter++, model.IsMaximization);
                AddToQueue(pQueue, nodeObj, rightChild, rightName, depth + 1, nodeIdCounter++, model.IsMaximization);

                nodeResults.Add("Node " + nodeCounter + " (" + nodeName + "): Branched on x" + (fracIndex + 1) + " = " + FormatNumber(valFrac));
            }

            // Check if we hit the node limit
            if (nodeCounter >= maxNodes)
            {
                reporter.WriteLine("WARNING: Maximum number of nodes (" + maxNodes + ") reached. Search terminated.");
            }

            // Final summary and results
            reporter.WriteLine("");
            reporter.WriteLine("");
            reporter.WriteLine("===============================================");
            reporter.WriteLine("=== BRANCH & BOUND SUMMARY ===");
            reporter.WriteLine("===============================================");

            reporter.WriteLine("Total nodes explored: " + nodeCounter);
            reporter.WriteLine("Nodes remaining in queue: " + CountNodesInQueue(pQueue));
            reporter.WriteLine("Node exploration summary:");
            foreach (string result in nodeResults)
            {
                reporter.WriteLine("  - " + result);
            }

            reporter.WriteLine("");
            reporter.WriteLine("===============================================");

            if (foundInteger)
            {
                reporter.WriteLine("=== OPTIMAL INTEGER SOLUTION FOUND ===");
                reporter.WriteLine("===============================================");
                reporter.WriteLine("Best Objective Value: " + FormatNumber(bestObj));
                reporter.WriteLine("Optimal Variable Values:");
                for (int i = 0; i < model.NumVars; i++)
                {
                    double v = (bestX != null && i < bestX.Length) ? bestX[i] : 0.0;
                    reporter.WriteLine("   x" + (i + 1) + " = " + FormatNumber(v));
                }

                return new LPResult
                {
                    Status = LPStatus.Optimal,
                    ObjectiveValue = bestObj,
                    VariableValues = bestX,
                    Log = reporter.GetLog()
                };
            }
            else
            {
                reporter.WriteLine("=== NO INTEGER FEASIBLE SOLUTION EXISTS ===");
                reporter.WriteLine("===============================================");
                reporter.WriteLine("All nodes were fathomed without finding an integer feasible solution.");

                return new LPResult
                {
                    Status = LPStatus.Infeasible,
                    ObjectiveValue = double.NaN,
                    VariableValues = new double[model.NumVars],
                    Log = reporter.GetLog()
                };
            }
        }

        private LpModel AddBinaryBounds(LpModel model, IReporter reporter)
        {
            if (model.VariableTypes == null || model.VariableTypes.Count == 0)
                return model;

            var bounded = CloneModel(model);
            int addedBounds = 0;

            for (int i = 0; i < model.NumVars && i < model.VariableTypes.Count; i++)
            {
                string type = model.VariableTypes[i]?.ToLower()?.Trim() ?? "";
                if (type == "bin" || type == "binary")
                {
                    // Add x_i >= 0
                    AddSimpleBoundConstraint(bounded, i, false, 0.0);
                    // Add x_i <= 1
                    AddSimpleBoundConstraint(bounded, i, true, 1.0);
                    addedBounds += 2;
                }
                else if (type == "int" || type == "integer")
                {
                    // Add non-negativity for integer variables
                    AddSimpleBoundConstraint(bounded, i, false, 0.0);
                    addedBounds++;
                }
            }

            if (addedBounds > 0)
            {
                reporter.WriteLine("Added " + addedBounds + " bound constraints for binary/integer variables");
                reporter.WriteLine("");
            }

            return bounded;
        }

        private void AddToQueue(SortedList<double, List<Tuple<LpModel, string, int, int>>> pQueue,
            double priority, LpModel model, string name, int depth, int nodeId, bool isMax)
        {
            // For maximization, we want higher bounds first (negative for ascending sort)
            // For minimization, we want lower bounds first (positive for ascending sort)
            double key = isMax ? -priority : priority;

            // Add small perturbation to avoid key collisions
            key += nodeId * 1e-10;

            if (!pQueue.ContainsKey(key))
                pQueue[key] = new List<Tuple<LpModel, string, int, int>>();

            pQueue[key].Add(new Tuple<LpModel, string, int, int>(model, name, depth, nodeId));
        }

        private int CountNodesInQueue(SortedList<double, List<Tuple<LpModel, string, int, int>>> pQueue)
        {
            int count = 0;
            foreach (var list in pQueue.Values)
                count += list.Count;
            return count;
        }

        private double[] RoundIntegerSolution(double[] solution, LpModel model)
        {
            var rounded = new double[solution.Length];
            for (int i = 0; i < solution.Length; i++)
            {
                if (i < model.NumVars && model.VariableTypes != null && i < model.VariableTypes.Count)
                {
                    string type = model.VariableTypes[i]?.ToLower()?.Trim() ?? "";
                    if (type == "int" || type == "integer" || type == "bin" || type == "binary")
                    {
                        rounded[i] = Math.Round(solution[i]);
                    }
                    else
                    {
                        rounded[i] = solution[i];
                    }
                }
                else
                {
                    rounded[i] = solution[i];
                }
            }
            return rounded;
        }

        private void DisplayCanonicalForm(LpModel model, IReporter reporter)
        {
            reporter.WriteLine("===============================================");
            reporter.WriteLine("=== CANONICAL FORM ===");
            reporter.WriteLine("===============================================");

            // Display objective function
            reporter.WriteLine("Objective Function:");
            StringBuilder objStr = new StringBuilder();
            objStr.Append(model.IsMaximization ? "Maximize: " : "Minimize: ");

            bool first = true;
            for (int i = 0; i < model.ObjectiveCoeffs.Count; i++)
            {
                double coeff = model.ObjectiveCoeffs[i];
                if (Math.Abs(coeff) < TOL) continue; // Skip zero coefficients

                if (!first)
                {
                    objStr.Append(coeff >= 0 ? " + " : " - ");
                    objStr.Append(FormatNumber(Math.Abs(coeff)));
                }
                else
                {
                    if (coeff < 0) objStr.Append("-");
                    objStr.Append(FormatNumber(Math.Abs(coeff)));
                    first = false;
                }
                objStr.Append("x" + (i + 1));
            }
            reporter.WriteLine("  " + objStr.ToString());

            reporter.WriteLine("");
            reporter.WriteLine("Subject to:");

            // Display constraints
            for (int i = 0; i < model.Constraints.Count; i++)
            {
                var constraint = model.Constraints[i];
                StringBuilder conStr = new StringBuilder("  ");

                bool firstTerm = true;
                for (int j = 0; j < constraint.Coefficients.Count; j++)
                {
                    double coeff = constraint.Coefficients[j];
                    if (Math.Abs(coeff) < TOL) continue; // Skip zero coefficients

                    if (!firstTerm)
                    {
                        conStr.Append(coeff >= 0 ? " + " : " - ");
                        conStr.Append(FormatNumber(Math.Abs(coeff)));
                    }
                    else
                    {
                        if (coeff < 0) conStr.Append("-");
                        conStr.Append(FormatNumber(Math.Abs(coeff)));
                        firstTerm = false;
                    }
                    conStr.Append("x" + (j + 1));
                }

                conStr.Append(" " + constraint.Relation + " " + FormatNumber(constraint.RHS));
                reporter.WriteLine(conStr.ToString());
            }

            reporter.WriteLine("");
            reporter.WriteLine("Variable Types:");

            if (model.VariableTypes != null && model.VariableTypes.Count > 0)
            {
                for (int i = 0; i < model.NumVars && i < model.VariableTypes.Count; i++)
                {
                    string type = model.VariableTypes[i] != null ? model.VariableTypes[i].ToLower().Trim() : "continuous";
                    string typeDesc;
                    if (type == "bin" || type == "binary")
                        typeDesc = "Binary (0 or 1)";
                    else if (type == "int" || type == "integer")
                        typeDesc = "Integer";
                    else if (type == "+")
                        typeDesc = "Non-negative";
                    else if (type == "-")
                        typeDesc = "Non-positive";
                    else
                        typeDesc = "Continuous";

                    reporter.WriteLine("  x" + (i + 1) + ": " + typeDesc);
                }
            }
            else
            {
                reporter.WriteLine("  All variables are continuous");
            }

            reporter.WriteLine("===============================================");
            reporter.WriteLine("");
        }

        // Adds a simple constraint: x_i <= rhs (isUpper==true) or x_i >= rhs (isUpper==false)
        private static void AddSimpleBoundConstraint(LpModel model, int varIndex, bool isUpper, double rhs)
        {
            int n = model.NumVars;
            var constraint = new Constraint();

            // Initialize coefficients to zero
            for (int j = 0; j < n; j++)
                constraint.Coefficients.Add(0.0);

            // Set coefficient for the target variable
            constraint.Coefficients[varIndex] = 1.0;
            constraint.Relation = isUpper ? "<=" : ">=";
            constraint.RHS = rhs;

            model.Constraints.Add(constraint);
        }

        // Deep clone the model
        private static LpModel CloneModel(LpModel src)
        {
            var copy = new LpModel();
            copy.ProblemType = src.ProblemType;

            // Copy objective coefficients
            foreach (double d in src.ObjectiveCoeffs)
                copy.ObjectiveCoeffs.Add(d);

            // Copy variable types
            if (src.VariableTypes != null)
            {
                foreach (string s in src.VariableTypes)
                    copy.VariableTypes.Add(s == null ? null : string.Copy(s));
            }

            // Copy constraints
            foreach (var c in src.Constraints)
            {
                var cc = new Constraint();
                cc.Relation = c.Relation;
                cc.RHS = c.RHS;
                foreach (double d in c.Coefficients)
                    cc.Coefficients.Add(d);
                copy.Constraints.Add(cc);
            }

            return copy;
        }

        private static string FormatNumber(double value)
        {
            if (Math.Abs(value) < TOL) value = 0.0;
            if (Math.Abs(value - Math.Round(value)) < TOL)
            {
                return Math.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                return value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}