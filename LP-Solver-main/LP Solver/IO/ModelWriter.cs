using System;
using System.IO;
using System.Text;

namespace LP_Solver.IO
{
    public static class ModelWriter
    {
        public static void WriteLogFile(string path, string title, string content)
        {
            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                sw.WriteLine("=== " + title + " ===");
                sw.WriteLine(content);
            }
        }
    }
}