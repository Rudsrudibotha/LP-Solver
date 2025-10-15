using System;
using System.Globalization;
using LP_Solver.Core;
using LP_Solver.Core.Models;
using LP_Solver.IO;

namespace LP_Solver.Menu
{
    internal static class SpecialCasesMenu
    {
        public static void Run()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("===== Special Cases =====");
                Console.WriteLine("1. Detect Infeasible Model (Phase I)");
                Console.WriteLine("2. Detect Unbounded Model");
                Console.WriteLine("3. Back");
                Console.Write("Enter choice: ");

                var choice = Console.ReadLine();
                switch (choice)
                {
                    case "1":
                        DetectInfeasible();
                        break;
                    case "2":
                        DetectUnbounded();
                        break;
                    case "3":
                        return;
                    default:
                        Console.WriteLine("Invalid choice. Press Enter to continue...");
                        Console.ReadLine();
                        break;
                }
            }
        }

        private static void DetectInfeasible()
        {
            Console.Clear();
            Console.Write("Enter input file path (leave blank for 'input.txt'): ");
            var path = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(path)) path = "input.txt";

            LpModel model;
            try
            {
                model = ReadFile.ReadFromFile(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading model: " + ex.Message);
                Console.WriteLine("\nPress Enter to return...");
                Console.ReadLine();
                return;
            }

            var reporter = new LP_Solver.IO.StringBuilderReporter();
            var handler = new SpecialCasesHandler(reporter);

            // Quick checks (contradictions / conflicting equalities)
            var infeas = handler.CheckInfeasibility(model);
            if (infeas != null && infeas.IsInfeasible)
            {
                handler.AnalyzeInfeasibility(model, infeas);
                Console.WriteLine(reporter.GetText());
                Console.WriteLine("\nPress Enter to continue...");
                Console.ReadLine();
                return;
            }

            // Phase I (Two-Phase) feasibility check
            handler.SolvePhaseOne(model);
            Console.WriteLine(reporter.GetText());

            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadLine();
        }

        private static void DetectUnbounded()
        {
            Console.Clear();
            var reporter = new LP_Solver.IO.StringBuilderReporter();
            var handler = new SpecialCasesHandler(reporter);

            if (SolverArtifacts.LastTableau == null || SolverArtifacts.LastBasicVars == null)
            {
                Console.WriteLine("No solved tableau available. Solve the model first.");
                Console.WriteLine("\nPress Enter to continue...");
                Console.ReadLine();
                return;
            }

            var result = handler.CheckUnboundedness(
                SolverArtifacts.LastTableau,
                SolverArtifacts.LastBasicVars,
                SolverArtifacts.IsMaximization
            );

            Console.WriteLine(reporter.GetText());

            if (result != null && result.IsUnbounded)
            {
                Console.WriteLine("\n*** PROBLEM IS UNBOUNDED ***");
                Console.WriteLine($"Variable x{result.UnboundedVariable + 1} can grow without bound.");
                Console.WriteLine("Unbounded direction (ray):");
                for (int i = 0; i < result.UnboundedDirection.Length; i++)
                {
                    var v = result.UnboundedDirection[i];
                    if (Math.Abs(v) > 1e-9)
                        Console.WriteLine($"  x{i + 1} = {v.ToString("F3", CultureInfo.InvariantCulture)}");
                }
            }

            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadLine();
        }
    }
}
