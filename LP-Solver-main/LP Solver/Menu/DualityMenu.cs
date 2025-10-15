using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LP_Solver.Menu
{
    internal static class DualityMenu
    {
        public static void Run()
        {
            bool back = false;
            while (!back)
            {
                Console.Clear();
                Console.WriteLine("===== Duality Menu =====");
                Console.WriteLine("1. Apply Duality");
                Console.WriteLine("2. Solve Dual Model");
                Console.WriteLine("3. Verify Strong/Weak Duality");
                Console.WriteLine("4. Back to Sensitivity Menu");
                Console.Write("Enter choice: ");

                switch (Console.ReadLine())
                {
                    case "1":
                        Console.WriteLine("TODO: ApplyDuality");
                        Console.ReadLine();
                        break;
                    case "2":
                        Console.WriteLine("TODO: SolveDualModel");
                        Console.ReadLine();
                        break;
                    case "3":
                        Console.WriteLine("TODO: VerifyDuality");
                        Console.ReadLine();
                        break;
                    case "4":
                        back = true;
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Press Enter to try again...");
                        Console.ReadLine();
                        break;
                }
            }
        }
    }
}