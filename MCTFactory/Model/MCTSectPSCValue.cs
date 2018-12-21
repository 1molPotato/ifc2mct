using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MCTFactory.Model
{
    public class MCTSectPSCValue : MCTSection
    {
        protected readonly string _bBU = "NO";
        protected readonly string _bEQ = "YES";
        protected readonly string _items = "AREA,Iyy,Izz,Cyp,Cym,Czp,Czm";

        protected readonly List<double> _points;

        // Calculated properties are stored here temporarily
        public List<double> SectionProperties { get; }
        // STIFF1 contains: AREA, ASy, ASz, Ixx, Iyy, Izz
        // STIFF2 contains: Cyp, Cym, Czp, Czm, QyB, QzB, PERI_OUT, PERI_IN, Cy, Cz
        public Dictionary<string, double> STIFF { get; }

        public MCTSectPSCValue(int id, string name, List<double> points) : base(id, name)
        {
            SHAPE = "GEN";
            SectionProperties = new List<double>();
            STIFF = new Dictionary<string, double>();
            _points = points;
            InitializeSectProperties();
        }

        private void InitializeSectProperties()
        {
            string strPoints = "";
            int n = _points.Count;
            if (n % 2 != 0) throw new ArgumentException("Input points are invalid.");
            for (int i = 0; i < n; i++)
                strPoints += i == n - 1 ? _points[i].ToString() : (_points[i].ToString() + ",");
            //var watch = new Stopwatch();
            //watch.Start();

            //SectPropCalculator.CalculateProperties(strPoints, _items, SectionProperties);
            //watch.Stop();
            //Console.WriteLine(watch.ElapsedMilliseconds.ToString());
            SetProperties();
        }

        // This function looks stupid.
        private void SetProperties()
        {
            //Assert.NotEmpty(SectionProperties);
            int count = 0;
            foreach (var str in _items.Split(','))
                STIFF[str] = SectionProperties[count++];
            //STIFF["Cy"] = STIFF["Cym"];
            //STIFF["Cz"] = STIFF["Czm"];
        }

        public override string ToString()
        {
            string ret = "SECT=";
            ret += string.Format($"{_id},VALUE,{_SNAME},{_OFFSET},{_bSD},{_bWE},{SHAPE},{_bBU},{_bEQ}\n");
            ret += string.Format($"{STIFF["AREA"]},0,0,0,{STIFF["Iyy"]},{STIFF["Izz"]}\n");
            ret += string.Format($"{STIFF["Cyp"]},{STIFF["Cym"]},{STIFF["Czp"]},{STIFF["Czm"]},0,0,0,0,0,0\n");
            ret += "0,0,0,0,0,0,0,0\n";
            ret += string.Format($"OPOLY=");
            int len = _points.Count;
            for (int i = 0; i < len; i++)
                ret += i == len - 1 ? _points[i].ToString() : (_points[i].ToString() + ",");
            return ret;
        }
    }
}
