using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LP_Solver.Menu
{
    internal static class SensitivityAnalysisMenu
    {
        public static void Run()
        {
            bool back = false;
            while (!back)
            {
                Console.Clear();
                Console.WriteLine("===== Sensitivity Analysis Menu =====");
                Console.WriteLine("1. Range of Non-Basic Variable");
                Console.WriteLine("2. Change Non-Basic Variable");
                Console.WriteLine("3. Range of Basic Variable");
                Console.WriteLine("4. Change Basic Variable");
                Console.WriteLine("5. Range of Constraint RHS");
                Console.WriteLine("6. Change Constraint RHS");
                Console.WriteLine("7. Add New Activity");
                Console.WriteLine("8. Add New Constraint");
                Console.WriteLine("9. Display Shadow Prices");
                Console.WriteLine("10. Duality Options");
                Console.WriteLine("11. Back to Main Menu");
                Console.Write("Enter choice: ");

                switch (Console.ReadLine())
                {
                    case "1":
                        Console.WriteLine("TODO: RangeNonBasicVariable");
                        Console.ReadLine();
                        break;
                    case "2":
                        Console.WriteLine("TODO: ChangeNonBasicVariable");
                        Console.ReadLine();
                        break;
                    case "3":
                        Console.WriteLine("TODO: RangeBasicVariable");
                        Console.ReadLine();
                        break;
                    case "4":
                        Console.WriteLine("TODO: ChangeBasicVariable");
                        Console.ReadLine();
                        break;
                    case "5":
                        Console.WriteLine("TODO: RangeConstraintRHS");
                        Console.ReadLine();
                        break;
                    case "6":
                        Console.WriteLine("TODO: ChangeConstraintRHS");
                        Console.ReadLine();
                        break;
                    case "7":
                        Console.WriteLine("TODO: AddNewActivity");
                        Console.ReadLine();
                        break;
                    case "8":
                        Console.WriteLine("TODO: AddNewConstraint");
                        Console.ReadLine();
                        break;
                    case "9":
                        Console.WriteLine("TODO: DisplayShadowPrices");
                        Console.ReadLine();
                        break;
                    case "10":
                        DualityMenu.Run();
                        break;
                    case "11":
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