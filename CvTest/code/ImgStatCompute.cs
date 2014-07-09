using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;

namespace CvTest
{
    /// <summary>
    /// 关于图像的一些统计学上的计算
    /// </summary>
    public class ImgStatCompute
    {
        /// <summary>
        /// 求区域像素的数学期望
        /// </summary>
        /// <param name="igram">积分图</param>
        /// <param name="rect">图像区域</param>
        /// <returns>数学期望值</returns>
        public static double ComputeExpectation(int[,] igram, Rectangle rect)
        {
            System.Drawing.Point lu = new System.Drawing.Point((int)rect.X, (int)rect.Y);
            System.Drawing.Point ru = new System.Drawing.Point((int)(rect.X + rect.Width - 1), (int)rect.Y);
            System.Drawing.Point ld = new System.Drawing.Point((int)rect.X, (int)(rect.Y + rect.Height - 1));
            System.Drawing.Point rd = new System.Drawing.Point((int)(rect.X + rect.Width - 1), (int)(rect.Y + rect.Height - 1));

            double e = (igram[rd.Y, rd.X] + igram[lu.Y, lu.X] - igram[ru.Y, ru.X] - igram[ld.Y, ld.X]) / (rect.Width * rect.Height);
            return e;
        }

        /// <summary>
        /// 求区域像素方差
        /// </summary>
        /// <param name="igram1">标准值积分图</param>
        /// <param name="igram2">平方值积分图</param>
        /// <param name="rect">图像区域</param>
        /// <returns>方差值</returns>
        public static double ComputeVariance(int[,] igram1, int[,] igram2, Rectangle rect)
        {
            System.Drawing.Point lu = new System.Drawing.Point((int)rect.X, (int)rect.Y);
            System.Drawing.Point ru = new System.Drawing.Point((int)(rect.X + rect.Width - 1), (int)rect.Y);
            System.Drawing.Point ld = new System.Drawing.Point((int)rect.X, (int)(rect.Y + rect.Height - 1));
            System.Drawing.Point rd = new System.Drawing.Point((int)(rect.X + rect.Width - 1), (int)(rect.Y + rect.Height - 1));

            double e1 = (igram1[rd.Y, rd.X] + igram1[lu.Y, lu.X] - igram1[ru.Y, ru.X] - igram1[ld.Y, ld.X]) / (rect.Width * rect.Height);
            double e2 = igram2[rd.Y, rd.X] + igram2[lu.Y, lu.X] - igram2[ru.Y, ru.X] - igram2[ld.Y, ld.X] / (rect.Width * rect.Height);

            return e2 - e1 * e1;
        }

        /// <summary>
        /// 方差集合排序
        /// </summary>
        /// <param name="varCollection">方差集合</param>
        /// <returns>返回排序后的方差集合</returns>
        public static ArrayList SortVarianceCollection(ArrayList varCollection)
        {
            if (varCollection == null || varCollection.Count == 0)
            {
                return new ArrayList();
            }

            int min;
            for (int i = 0; i < varCollection.Count - 1; i++)
            {
                min = i;
                for (int j = i + 1; j < varCollection.Count; j++)
                {
                    if (((RectVariance)varCollection[j]).Variance < ((RectVariance)varCollection[min]).Variance)
                    {
                        min = j;
                    }
                }
                RectVariance t = (RectVariance)varCollection[min];
                varCollection[min] = varCollection[i];
                varCollection[i] = t;
            }
            return varCollection;
        }

        /// <summary>
        /// 给出积分图，计算两幅图像的相关系数
        /// </summary>
        /// <param name="igramX1">第一幅图像的标准积分图</param>
        /// <param name="igramX2">第一幅图像的平方积分图</param>
        /// <param name="igramY1">第二幅图像的标准积分图</param>
        /// <param name="igramY2">第二幅图像的平方积分图</param>
        /// <param name="igramXY1">两幅图像相乘积分图</param>
        /// <returns>返回相关系数</returns>
        public static double ComputeAssociationCoef(int[,] igramX1, int[,] igramX2, int[,] igramY1, int[,] igramY2, int[,] igramXY1, Rectangle rect)
        {
            double e_xy = ComputeExpectation(igramXY1, rect);
            double e_x = ComputeExpectation(igramX1, rect);
            double e_y = ComputeExpectation(igramY1, rect);
            double d_x = ComputeVariance(igramX1, igramX2, rect);
            double d_y = ComputeVariance(igramY1, igramY2, rect);

            double coef = 0;
            double devprod = Math.Sqrt(d_x) * Math.Sqrt(d_y);
            if (devprod != 0)
            {
                coef = Math.Abs((e_xy - e_x * e_y) / devprod);
            }
            return coef;
        }

        /// <summary>
        /// 直接计算两幅灰度图的相关系数
        /// </summary>
        /// <param name="bmpX"></param>
        /// <param name="bmpY"></param>
        /// <returns></returns>
        public static double ComputeAssociationCoef(Bitmap bmpX, Bitmap bmpY)
        {
            int[,] igramX1 = null;   //第一幅图像的标准积分图
            int[,] igramX2 = null;   //第一幅图像的平方积分图
            int[,] igramY1 = null;   //第二幅图像的标准积分图
            int[,] igramY2 = null;   //第二幅图像的平方积分图
            int[,] igramXY1 = null; //两幅图像相乘积分图
            if (bmpX != null && bmpX.PixelFormat == PixelFormat.Format8bppIndexed &&
                bmpY != null && bmpY.PixelFormat == PixelFormat.Format8bppIndexed &&
                bmpX.Width == bmpY.Width && bmpX.Height == bmpY.Height)
            {
                Rectangle rect = new Rectangle(0,0,bmpX.Width, bmpY.Height);
                igramX1 = ImgOper.Integrogram(bmpX, 1);
                igramX2 = ImgOper.Integrogram(bmpX, 2);
                igramY1 = ImgOper.Integrogram(bmpY, 1);
                igramY2 = ImgOper.Integrogram(bmpY, 2);
                igramXY1 = ImgOper.Integrogram(bmpX, bmpY, 1);
                return ComputeAssociationCoef(igramX1, igramX2, igramY1, igramY2, igramXY1, rect);
            }
            return 0;
        }

        /// <summary>
        /// 计算两个向量的欧氏距离
        /// </summary>
        /// <returns></returns>
        public static double ComputeDistance(double[] vect1, double[] vect2)
        {
            double distance = 0;    // 返回值
            if (vect1 != null && vect2 != null && vect1.Length == vect2.Length)
            {
                for (int i = 0; i < vect1.Length; i++)
                {
                    // 用绝对值的和代替平方和再开方，加快速度
                    distance += Math.Abs(vect1[i] - vect2[i]);
                }
            }
            return distance;
        }
    }
}
