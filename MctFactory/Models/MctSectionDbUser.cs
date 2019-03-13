using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public enum MctSectionDbUserTypeEnum
    {
        L = 0, C, H, T, B, P, DOUBLE_L, DOUBLE_C, SB, SR, OCT, SOCT, ROCT, TRK, STRK
    }
    public class MctSectionDbUser : MctSection
    {
        public string Data1 { get; set; }
        public string DbName { get; set; }
        public string SectionName { get; set; }
        public List<double> Dimensions { get; set; }
        public bool IsDb { get; set; }

        public MctSectionDbUser(MctSectionDbUserTypeEnum sectionType)
        {
            Type = "DBUSER";
            Shape = sectionType == MctSectionDbUserTypeEnum.DOUBLE_L ? "2L"
                : (sectionType == MctSectionDbUserTypeEnum.DOUBLE_C ? "2C" : sectionType.ToString());
        }

        public override string ToString()
        {
            if (Data1 == null)
            {
                if (IsDb)
                    Data1 = $"1,{DbName},{SectionName}";
                else
                {
                    if (Dimensions == null)
                        throw new ArgumentException("Dimensions must be provided when using User defined section");
                    Data1 = "2";
                    const int CAPACITY = 10;
                    for (int i = 0; i < CAPACITY; ++i)
                    {
                        if (i < Dimensions.Count)
                            Data1 += $",{Dimensions[i]}";
                        else
                            Data1 += ",";
                    }
                }
            }

            return $"{base.ToString()},{Data1}";
        }
    }
}
