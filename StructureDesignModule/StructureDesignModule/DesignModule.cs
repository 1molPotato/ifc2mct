using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace StructureDesignModule
{
    static class Constants
    {
        public const double TH_CA = 1.89e8;
        public const double TH_TA = 2.1e8;
        public const double PARAMETER_REDUCE = 0.8;
        public const double STEEL_INTENSITY = 78500;
        public const double spanOfBridge = 30.0;
        public const double widthOfLane = 3.75;
        public const double widthOfleftshouder = 2.5;
        public const double widthOfrightshouder = 2.5;
        public const double widthOfmedialIsland = 2;
        public const int laneNums = 4;
        public const double add = 0.5;
        public enum Vehicle_Load { lord_i, lord_ii };
    }

    public class Technical_Demand
    {
        const double TH_CA = 1.89e8;
        const double TH_TA = 2.1e8;
        const double PARAMETER_REDUCE = 0.8;
        const double STEEL_INTENSITY = 78500;
        const double spanOfBridge = 30.0;
        private double widthOfBridge { get; set; }
        private double widthOfLane { get; set; }
        private int laneNums { get; set; }
        private double widthOfleftshouder { get; set; }
        private double widthOfrightshouder { get; set; }
        private double widthOfmedialIsland { get; set; }
        private double add { get; set; }
        private enum Vehicle_Load { lord_i, lord_ii };

        public Technical_Demand()
        {
            widthOfLane = 3.75;
            laneNums = 4;
            widthOfleftshouder = 2.5;
            widthOfrightshouder = 2.5;
            widthOfmedialIsland = 2;
            add = 0.5;
        }
        /*
                      public double Get_SpanOfBridge()
                      {
                          Console.WriteLine("请输入计算跨径");
                          spanOfBridge = Convert.ToDouble(Console.ReadLine());
                          return spanOfBridge;
                      }

                      public double Get_WidthOfLane()
                      {
                          Console.WriteLine("请输入车道宽度");
                          widthOfLane= Convert.ToDouble(Console.ReadLine());
                          return widthOfLane;
                      }

                      public int Get_LaneNums()
                      {
                          Console.WriteLine("请输入车道数量");
                          laneNums = Convert.ToInt32(Console.ReadLine());
                          return laneNums;
                      }

                      public double Get_WidthOfrightshouder()
                      {
                          Console.WriteLine("请输入右侧硬路肩宽度");
                          widthOfrightshouder = Convert.ToDouble(Console.ReadLine());
                          return widthOfrightshouder;
                      }

                      public double Get_WidthOfleftshouder()
                      {
                          Console.WriteLine("请输入左侧硬路肩宽度");
                          widthOfleftshouder= Convert.ToDouble(Console.ReadLine());
                          return widthOfleftshouder;
                      }

                      public double Get_WidthOfmedialIsland()
                      {
                          Console.WriteLine("请输入中央分隔带宽度");
                          widthOfmedialIsland = Convert.ToDouble(Console.ReadLine());
                          return widthOfmedialIsland;
                      }
              */
        public double Get_WidthOfBridge()
        {
            widthOfBridge = Constants.widthOfleftshouder + Constants.widthOfrightshouder + Constants.widthOfmedialIsland + Constants.widthOfLane * Constants.laneNums * 2 + Constants.add * 2;
            /*           Console.WriteLine("the width of bridge is :");
                       Console.WriteLine(widthOfBridge);*/
            return widthOfBridge;
        }

        public void Write_Info2TXT()
        {
            StreamWriter sw = new StreamWriter("DesignInfo.txt");
            sw.WriteLine("Design Information is belowing:");
            string str = "";
            sw.WriteLine(str.PadRight(40, '-'));
            sw.WriteLine("The Span Of Bridge is :{0}m", Constants.spanOfBridge);
            sw.WriteLine("The Width Of Lanes is :{0}m", Constants.widthOfLane);
            sw.WriteLine("The Num Of the lane are :{0}", Constants.laneNums);
            sw.WriteLine("The Width Of LeftShouder is :{0}m", Constants.widthOfleftshouder);
            sw.WriteLine("The Width Of RightShouder is :{0}m", Constants.widthOfrightshouder);
            sw.WriteLine("The Width Of widthOfmedialIsland {0}m", Constants.widthOfmedialIsland);
            sw.WriteLine("The Vehicle Load is :{0}", Constants.Vehicle_Load.lord_i);
            sw.WriteLine("The Width Of Bridge is :{0}m", Get_WidthOfBridge());
            sw.WriteLine(str.PadRight(40, '-'));
            sw.Close();
        }
    }

    public class CrossSection
    {
        private int girder_nums { get; set; }
        private double width_flange { get; set; }
        private double girder_gap { get; set; }
        public double girder_web_height { get; set; }
        public double girder_web_thickness { get; set; }
        private double girder_upper_flange_width { get; set; }
        private double girder_upper_flange_thickness { get; set; }
        private double girder_lower_flange_width { get; set; }
        private double girder_lower_flange_thickness { get; set; }
        public double axis_moment_inertial_girder { get; set; }//转动刚度
        private double calculated_Moment { get; set; }//计算弯矩
        //新添加的成员属性，获取组合梁桥面板轮廓
        public double[] lateral_offset_dis { get; set; }
        public int vertical_offset_dis { get; set; }
        public void calculate_girder_parament(ref Technical_Demand t)
        {
            //计算girder_gap，假定width_flange长度在1.0-1.5之间
            //根据经验girder_gap一般在2.5m-3.5m之间 
            //计算主梁根数 
            double tmp_girder_nums = 0.5 * ((t.Get_WidthOfBridge() - 1.0 * 2) / 2.5 + (t.Get_WidthOfBridge() - 1.0 * 2) / 3.5);
            if (tmp_girder_nums - Math.Floor(tmp_girder_nums) > 0)
            {
                girder_nums = Convert.ToInt32(Math.Floor(tmp_girder_nums) + 2);
            }
            else
            {
                girder_nums = Convert.ToInt32(Math.Floor(tmp_girder_nums) + 1);
            }
            //计算主梁间距
            girder_gap = Math.Floor((t.Get_WidthOfBridge() - 1 * 2) / (girder_nums - 1) * 10) / 10.0;
            //计算挑臂长度 
            width_flange = 0.5 * (t.Get_WidthOfBridge() - girder_gap * (girder_nums - 1));
            //计算主梁高度 
            double tmp_web_height = 0.5 * Constants.spanOfBridge * (1.0 / 12.0 + 1.0 / 25.0);
            if (tmp_web_height * 10.0 > Math.Floor(tmp_web_height * 10))
            {
                //cout<<tmp_web_height<<endl;
                girder_web_height = Math.Floor(tmp_web_height * 10) / 10.0 + 0.1;
                //cout<<girder_web_height<<endl;
            }
            else
            {
                girder_web_height = tmp_web_height * 10 / 10.0;
            }
            //计算主梁腹板厚度，单位：m 
            double parameter_hw_tw = (Math.Floor(girder_web_height * 1000 / 310) + 1) / 1000.0;
            if (parameter_hw_tw <= 10)
            {
                girder_web_thickness = 0.01;
            }
            else
            {
                girder_web_thickness = 0.012;
            }
            //计算主梁翼缘板尺寸
            /*根据规范7.2.1：焊接板梁受压翼缘的伸出肢宽不宜大于40cm，也不应大于其厚度的12倍，
            受拉翼缘的伸出肢宽不应大于其厚度的16倍。翼缘板的面外惯矩宜满足下式要求：
            0<=Iyc/Iyt<=10; */
            //根据经验翼板宽度在300mm-650mm之间（跨径小于40m），考虑焊接影响，翼板b=0.3-0.45h<600mm
            //翼板厚度一般不超过32mm 
            //Q345最大弯拉设计应力为210MPa，最大弯压设计应力为189MPa
            double tmp_yc = Constants.TH_CA * 1.0 / (Constants.TH_CA + Constants.TH_TA) * girder_web_height;
            double tmp_yt = Constants.TH_TA * 1.0 / (Constants.TH_CA + Constants.TH_TA) * girder_web_height;
            calculated_Moment = 0.5 * Constants.spanOfBridge * Constants.spanOfBridge * Constants.STEEL_INTENSITY;
            double Area_c = calculated_Moment * 1.0 / (1.0 * Constants.PARAMETER_REDUCE * girder_web_height * Constants.TH_CA) - 1.0 * girder_web_height * girder_web_thickness / 6.0 * (2.0 * Constants.TH_CA - Constants.TH_TA) / Constants.TH_CA;
            double Area_t = calculated_Moment * 1.0 / (1.0 * Constants.PARAMETER_REDUCE * girder_web_height * Constants.TH_TA) - 1.0 * girder_web_height * girder_web_thickness / 6.0 * (2.0 * Constants.TH_TA - Constants.TH_CA) / Constants.TH_TA;
            //翼缘板宽度映射宽度在300-650mm之间
            //上翼缘板尺寸计算 
            if (Constants.spanOfBridge <= 30)
            {
                girder_upper_flange_width = 0.4;
            }
            else
            {
                int tmp_girder_upper_flange_width = Convert.ToInt32(Math.Floor(Constants.spanOfBridge * 10 + 100));
                if (tmp_girder_upper_flange_width % 5 > 0)
                {
                    girder_upper_flange_width = Math.Floor(tmp_girder_upper_flange_width / 5.0 + 1) * 5 / 1000.0;
                }
                else
                {
                    girder_upper_flange_width = tmp_girder_upper_flange_width / 1000.0;
                }
            }
            int tmp_girder_upper_flange_thickness = Convert.ToInt32(Math.Floor(girder_upper_flange_width / 2.0 * 1000.0 / 12.0));
            if (tmp_girder_upper_flange_thickness % 5 != 0)
            {
                girder_upper_flange_thickness = (tmp_girder_upper_flange_thickness / 5 + 1) * 5 / 1000.0;
            }
            else
            {
                girder_upper_flange_thickness = tmp_girder_upper_flange_thickness / 1000.0;
            }
            girder_upper_flange_thickness += 0.005;
            if (girder_upper_flange_width >= 0.6 || girder_upper_flange_width >= 24 * girder_upper_flange_thickness)
            {
                girder_upper_flange_width = Math.Min(0.6, 24 * girder_upper_flange_thickness);
            }

            //下翼缘板尺寸计算 
            if (Constants.spanOfBridge <= 40)
            {
                girder_lower_flange_width = 0.55;
            }
            else
            {
                int tmp_girder_lower_flange_width = Convert.ToInt32(Math.Floor(Constants.spanOfBridge * 10 * 4 / 3.0));
                if (tmp_girder_lower_flange_width % 5 > 0)
                {
                    girder_lower_flange_width = Math.Floor(tmp_girder_lower_flange_width / 5.0 + 1) * 5 / 1000.0;
                }
                else
                {
                    girder_lower_flange_width = tmp_girder_lower_flange_width / 1000.0;
                }
            }

            int tmp_girder_lower_flange_thickness = Convert.ToInt32(Math.Floor(girder_lower_flange_width / 2.0 * 1000.0 / 16.0));
            if (tmp_girder_lower_flange_thickness % 5 != 0)
            {
                girder_lower_flange_thickness = (tmp_girder_lower_flange_thickness / 5 + 1) * 5 / 1000.0;
            }
            else
            {
                girder_lower_flange_thickness = tmp_girder_lower_flange_thickness / 1000.0;
            }
            girder_lower_flange_thickness += 0.01;
            if (girder_lower_flange_width >= 0.6 || girder_lower_flange_width >= 32 * girder_lower_flange_thickness)
            {
                girder_lower_flange_width = Math.Min(0.6, 32 * girder_lower_flange_thickness);
            }
            //计算纵梁的惯性矩
            double Area = girder_upper_flange_thickness * girder_upper_flange_width +
                girder_lower_flange_width * girder_lower_flange_thickness +
                girder_web_thickness * girder_web_height;
            double yc = (1.0 / Area) * (0.5 * girder_upper_flange_width * girder_upper_flange_thickness * girder_upper_flange_thickness
                + girder_web_thickness * girder_web_height * (0.5 * girder_web_height + girder_upper_flange_thickness)
                + girder_lower_flange_width * girder_lower_flange_thickness * (0.5 * girder_lower_flange_thickness
                    + girder_upper_flange_thickness + girder_web_height));
            //只计算腹板的惯性矩
            axis_moment_inertial_girder = 1 / 3.0 * (girder_web_thickness * yc * yc * yc) + 1 / 3.0 * (girder_web_thickness * (girder_web_height - yc)
                * (girder_web_height - yc) * (girder_web_height - yc)) + girder_upper_flange_thickness * girder_upper_flange_width * yc * yc +
                girder_lower_flange_width * girder_lower_flange_thickness * (girder_web_height - yc) * (girder_web_height - yc);
            //计算各个结构定位线的偏移距离
            lateral_offset_dis = new double[girder_nums];
            //结构定位线数量为奇数的时候
            if (girder_nums % 2 == 1)
            {
                int tmp_num = -girder_nums / 2;
                for (int i = 0; i < lateral_offset_dis.Length; i++)
                {
                    lateral_offset_dis[i] = tmp_num * girder_gap;
                    tmp_num++;
                }
            }
            //为偶数的时候
            else
            {
                double tmp_off_para = -girder_nums / 2 + 0.5;
                for (int i = 0; i < lateral_offset_dis.Length; i++)
                {
                    lateral_offset_dis[i] = tmp_off_para * girder_gap;
                    tmp_off_para++;
                }
            }

            //计算垂直偏移距离
            //桥面板厚度t的计算采用经验公式t=k*(30b+110)单位是mm
            //k取1.2，铺装层厚度取70mm，加腋高度取80mm，斜率为1:3

            double th_plate = 1.2 * (30 * girder_gap + 110);
            vertical_offset_dis = Convert.ToInt32(Math.Ceiling(th_plate / 10.0) * 10);

        }
        //还需要获得各个结构定位线的参数，以及桥面板的铺装层厚度
        public void Write_Info2TXT()
        {
            StreamWriter sw = new StreamWriter("DesignInfo.txt", true);
            sw.WriteLine("CrossSection information is belowing:");
            string str = "";
            sw.WriteLine(str.PadRight(40, '-'));
            sw.WriteLine("the girder_nums is :{0}", girder_nums);
            sw.WriteLine("the width_flange is :{0}", width_flange);
            sw.WriteLine("the girder_gap is :{0}", girder_gap);
            sw.WriteLine("the girder_web_height is :{0}", girder_web_height);
            sw.WriteLine("the girder_web_thickness is :{0}", girder_web_thickness);
            sw.WriteLine("the girder_upper_flange_width is :{0}", girder_upper_flange_width);
            sw.WriteLine("he girder_upper_flange_thickness is :{0}", girder_upper_flange_thickness);
            sw.WriteLine("the girder_lower_flange_width is :{0}", girder_lower_flange_width);
            sw.WriteLine("the girder_lower_flange_thickness is :{0}", girder_lower_flange_thickness);
            sw.WriteLine("the bridge structure offset info is belowing:");
            for (int i = 0; i < lateral_offset_dis.Length; i++)
            {
                sw.WriteLine("girder{0}:{1} {2}", i + 1, lateral_offset_dis[i], vertical_offset_dis);
            }
            sw.WriteLine(str.PadRight(40, '-'));
            sw.Close();
        }
    }
    public class longitudinal_lateral_connection
    {
        private double lateral_web_height { get; set; }
        private double lateral_flange_thickness { get; set; }
        private double lateral_flange_width { get; set; }
        private double lateral_web_thickness { get; set; }
        private double axis_moment_inertial_lateral_beam { get; set; }
        private double lateral_beam_gap { get; set; }
        private double stiffener_Z { get; set; }
        private int lateral_end_beam_nums { get; set; }
        private int intermediate_beam_nums { get; set; }

        public void get_parameters(ref Technical_Demand t1, ref longitudinal_lateral_connection t2, ref CrossSection t3)
        {
            //计算横梁的数量
            t2.lateral_end_beam_nums = 2;
            if (Constants.spanOfBridge == 20)
            {
                t2.intermediate_beam_nums = 3;
            }
            if (Constants.spanOfBridge > 20 && Constants.spanOfBridge <= 30)
            {
                t2.intermediate_beam_nums = 5;
            }
            if (Constants.spanOfBridge > 30)
            {
                t2.intermediate_beam_nums = 6;
            }
            double lateral_beam_gap = Constants.spanOfBridge * 1.0 / (t2.intermediate_beam_nums + 1);
            t2.lateral_beam_gap = lateral_beam_gap;
            //这里可以供用户选择是实腹式还是桁架式的横梁

            //实腹式横梁的计算(跨间)
            double tmp_height = t3.girder_web_height - 0.3;
            if (tmp_height <= t3.girder_web_height * 0.5)
            {
                lateral_web_height = 0.5 * t3.girder_web_height;
            }
            else
            {
                lateral_web_height = tmp_height;
            }

            stiffener_Z = 0;
            while (stiffener_Z == 0)
            {
                double tmp = 0.0;
                //试算Z的值首先令腹板宽度为300mm厚度取纵梁腹板厚度
                lateral_flange_width = 0.3 + tmp;
                lateral_flange_thickness = t3.girder_web_thickness;
                lateral_web_thickness = t3.girder_web_thickness;
                axis_moment_inertial_lateral_beam = 1 / 12.0 * (lateral_flange_width * (lateral_web_height + 2.0 *
                    lateral_flange_thickness) * (lateral_web_height + 2.0 * lateral_flange_thickness)
                    * (lateral_web_height + 2.0 * lateral_flange_thickness)) - 1 / 12.0 * ((lateral_flange_width -
                        lateral_web_thickness) * lateral_web_height * lateral_web_height * lateral_web_height);

                double equal_inertial = 0.0;

                if (t2.intermediate_beam_nums == 1 || t2.intermediate_beam_nums == 2)
                {
                    equal_inertial = 1.0 * axis_moment_inertial_lateral_beam;
                }
                if (t2.intermediate_beam_nums == 3 || t2.intermediate_beam_nums == 4)
                {
                    equal_inertial = 1.6 * axis_moment_inertial_lateral_beam;
                }
                if (t2.intermediate_beam_nums == 5 || t2.intermediate_beam_nums == 6)
                {
                    equal_inertial = 2.6 * axis_moment_inertial_lateral_beam;
                }
                stiffener_Z = (Constants.spanOfBridge * Constants.spanOfBridge * Constants.spanOfBridge) * equal_inertial / (t3.axis_moment_inertial_girder * (2.0 * t2.lateral_beam_gap)
                    * (2.0 * t2.lateral_beam_gap) * (2.0 * t2.lateral_beam_gap));

                //if (stiffener_Z > 10)
                //{
                //    cout << "Z=" << stiffener_Z << ">10" << endl;
                //    cout << "横梁格子刚度满足要求，横向传力均匀" << endl;
                //}
                if (stiffener_Z <= 10)
                {
                    stiffener_Z = 0;
                    tmp += 0.05;
                }
            }
        }
        public void Write_Info2TXT()
        {
            StreamWriter sw = new StreamWriter("DesignInfo.txt", true);
            string str = "";
            sw.WriteLine(str.PadRight(40, '-'));
            sw.WriteLine("The longitudinal design info is belowing:");
            sw.WriteLine("the axis moment inertial is :{0}", axis_moment_inertial_lateral_beam);
            if (stiffener_Z > 10)
            {
                sw.WriteLine("Z={0}>10", stiffener_Z);
                sw.WriteLine("横梁格子刚度满足要求，横向传力均匀");
            }
            else
            {
                sw.WriteLine("Z={0}<=10", stiffener_Z);
                sw.WriteLine("横梁格子刚度满足要求，横向传力不均匀，需要重新修改");
            }
            sw.WriteLine(str.PadRight(40, '-'));
            sw.Close();
        }
    }
}
