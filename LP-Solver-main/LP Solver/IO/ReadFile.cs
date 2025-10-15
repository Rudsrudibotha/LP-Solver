using LP_Solver.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LP_Solver.IO
{
    public static class ReadFile
    {
        public static LpModel ReadFromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Input file not found.");

            var lines = File.ReadAllLines(path)
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToArray();

            if (lines.Length < 2)
                throw new Exception("Input file must contain at least one objective line and one constraint.");

            var model = new LpModel();

            // ---------------------
            // Parse first line: problem type + objective coefficients
            // ---------------------
            var firstParts = lines[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            model.ProblemType = firstParts[0].ToLower();
            for (int i = 1; i < firstParts.Length; i++)
            {
                string part = firstParts[i];
                if (part.StartsWith("+") || part.StartsWith("-"))
                    model.ObjectiveCoeffs.Add(double.Parse(part, CultureInfo.InvariantCulture));
                else
                    throw new Exception("Invalid objective coefficient: " + part);
            }

            // ---------------------
            // Last line: variable types
            // ---------------------
            var varTypeLine = lines[lines.Length - 1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            model.VariableTypes.AddRange(varTypeLine);

            // ---------------------
            // Constraints: all lines in between
            // ---------------------
            for (int i = 1; i < lines.Length - 1; i++)
            {
                var parts = lines[i].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != model.ObjectiveCoeffs.Count + 2)
                    throw new Exception($"Constraint line {i + 1} does not match number of variables.");

                var c = new Constraint();
                // coefficients
                for (int j = 0; j < model.ObjectiveCoeffs.Count; j++)
                    c.Coefficients.Add(double.Parse(parts[j], CultureInfo.InvariantCulture));

                c.Relation = parts[model.ObjectiveCoeffs.Count]; // <=, >=, =
                c.RHS = double.Parse(parts[model.ObjectiveCoeffs.Count + 1], CultureInfo.InvariantCulture);

                model.Constraints.Add(c);
            }

            return model;
        }
    }
}