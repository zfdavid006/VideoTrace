using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace CvTest
{
    /// <summary>
    /// 设置一些常量参数
    /// </summary>
    public class Parameter
    {
        #region 一些参数常量的定义
        /// <summary>
        /// ！！！！！注意：BMPLIMITSIZE和DETECT_WINDOW_SIZE有非常大的关联，两者之间协调好才能有较准确的识别！！！
        /// 经过试验，目前的尺寸可以说是到现在为止是最好的
        /// </summary>
        public static Size BMPLIMITSIZE = new System.Drawing.Size(240, 180);
        public static Size DETECT_WINDOW_SIZE = new System.Drawing.Size(24, 24);     // 检测窗口尺寸（以像素为单位）
        

        public const double POS_DIST_COEF = 1.3;  // 正距离系数，在检测时让目标偏向与正样本，消除决策边界上的目标,一般取1.2到1.3之间
        public const double SCALE_COEF = 1.3;       // 图像收缩系数

        public const double AREA_INTERSECT_PROPORTION = 0.2;    // 矩形面积交集占小的那个区域的比例

        public const int POS_LIMITED_NUMBER = 25;     // 正样本数量上限
        public const int NEG_LIMITED_NUMBER = 50;     // 负样本数量上限

        #region 这些个基本上不变
        public static Size CELL_SIZE = new System.Drawing.Size(6, 6);        // 单元格尺寸（以像素为单位）
        public static Size BLOCK_SIZE = new System.Drawing.Size(2, 2);      // 块尺寸
        public const int PART_NUMBER = 9;                // HOG单元投票维数
        public const double GAUSSIAN_SIGMA = 0.6;    // 高斯模糊sigma值
        public const int GAUSSIAN_SIZE = 5;               // 高斯卷积核尺寸，尺寸为6×SIGMA＋1
        public const double MEDIAN_COEF = 0.5;         // 正负样本距离的中值系数：POSITIVE/(POSITIVE+NEGATIVE)
        #endregion

        #region 训练正样本时用到的一些间隔参数，基本也不太会变
        public const double ANGLE_BORDER = 4;       // 旋转角度变化边界，如[-5, 5)
        public const double ANGLE_INTERVAL = 2;     // 旋转角度变化间隔，如每次增加1度
        public const double SCALE_BORDER = 0.1;      // 缩放比例边界，如[-10%, 10%)
        public const double SCALE_INTERVAL = 0.1;    // 缩放比例变化间隔，如每次增加10%
        public const double SHIFT_BORDER = 0.05;      // 平移比例边界，如[-10%, 10%)
        public const double SHIFT_INTERVAL = 0.05;    // 平移比例变化间隔，如每次增加10%
        #endregion

        #region 关于金字塔的一些固定参数
        public const int INITIAL_POINTS_NUMBER = 50;
        #endregion

        #endregion
    }
}
