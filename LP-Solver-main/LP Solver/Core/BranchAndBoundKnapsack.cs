using System;
using System.Collections.Generic;
using LP_Solver.Core.Models;

namespace LP_Solver.Core
{
    internal class BranchAndBoundKnapsack : ISolver
    {
        private const double TOL = 1e-9;

        public LPResult Solve(LpModel model, SolverOptions options, IReporter reporter)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (reporter == null) reporter = new StringBuilderReporter();
            if (options == null) options = new SolverOptions();

            reporter.WriteLine("===============================================");
            reporter.WriteLine("=== Branch & Bound Knapsack Algorithm ===");
            reporter.WriteLine("===============================================");

            double[] profits = model.ObjectiveCoeffs.ToArray();
            int n = profits.Length;

            if (model.Constraints.Count == 0)
                throw new ArgumentException("Knapsack problem must have one constraint representing capacity.");

            double capacity = model.Constraints[0].RHS;
            double[] weights = model.Constraints[0].Coefficients.ToArray();

            // Store best solution
            double bestValue = 0.0;
            bool[] bestSolution = new bool[n];

            // Stack for DFS backtracking: index, current value, current weight, selected items
            var stack = new Stack<Tuple<int, double, double, bool[]>>();
            stack.Push(new Tuple<int, double, double, bool[]>(0, 0.0, 0.0, new bool[n]));

            int nodeCounter = 0;

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                int idx = node.Item1;
                double value = node.Item2;
                double weight = node.Item3;
                bool[] taken = node.Item4;

                nodeCounter++;

                // Display table iteration for this node
                reporter.WriteLine("Node " + nodeCounter + ": Depth " + idx);
                reporter.WriteLine("   Current value: " + value + ", Current weight: " + weight);
                reporter.WriteLine("   Items taken: [" + string.Join(", ", Array.ConvertAll(taken, b => b ? "1" : "0")) + "]");
                reporter.WriteLine("------------------------------------------------");

                if (idx >= n)
                {
                    // Leaf node: check if it's the best solution
                    if (value > bestValue)
                    {
                        bestValue = value;
                        Array.Copy(taken, bestSolution, n);
                        reporter.WriteLine("   >>> New best candidate found! Objective = " + bestValue);
                    }
                    continue;
                }

                // Branch: include item
                if (weight + weights[idx] <= capacity)
                {
                    bool[] takeCopy = new bool[n];
                    Array.Copy(taken, takeCopy, n);
                    takeCopy[idx] = true;
                    stack.Push(new Tuple<int, double, double, bool[]>(idx + 1, value + profits[idx], weight + weights[idx], takeCopy));
                }

                // Branch: exclude item
                bool[] skipCopy = new bool[n];
                Array.Copy(taken, skipCopy, n);
                skipCopy[idx] = false;
                stack.Push(new Tuple<int, double, double, bool[]>(idx + 1, value, weight, skipCopy));
            }

            // Final result
            double[] solutionValues = new double[n];
            for (int i = 0; i < n; i++)
                solutionValues[i] = bestSolution[i] ? 1.0 : 0.0;

            reporter.WriteLine("");
            reporter.WriteLine("===============================================");
            reporter.WriteLine("=== FINAL BEST SOLUTION ===");
            reporter.WriteLine("Objective Value: " + bestValue);
            reporter.WriteLine("Selected Items: [" + string.Join(", ", solutionValues) + "]");
            reporter.WriteLine("===============================================");

            return new LPResult
            {
                Status = LPStatus.Optimal,
                ObjectiveValue = bestValue,
                VariableValues = solutionValues,
                Log = reporter.GetLog()
            };
        }
    }
}