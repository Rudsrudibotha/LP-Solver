using LP_Solver.Core.Models;
using LP_Solver.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LP_Solver.Core
{
    public class SensitivityAnalysisMenu
    {
        private LpModel model;
        private LPResult optimalSolution;
        private SimplexTableau finalTableau;
        private SensitivityAnalyzer analyzer;
        private IReporter reporter;

        public SensitivityAnalysisMenu(LpModel model, LPResult solution, SimplexTableau tableau, IReporter reporter = null)
        {
            this.model = model;
            this.optimalSolution = solution;
            this.finalTableau = tableau;
            this.reporter = reporter ?? new FileReporter("sensitivity_output.txt");
            this.analyzer = new SensitivityAnalyzer(model, solution, tableau, this.reporter);
        }

        public void DisplayMenu()
        {
            bool exit = false;

            while (!exit)
            {
                Console.Clear();
                Console.WriteLine("===============================================");
                Console.WriteLine("       SENSITIVITY ANALYSIS MENU");
                Console.WriteLine("===============================================");
                Console.WriteLine();
                Console.WriteLine("Current Optimal Solution:");
                Console.WriteLine($"  Objective Value: {FormatNumber(optimalSolution.ObjectiveValue)}");
                Console.WriteLine($"  Number of Variables: {model.NumVars}");
                Console.WriteLine($"  Number of Constraints: {model.NumCons}");
                Console.WriteLine();
                Console.WriteLine("Select an analysis option:");
                Console.WriteLine();
                Console.WriteLine("  1.  Display range of a Non-Basic Variable");
                Console.WriteLine("  2.  Apply change to a Non-Basic Variable");
                Console.WriteLine("  3.  Display range of a Basic Variable");
                Console.WriteLine("  4.  Apply change to a Basic Variable");
                Console.WriteLine("  5.  Display range of constraint RHS");
                Console.WriteLine("  6.  Apply change to constraint RHS");
                Console.WriteLine("  7.  Display range of Non-Basic column coefficient");
                Console.WriteLine("  8.  Apply change to Non-Basic column coefficient");
                Console.WriteLine("  9.  Add new activity");
                Console.WriteLine("  10. Add new constraint");
                Console.WriteLine("  11. Display shadow prices");
                Console.WriteLine("  12. Perform duality analysis");
                Console.WriteLine("  13. Export all analyses to file");
                Console.WriteLine("  14. Display current solution details");
                Console.WriteLine("  0.  Exit Sensitivity Analysis");
                Console.WriteLine();
                Console.Write("Enter your choice: ");

                string choice = Console.ReadLine();
                Console.WriteLine();

                try
                {
                    switch (choice)
                    {
                        case "1":
                            AnalyzeNonBasicVariableRange();
                            break;
                        case "2":
                            ApplyNonBasicVariableChange();
                            break;
                        case "3":
                            AnalyzeBasicVariableRange();
                            break;
                        case "4":
                            ApplyBasicVariableChange();
                            break;
                        case "5":
                            AnalyzeRHSRange();
                            break;
                        case "6":
                            ApplyRHSChange();
                            break;
                        case "7":
                            AnalyzeNonBasicColumnRange();
                            break;
                        case "8":
                            ApplyNonBasicColumnChange();
                            break;
                        case "9":
                            AddNewActivity();
                            break;
                        case "10":
                            AddNewConstraint();
                            break;
                        case "11":
                            DisplayShadowPrices();
                            break;
                        case "12":
                            PerformDualityAnalysis();
                            break;
                        case "13":
                            ExportAllAnalyses();
                            break;
                        case "14":
                            DisplayCurrentSolution();
                            break;
                        case "0":
                            exit = true;
                            Console.WriteLine("Exiting Sensitivity Analysis...");
                            break;
                        default:
                            Console.WriteLine("Invalid choice. Please try again.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }

                if (!exit)
                {
                    Console.WriteLine();
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }
            }
        }

        private void AnalyzeNonBasicVariableRange()
        {
            Console.WriteLine("=== Non-Basic Variable Range Analysis ===");
            int varIndex = GetVariableIndex("Enter variable index (1 to " + model.NumVars + "): ");

            analyzer.DisplayNonBasicVariableRange(varIndex);
            DisplayAnalysisResult();
        }

        private void ApplyNonBasicVariableChange()
        {
            Console.WriteLine("=== Apply Non-Basic Variable Change ===");
            int varIndex = GetVariableIndex("Enter variable index (1 to " + model.NumVars + "): ");
            double newCoeff = GetDoubleInput("Enter new coefficient value: ");

            analyzer.ApplyNonBasicVariableChange(varIndex, newCoeff);
            DisplayAnalysisResult();
        }

        private void AnalyzeBasicVariableRange()
        {
            Console.WriteLine("=== Basic Variable Range Analysis ===");
            int varIndex = GetVariableIndex("Enter variable index (1 to " + model.NumVars + "): ");

            analyzer.DisplayBasicVariableRange(varIndex);
            DisplayAnalysisResult();
        }

        private void ApplyBasicVariableChange()
        {
            Console.WriteLine("=== Apply Basic Variable Change ===");
            int varIndex = GetVariableIndex("Enter variable index (1 to " + model.NumVars + "): ");
            double newCoeff = GetDoubleInput("Enter new coefficient value: ");

            analyzer.ApplyBasicVariableChange(varIndex, newCoeff);
            DisplayAnalysisResult();
        }

        private void AnalyzeRHSRange()
        {
            Console.WriteLine("=== Constraint RHS Range Analysis ===");
            int constraintIndex = GetConstraintIndex("Enter constraint index (1 to " + model.NumCons + "): ");

            analyzer.DisplayRHSRange(constraintIndex);
            DisplayAnalysisResult();
        }

        private void ApplyRHSChange()
        {
            Console.WriteLine("=== Apply Constraint RHS Change ===");
            int constraintIndex = GetConstraintIndex("Enter constraint index (1 to " + model.NumCons + "): ");
            double newRHS = GetDoubleInput("Enter new RHS value: ");

            analyzer.ApplyRHSChange(constraintIndex, newRHS);
            DisplayAnalysisResult();
        }

        private void AnalyzeNonBasicColumnRange()
        {
            Console.WriteLine("=== Non-Basic Column Coefficient Range Analysis ===");
            int varIndex = GetVariableIndex("Enter non-basic variable index (1 to " + model.NumVars + "): ");
            int constraintIndex = GetConstraintIndex("Enter constraint index (1 to " + model.NumCons + "): ");

            analyzer.DisplayNonBasicColumnRange(varIndex, constraintIndex);
            DisplayAnalysisResult();
        }

        private void ApplyNonBasicColumnChange()
        {
            Console.WriteLine("=== Apply Non-Basic Column Coefficient Change ===");
            int varIndex = GetVariableIndex("Enter non-basic variable index (1 to " + model.NumVars + "): ");
            int constraintIndex = GetConstraintIndex("Enter constraint index (1 to " + model.NumCons + "): ");
            double newCoeff = GetDoubleInput("Enter new coefficient value: ");

            analyzer.ApplyNonBasicColumnChange(varIndex, constraintIndex, newCoeff);
            DisplayAnalysisResult();
        }

        private void AddNewActivity()
        {
            Console.WriteLine("=== Add New Activity ===");

            double objCoeff = GetDoubleInput("Enter objective coefficient for new activity: ");

            double[] coefficients = new double[model.NumCons];
            Console.WriteLine("Enter constraint coefficients for the new activity:");
            for (int i = 0; i < model.NumCons; i++)
            {
                coefficients[i] = GetDoubleInput($"  Constraint {i + 1}: ");
            }

            Console.Write("Enter name for new activity (default: 'new'): ");
            string name = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(name)) name = "new";

            analyzer.AddNewActivity(coefficients, objCoeff, name);
            DisplayAnalysisResult();
        }

        private void AddNewConstraint()
        {
            Console.WriteLine("=== Add New Constraint ===");

            double[] coefficients = new double[model.NumVars];
            Console.WriteLine("Enter coefficients for each variable:");
            for (int i = 0; i < model.NumVars; i++)
            {
                coefficients[i] = GetDoubleInput($"  x{i + 1}: ");
            }

            Console.Write("Enter relation (<=, >=, =): ");
            string relation = Console.ReadLine().Trim();
            while (relation != "<=" && relation != ">=" && relation != "=")
            {
                Console.Write("Invalid relation. Enter <=, >=, or =: ");
                relation = Console.ReadLine().Trim();
            }

            double rhs = GetDoubleInput("Enter RHS value: ");

            analyzer.AddNewConstraint(coefficients, relation, rhs);
            DisplayAnalysisResult();
        }

        private void DisplayShadowPrices()
        {
            Console.WriteLine("=== Shadow Prices Analysis ===");
            analyzer.DisplayShadowPrices();
            DisplayAnalysisResult();
        }

        private void PerformDualityAnalysis()
        {
            Console.WriteLine("=== Duality Analysis ===");
            analyzer.PerformDualityAnalysis();
            DisplayAnalysisResult();
        }

        private void ExportAllAnalyses()
        {
            Console.WriteLine("=== Exporting All Analyses ===");
            Console.Write("Enter output filename (default: sensitivity_full_report.txt): ");
            string filename = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(filename)) filename = "sensitivity_full_report.txt";

            var fullReporter = new FileReporter(filename);
            var fullAnalyzer = new SensitivityAnalyzer(model, optimalSolution, finalTableau, fullReporter);

            fullReporter.WriteLine("===============================================");
            fullReporter.WriteLine("    COMPLETE SENSITIVITY ANALYSIS REPORT");
            fullReporter.WriteLine("===============================================");
            fullReporter.WriteLine($"Generated: {DateTime.Now}");
            fullReporter.WriteLine();

            // Display current solution
            fullReporter.WriteLine("OPTIMAL SOLUTION:");
            fullReporter.WriteLine($"Objective Value: {FormatNumber(optimalSolution.ObjectiveValue)}");
            fullReporter.WriteLine("Variable Values:");
            for (int i = 0; i < model.NumVars; i++)
            {
                fullReporter.WriteLine($"  x{i + 1} = {FormatNumber(optimalSolution.VariableValues[i])}");
            }

            // Perform all analyses
            fullReporter.WriteLine("\n" + new string('=', 50));
            fullAnalyzer.DisplayShadowPrices();

            fullReporter.WriteLine("\n" + new string('=', 50));
            for (int i = 0; i < model.NumVars; i++)
            {
                if (IsBasicVariable(i))
                    fullAnalyzer.DisplayBasicVariableRange(i);
                else
                    fullAnalyzer.DisplayNonBasicVariableRange(i);
            }

            fullReporter.WriteLine("\n" + new string('=', 50));
            for (int i = 0; i < model.NumCons; i++)
            {
                fullAnalyzer.DisplayRHSRange(i);
            }

            fullReporter.WriteLine("\n" + new string('=', 50));
            fullAnalyzer.PerformDualityAnalysis();

            fullReporter.WriteLine("\n" + new string('=', 50));
            fullReporter.WriteLine("END OF REPORT");

            Console.WriteLine($"Full analysis exported to {filename}");
        }

        private void DisplayCurrentSolution()
        {
            Console.WriteLine("=== Current Optimal Solution ===");
            Console.WriteLine();
            Console.WriteLine($"Problem Type: {(model.IsMaximization ? "Maximization" : "Minimization")}");
            Console.WriteLine($"Optimal Objective Value: {FormatNumber(optimalSolution.ObjectiveValue)}");
            Console.WriteLine();

            Console.WriteLine("Decision Variables:");
            for (int i = 0; i < model.NumVars; i++)
            {
                string status = IsBasicVariable(i) ? "Basic" : "Non-Basic";
                Console.WriteLine($"  x{i + 1} = {FormatNumber(optimalSolution.VariableValues[i])} ({status})");
            }

            Console.WriteLine();
            Console.WriteLine("Constraints:");
            for (int i = 0; i < model.NumCons; i++)
            {
                double lhs = 0;
                var constraint = model.Constraints[i];
                for (int j = 0; j < model.NumVars; j++)
                {
                    lhs += constraint.Coefficients[j] * optimalSolution.VariableValues[j];
                }

                double slack = 0;
                switch (constraint.Relation)
                {
                    case "<=":
                        slack = constraint.RHS - lhs;
                        break;
                    case ">=":
                        slack = lhs - constraint.RHS;
                        break;
                }

                string status = Math.Abs(slack) < 1e-9 ? "Binding" : "Non-binding";
                Console.WriteLine($"  Constraint {i + 1}: LHS = {FormatNumber(lhs)}, RHS = {FormatNumber(constraint.RHS)}, Slack = {FormatNumber(slack)} ({status})");
            }
        }

        private void DisplayAnalysisResult()
        {
            // Results are already written to the reporter
            // Display the log to console
            string log = reporter.GetLog();

            // Display only the latest analysis (after the last separator)
            string[] sections = log.Split(new[] { "\n===" }, StringSplitOptions.None);
            if (sections.Length > 0)
            {
                string latestSection = sections[sections.Length - 1];
                if (!latestSection.StartsWith("==="))
                    latestSection = "===" + latestSection;
                Console.WriteLine(latestSection);
            }
        }

        private bool IsBasicVariable(int varIndex)
        {
            return Array.IndexOf(finalTableau.BasicVars, varIndex) != -1;
        }

        private int GetVariableIndex(string prompt)
        {
            int index;
            do
            {
                Console.Write(prompt);
                if (int.TryParse(Console.ReadLine(), out index) && index >= 1 && index <= model.NumVars)
                {
                    return index - 1; // Convert to 0-based index
                }
                Console.WriteLine("Invalid input. Please enter a valid variable index.");
            } while (true);
        }

        private int GetConstraintIndex(string prompt)
        {
            int index;
            do
            {
                Console.Write(prompt);
                if (int.TryParse(Console.ReadLine(), out index) && index >= 1 && index <= model.NumCons)
                {
                    return index - 1; // Convert to 0-based index
                }
                Console.WriteLine("Invalid input. Please enter a valid constraint index.");
            } while (true);
        }

        private double GetDoubleInput(string prompt)
        {
            double value;
            do
            {
                Console.Write(prompt);
                if (double.TryParse(Console.ReadLine(), out value))
                {
                    return value;
                }
                Console.WriteLine("Invalid input. Please enter a valid number.");
            } while (true);
        }

        private static string FormatNumber(double value)
        {
            if (Math.Abs(value) < 1e-9) return "0.000";
            if (double.IsInfinity(value)) return value > 0 ? "∞" : "-∞";
            return value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // File Reporter for output
    public class FileReporter : IReporter
    {
        private StreamWriter writer;
        private string filename;
        private StringBuilder log;

        public FileReporter(string filename)
        {
            this.filename = filename;
            this.writer = new StreamWriter(filename, false);
            this.log = new StringBuilder();
        }

        public void WriteLine(string message = "")
        {
            writer.WriteLine(message);
            writer.Flush();
            log.AppendLine(message);
        }

        public void WriteStep(string stepName, string details)
        {
            WriteLine($"[{stepName}] {details}");
        }

        public string GetLog()
        {
            return log.ToString();
        }

        public void Dispose()
        {
            writer?.Dispose();
        }
    }
}