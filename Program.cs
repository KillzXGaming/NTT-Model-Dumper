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
            Model model = new Model(args[0]);
        }
    }
}
