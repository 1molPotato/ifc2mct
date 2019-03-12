using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.Translator
{
    class Program
    {
        
        static void Main(string[] args)
        {
            const string PATH = "../../../Tests/TestFiles/sectioned-spine.ifc";
            var worker = new Worker(PATH);
        }
    }
}
