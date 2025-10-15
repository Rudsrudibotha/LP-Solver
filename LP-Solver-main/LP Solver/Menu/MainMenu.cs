using LP_Solver.Core.Models;
using LP_Solver.IO;
using System;
using System.Linq;
using System.Text;

namespace LP_Solver.Menu
{
    internal static class MainMenu
    {
        private static LpModel _currentModel;
        private static string _lastSolverLog;

        public static void Run()
        {
            bool exit = false;
            while (!exit)
            {
                Console.Clear();
                Console.WriteLine("=======================================");
                Console.WriteLine("   Linear & Integer Programming Solver ");
                Console.WriteLine("=======================================");
                Console.WriteLine("1. Load Model");
                Console.WriteLine("2. Solve Model");
                Console.WriteLine("3. Sensitivity Analysis");
                Console.WriteLine("4. Export Results");
                Console.WriteLine("5. Special Cases");
                Console.WriteLine("6. Bonus: Non-Linear Solver"); // <-- updated to match switch
                Console.WriteLine("7. Exit");                      // <-- updated to match switch
                Console.WriteLine("=======================================");
                Console.Write("Enter choice: ");

                var choice = Console.ReadLine();
                switch (choice)
                {
                    case "1":
                        LoadModel();
                        break;

                    case "2":
                        // Keep: Solve flow uses the loaded model and appends to _lastSolverLog
                        SolveModelMenu.Run(_currentModel, ref _lastSolverLog);
                        break;

                    case "3":
                        // Keep: Sensitivity analysis menu
                        SensitivityAnalysisMenu.Run();
                        break;

                    case "4":
                        // Keep: Export last solver log or model summary
                        ExportResults();
                        break;

                    case "5":
                        // Keep: Special Cases menu (parameterless version you already have)
                        SpecialCasesMenu.Run();
                        break;

                    case "6":
                        // Keep: Bonus Non-Linear Solver
                        BonusNonLinearSolverMenu.Run();
                        break;

                    case "7":
                        exit = true;
                        break;

                    default:
                        Console.WriteLine("Invalid choice. Press Enter to try again...");
                        Console.ReadLine();
                        break;
                }
            }
        }

        private static void LoadModel()
        {
            Console.Clear();
            Console.Write("Enter input file path (leave blank for 'input.txt'): ");
            string path = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(path)) path = "input.txt";

            try
            {
                _currentModel = ReadFile.ReadFromFile(path);
                Console.WriteLine("Model loaded successfully.");
                Console.WriteLine("Variables (n) = " + _currentModel.NumVars + ", Constraints (m) = " + _currentModel.NumCons);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading model: " + ex.Message);
            }

            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
        }

        private static void ExportResults()
        {
            Console.Clear();

            if (_currentModel == null && string.IsNullOrEmpty(_lastSolverLog))
            {
                Console.WriteLine("No model or results to export. Load and/or solve a model first.");
                Console.ReadLine();
                return;
            }

            Console.Write("Enter output file path (leave blank for 'output.txt'): ");
            string path = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(path)) path = "output.txt";

            try
            {
                if (!string.IsNullOrEmpty(_lastSolverLog))
                {
                    ModelWriter.WriteLogFile(path, "Solver Results", _lastSolverLog);
                }
                else if (_currentModel != null)
                {
                    string summary = BuildModelSummary(_currentModel);
                    ModelWriter.WriteLogFile(path, "Model Summary", summary);
                }

                Console.WriteLine("Results written to: " + path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing output: " + ex.Message);
            }

            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
        }

        private static string BuildModelSummary(LpModel model)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Problem Type: " + model.ProblemType);
            sb.AppendLine("Number of Variables: " + model.NumVars);
            sb.AppendLine("Number of Constraints: " + model.NumCons);
            sb.AppendLine();
            sb.AppendLine("Objective Coefficients:");
            sb.AppendLine(string.Join(", ", model.ObjectiveCoeffs.Select(x => x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture))));
            sb.AppendLine();
            sb.AppendLine("Constraints (Coefficients | Relation | RHS):");

            foreach (var con in model.Constraints)
            {
                sb.AppendLine(string.Join(" ", con.Coefficients.Select(v => v.ToString("F3", System.Globalization.CultureInfo.InvariantCulture))) +
                              "  " + con.Relation + "  " + con.RHS.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            }

            sb.AppendLine();
            sb.AppendLine("Variable Types: " + string.Join(", ", model.VariableTypes));

            return sb.ToString();
        }
    }
}
