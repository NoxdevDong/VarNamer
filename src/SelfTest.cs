using System;
using System.Text;

namespace VarNamer
{
    internal class SelfTest
    {
        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch (Exception) { }
            return EngineTest.Run(null);
        }
    }
}
