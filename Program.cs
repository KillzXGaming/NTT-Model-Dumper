using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTLibrary
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Input .model required!");
                Console.Read();
                return;
            }

            if (!args[0].ToLower().EndsWith(".model"))
            {
                Console.WriteLine($"Input file must be a .model.");
                return;
            }

            Model model = new Model(args[0]);
        }
    }
}
