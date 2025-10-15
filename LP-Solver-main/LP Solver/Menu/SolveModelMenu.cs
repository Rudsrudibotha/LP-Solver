using LP_Solver.Core;
using LP_Solver.Core.Models;
using System;

namespace LP_Solver.Menu
{
    internal static class SolveModelMenu
    {
        public static void Run(LpModel model, ref string lastSolverLog)
        {
            if (model == null)
            {
                Console.WriteLine("No model loaded. Load a model first.");
                Console.ReadLine();
                return;
            }

            bool back = false;

            while (!back)
            {
                Console.Clear();
                Console.WriteLine("===== Solve Model Menu =====");
                Console.WriteLine("1. Primal Simplex");
                Console.WriteLine("2. Revised Simplex");
                Console.WriteLine("3. Branch & Bound (Simplex)");
                Console.WriteLine("4. Cutting Plane");
                Console.WriteLine("5. Branch & Bound (Knapsack)");
                Console.WriteLine("6. Sensitivity Analysis");
                Console.WriteLine("7. Back to Main Menu");
                Console.Write("Enter choice: ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        RunPrimalSimplex(model, ref lastSolverLog);
                        break;
                    case "2":
                        RunRevisedSimplex(model, ref lastSolverLog);
                        break;
                    case "3":
                        RunBranchAndBound(model, ref lastSolverLog);
                        break;
                    case "4":
                        RunCuttingPlane(model, ref lastSolverLog);
                        break;
                    case "5":
                        RunBranchAndBoundKnapsack(model, ref lastSolverLog);
                        break;
                    case "6":
                        RunSensitivityAnalysis (model, ref lastSolverLog);
                        break;
                    case "7":
                        back = true;
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Press Enter to try again...");
                        Console.ReadLine();
                        break;
                }
            }
        }

        private static void RunPrimalSimplex(LpModel model, ref string lastSolverLog)
        {
            IReporter reporter = new ConsoleReporter();
            ISolver solver = new PrimalSimplex();
            SolverOptions options = new SolverOptions { ShowIterations = true };

            LPResult result = solver.Solve(model, options, reporter);
            lastSolverLog = reporter.GetLog();

            Console.Clear();
            Console.WriteLine(result.Log);
            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadLine();
        }

        private static void RunRevisedSimplex(LpModel model, ref string lastSolverLog)
        {
            IReporter reporter = new ConsoleReporter();
            ISolver solver = new RevisedSimplex();
            SolverOptions options = new SolverOptions { ShowIterations = true };

            LPResult result = solver.Solve(model, options, reporter);
            lastSolverLog = reporter.GetLog();

            Console.Clear();
            Console.WriteLine(result.Log);
            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadLine();
        }

        private static void RunBranchAndBound(LpModel model, ref string lastSolverLog)
        {
            IReporter reporter = new ConsoleReporter();
            ISolver solver = new BranchAndBoundSimplex();
            SolverOptions options = new SolverOptions { ShowIterations = true };

            Console.Clear();

            LPResult result = solver.Solve(model, options, reporter);
            lastSolverLog = reporter.GetLog();

            Console.WriteLine(result.Log);
            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadLine();
        }

        private static void RunCuttingPlane(LpModel model, ref string lastSolverLog)
        {
            try
            {
                IReporter reporter = new ConsoleReporter();
                ISolver solver = new CuttingPlane();
                SolverOptions options = new SolverOptions { ShowIterations = true };

                Console.Clear();

                LPResult result = solver.Solve(model, options, reporter);
                lastSolverLog = reporter.GetLog();

                Console.Clear();
                Console.WriteLine(result.Log);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error running Cutting Plane solver: " + ex.Message);
                lastSolverLog = "Cutting Plane solver failed: " + ex.Message;
            }

            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadLine();
        }

        private static void RunBranchAndBoundKnapsack(LpModel model, ref string lastSolverLog)
        {
            IReporter reporter = new ConsoleReporter();
            ISolver solver = new BranchAndBoundKnapsack();
            SolverOptions options = new SolverOptions { ShowIterations = true };

            // Clear console before running for fresh look
            Console.Clear();

            LPResult result = solver.Solve(model, options, reporter);
            lastSolverLog = reporter.GetLog();

            Console.WriteLine(result.Log);
            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadLine();
        }

        private static void RunSensitivityAnalysis(LP_Solver.Core.Models.LpModel model, ref string lastSolverLog)
        {
            IReporter reporter = new ConsoleReporter();
            var sensSolver = new PrimalSimplex();                  // concrete type, not ISolver
            var options = new SolverOptions { ShowIterations = true };

            Console.Clear();

            LPREsults sens = sensSolver.SolveForSensitivity(model, options, reporter);
            lastSolverLog = reporter.GetLog();

            if (sens == null || !sens.IsOptimal || sens.FinalTableau == null)
            {
                Console.WriteLine(sens == null
                    ? "No result returned from solver."
                    : (!sens.IsOptimal ? "Sensitivity analysis requires an optimal solution."
                                       : "Final tableau not available."));
                Console.WriteLine(sens != null ? sens.Log : "");
                Console.WriteLine("\nPress Enter to continue...");
                Console.ReadLine();
                return;
            }

            // Adapt to your existing menu signature which expects LPResult
            var shim = new LPResult
            {
                Status = sens.Status,
                ObjectiveValue = sens.ObjectiveValue,
                VariableValues = sens.VariableValues,
                Log = sens.Log
            };

            var menu = new LP_Solver.Core.SensitivityAnalysisMenu(model, shim, sens.FinalTableau, reporter);
            menu.DisplayMenu();
        }
    }
}