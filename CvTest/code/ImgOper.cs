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
using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge;
using AForge.Math.Random;

namespace CvTest
{
    public class ImgOper
    {
        /// <summary>
        /// RGB位图转灰度图，位图像素是以从左到右、从下到上的顺序在内存中从低地址排列到高地址
        /// 但是C#的Bitmap对象好像把这顺序颠倒过来了，根据实际经验发现，在BitmapData中，位图数据以从左到
        /// 右、从上到下的顺序排列，和图像坐标的顺序一致
        /// </summary>
        /// <param name="bitmapSource">源RGB位图</param>
        /// <returns>转化后的灰度图</returns>
        public static Bitmap Grayscale(Bitmap bitmapSource)
        {
            Bitmap bitmapGrayscale = null;
            if (bitmapSource != null && (bitmapSource.PixelFormat == PixelFormat.Format24bppRgb || bitmapSource.PixelFormat ==
                PixelFormat.Format32bppRgb || bitmapSource.PixelFormat == PixelFormat.Format32bppArgb))
            {
                int width = bitmapSource.Width;
                int height = bitmapSource.Height;
                Rectangle rect = new Rectangle(0, 0, width, height);
                bitmapGrayscale = new Bitmap(width, height, PixelFormat.Format8bppIndexed);

                // 设置调色板
                ColorPalette palette = bitmapGrayscale.Palette;
                for (int i = 0; i < palette.Entries.Length; i++)
                {
                    palette.Entries[i] = Color.FromArgb(255, i, i, i);
                }
                bitmapGrayscale.Palette = palette;
                BitmapData dataSource = bitmapSource.LockBits(rect, ImageLockMode.ReadOnly, bitmapSource.PixelFormat);
                BitmapData dataGrayscale = bitmapGrayscale.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
                byte b, g, r;
                // Stride为位图中每一行以4字节对齐的行宽
                int strideSource = dataSource.Stride;
                int strideGrayscale = dataGrayscale.Stride;

                unsafe
                {
                    byte* ptrSource = (byte*)dataSource.Scan0.ToPointer();
                    byte* ptr1;
                    byte* ptrGrayscale = (byte*)dataGrayscale.Scan0.ToPointer();
                    byte* ptr2;
                    if (bitmapSource.PixelFormat == PixelFormat.Format24bppRgb)
                    {
                        for (int row = 0; row < height; row++)
                        {
                            ptr1 = ptrSource + strideSource * row;
                            ptr2 = ptrGrayscale + strideGrayscale * row;
                            for (int col = 0; col < width; col++)
                            {
                                b = *ptr1;
                                ptr1++;
                                g = *ptr1;
                                ptr1++;
                                r = *ptr1;
                                ptr1++;
                                *ptr2 = (byte)(0.114 * b + 0.587 * g + 0.299 * r);
                                ptr2++;
                            }
                        }
                    }
                    else //bitmapSource.PixelFormat == PixelFormat.Format32bppArgb || bitmapSource.PixelFormat == PixelFormat.Format32bppRgb
                    {
                        for (int row = 0; row < height; row++)
                        {
                            ptr1 = ptrSource + strideSource * row;
                            ptr2 = ptrGrayscale + strideGrayscale * row;
                            for (int col = 0; col < width; col++)
                            {
                                b = *ptr1;
                                ptr1++;
                                g = *ptr1;
                                ptr1++;
                                r = *ptr1;
                                ptr1+=2;
                                *ptr2 = (byte)(0.114 * b + 0.587 * g + 0.299 * r);
                                ptr2++;
                            }
                        }
                    }
                }

                bitmapSource.UnlockBits(dataSource);
                bitmapGrayscale.UnlockBits(dataGrayscale);
            }
            else if (bitmapSource != null && bitmapSource.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                bitmapGrayscale = bitmapSource;
            }

            return bitmapGrayscale;
        }

        /// <summary>
        /// 反转每一像素的灰度值
        /// </summary>
        /// <param name="bitmapSource">原灰度图</param>
        /// <returns></returns>
        public static void ReverseGray(Bitmap bitmapSource)
        {
            if (bitmapSource != null && bitmapSource.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                int width = bitmapSource.Width;
                int height = bitmapSource.Height;
                Rectangle rect = new Rectangle(0, 0, width, height);
                BitmapData dataSource = bitmapSource.LockBits(rect, ImageLockMode.ReadWrite, bitmapSource.PixelFormat);
                int strideSource = dataSource.Stride;
                unsafe
                {
                    byte* ptrSource = (byte*)dataSource.Scan0.ToPointer();
                    byte* ptr1;
                    for (int row = 0; row < height; row++)
                    {
                        ptr1 = ptrSource + strideSource * row;
                        for (int col = 0; col < width; col++)
                        {
                            if (row < height / 2)
                            {
                                *ptr1 = (byte)(255 - *ptr1);
                            }
                            ptr1++;
                        }
                    }
                    bitmapSource.UnlockBits(dataSource);
                }
            }
        }

        /// <summary>
        /// 位图缩放
        /// </summary>
        /// <param name="bmp">源位图</param>
        /// <param name="newW">新宽度</param>
        /// <param name="newH">新高度</param>
        /// <returns>返回变换后的位图</returns>
        public static Bitmap ResizeImage(Bitmap bmp, int newW, int newH)
        {
            // create filter
            ResizeBilinear filter = new ResizeBilinear(newW, newH);
            // apply the filter
            return filter.Apply(bmp);

            #region 旧代码
            //try
            //{
            //    Bitmap b = new Bitmap(newW, newH, pf);
            //    Graphics g = Graphics.FromImage(b);

            //    // 插值算法的质量
            //    g.InterpolationMode = InterpolationMode.HighQualityBilinear;

            //    g.DrawImage(bmp, new Rectangle(0, 0, newW, newH), new Rectangle(0, 0, bmp.Width, bmp.Height), GraphicsUnit.Pixel);
            //    g.Dispose();

            //    return b;
            //}
            //catch(Exception ex)
            //{
            //    throw new Exception(ex.Message);
            //}
            #endregion
        }

        /// <summary>
        /// 裁剪位图
        /// </summary>
        /// <param name="bmp">源位图</param>
        /// <param name="StartX">裁剪起始X坐标</param>
        /// <param name="StartY">裁剪起始Y坐标</param>
        /// <param name="iWidth">裁剪后的宽度</param>
        /// <param name="iHeight">裁剪后的高度</param>
        /// <returns>裁剪后的位图</returns>
        public static Bitmap CutImage(Bitmap bmp, int StartX, int StartY, int iWidth, int iHeight)
        {
            // create filter
            Crop filter = new Crop(new Rectangle(StartX, StartY, iWidth, iHeight));
            // apply the filter
            return filter.Apply(bmp);

            #region 旧代码
            //if (bmp == null)
            //{
            //    return null;
            //}

            //int w = bmp.Width;
            //int h = bmp.Height;

            //if (StartX >= w || StartY >= h)
            //{
            //    return null;
            //}

            //if (StartX + iWidth > w)
            //{
            //    iWidth = w - StartX;
            //}

            //if (StartY + iHeight > h)
            //{
            //    iHeight = h - StartY;
            //}

            //try
            //{
            //    Bitmap bmpOut = new Bitmap(iWidth, iHeight, pf);

            //    Graphics g = Graphics.FromImage(bmpOut);
            //    g.DrawImage(bmp, new Rectangle(0, 0, iWidth, iHeight), new Rectangle(StartX, StartY, iWidth, iHeight), GraphicsUnit.Pixel);
            //    g.Dispose();

            //    return bmpOut;
            //}
            //catch
            //{
            //    return null;
            //}
            #endregion
        }

        /// <summary>
        /// 旋转位图
        /// </summary>
        /// <param name="bmp">源位图</param>
        /// <param name="angle">旋转角度</param>
        /// <returns>返回旋转后的位图</returns>
        public static Bitmap RotateImage(Bitmap bmp, double angle)
        {
            // create filter - rotate for 30 degrees keeping original image size
            RotateBilinear filter = new RotateBilinear(angle, true);
            // apply the filter
            return filter.Apply(bmp);

        }

        /// <summary>
        /// 四边形变换
        /// </summary>
        /// <param name="bmp">源位图</param>
        /// <param name="lt">左上角点</param>
        /// <param name="rt">右上角点</param>
        /// <param name="rb">右下角点</param>
        /// <param name="lb">左下角点</param>
        /// <returns></returns>
        public static Bitmap QuadrilateralTransform(Bitmap bmp, IntPoint lt, IntPoint rt, IntPoint rb, IntPoint lb)
        {
            // define quadrilateral's corners
            List<IntPoint> corners = new List<IntPoint>();
            corners.Add(lt);
            corners.Add(rt);
            corners.Add(rb);
            corners.Add(lb);
            // create filter
            BackwardQuadrilateralTransformation filter =
                new BackwardQuadrilateralTransformation(bmp, corners);
            Bitmap dest = new Bitmap(bmp.Width, bmp.Height, bmp.PixelFormat);
            // apply the filter
            return filter.Apply(dest);
        }

        /// <summary>
        /// 增加椒盐噪点
        /// </summary>
        /// <param name="bmp">源位图</param>
        /// <param name="noiseamount">噪点量</param>
        public static void AddSaltNoise(Bitmap bmp, double noiseamount)
        {
            // create filter
            SaltAndPepperNoise filter = new SaltAndPepperNoise(noiseamount);
            // apply the filter
            filter.ApplyInPlace(bmp);
        }

        /// <summary>
        /// 增加噪点
        /// </summary>
        /// <param name="bmp">原位图</param>
        public static void AddictiveNoise(Bitmap bmp)
        {
            // create random generator
            IRandomNumberGenerator generator = new UniformGenerator(new Range(-50, 50));
            // create filter
            AdditiveNoise filter = new AdditiveNoise(generator);
            // apply the filter
            filter.ApplyInPlace(bmp);
        }

        /// <summary>
        /// 高斯卷积（模糊）
        /// </summary>
        /// <param name="bmp">原位图</param>
        ///// <param name="sigma">sigma</param>
        ///// <param name="lenght">卷积核边长（像素）</param>
        public static Bitmap GaussianConvolution(Bitmap bmpSource, double sigma, int length
            )
        {
            Bitmap bmp = null;
            double[] gaussian_coef = new double[length];

            for (int i = 0; i < length; i++)
            {
                gaussian_coef[i] = Math.Pow(Math.E, -((i - length / 2) * (i - length / 2)) / (2 * sigma*sigma)) / Math.Sqrt(2 * Math.PI) * sigma;
            }

            if (bmpSource == null)
            {
                return bmp;
            }

            if (bmpSource.PixelFormat == PixelFormat.Format24bppRgb || bmpSource.PixelFormat ==
               PixelFormat.Format32bppRgb || bmpSource.PixelFormat == PixelFormat.Format32bppArgb)
            {
                bmpSource = ImgOper.Grayscale(bmpSource);
            }

            if (bmpSource.PixelFormat != PixelFormat.Format8bppIndexed)
            {
                return bmp;
            }

            int width = bmpSource.Width;
            int height = bmpSource.Height;
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData dataSource = bmpSource.LockBits(rect, ImageLockMode.ReadOnly, bmpSource.PixelFormat);
            // Stride为位图中每一行以4字节对齐的行宽
            int strideSource = dataSource.Stride;
            double[,] bmpdata1 = new double[height, width];
            double[,] bmpdata2 = new double[height, width];
            unsafe
            {
                byte* ptrSource = (byte*)dataSource.Scan0.ToPointer();
                byte* ptr1;
                double d = 0;     // 临时计算某点上的卷积值
                byte minvalue = 255;
                byte maxvalue = 0;
                double minvalue_double = double.MaxValue;
                double maxvalue_double = 0;

                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        // 位图的在内存中的排列是从左到右，从下到上的，但BitmapData貌似做了优化，把位图数据在内存中的排列
                        // 顺序改为与坐标系一致，即从左到右、从上到下
                        ptr1 = ptrSource + row * strideSource + col;
                        
                        d = 0;
                        for (int i = 0; i < length; i++)
                        {
                            if (col + i - length / 2 >= 0 && col + i - length / 2 < width)
                            {
                                d += *(ptr1 + i - length / 2) * gaussian_coef[i];
                            }
                        }
                        bmpdata1[row, col] = d;

                        if (*ptr1 > maxvalue)
                        {
                            maxvalue = *ptr1;
                        }
                        if (*ptr1 < minvalue)
                        {
                            minvalue = *ptr1;
                        }
                    }
                }

                for (int col = 0; col < width; col++)
                {
                    for (int row = 0; row < height; row++)
                    {
                        d = 0;
                        for (int i = 0; i < length; i++)
                        {
                            if (row + i - length / 2 >= 0 && row + i - length / 2 < height)
                            {
                                d += bmpdata1[row + i - length / 2, col] * gaussian_coef[i];
                            }
                        }
                        bmpdata2[row, col] = d;
                        if (d > maxvalue_double)
                        {
                            maxvalue_double = d;
                        }
                        if (d < minvalue_double)
                        {
                            minvalue_double = d;
                        }
                    }
                }

                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        ptr1 = ptrSource + row * strideSource + col;
                        *ptr1 = (byte)(bmpdata2[row, col] * (maxvalue - minvalue) / (maxvalue_double - minvalue_double));
                    }
                }
                bmpSource.UnlockBits(dataSource);

                bmp = bmpSource;
            }
            return bmp;
        }

        /// <summary>
        /// 绘制Hog示意图
        /// </summary>
        /// <param name="hogGram">Hog图数据</param>
        /// <param name="width">示意图宽</param>
        /// <param name="height">示意图高</param>
        public static Bitmap DrawHogGram(HogGram hogGram, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            Graphics g = Graphics.FromImage(bmp);
            int grid_width = width / hogGram.HogSize.Width;        // 格子宽
            int grid_height = height / hogGram.HogSize.Height;     // 格子高
            // 格子中心相对坐标
            PointF rel_center = new PointF((float)(grid_width / 2), (float)(grid_height / 2));
            float radius = Math.Min((float)(grid_width / 2), (float)(grid_height / 2));

            double maxval = 0;
            double[] transval = new double[hogGram.HogCells.Length * hogGram.PartNumber];
            for (int i = 0; i < hogGram.HogCells.Length; i++)
            {
                for (int j = 0; j < hogGram.PartNumber; j++)
                {
                    transval[j + i * hogGram.PartNumber] = hogGram.HogCells[i].HistElements[j].rho;
                    if (transval[j + i * hogGram.PartNumber] > maxval)
                    {
                        maxval = transval[j + i * hogGram.PartNumber];
                    }
                }
            }

            for (int i = 0; i < transval.Length; i++)
            {
                if (maxval != 0)
                {
                    transval[i] = 255 * Math.Sqrt(transval[i]) / Math.Sqrt(maxval);
                }
            }

            HogHistElement he;
            PointF center;
            PointF right;
            PointF left;

            for (int row = 0; row < hogGram.HogSize.Height; row++)
            {
                for (int col = 0; col < hogGram.HogSize.Width; col++)
                {
                    for (int i = 0; i < hogGram.PartNumber; i++)
                    {
                        he = hogGram.HogCells[col + row * hogGram.HogSize.Width].HistElements[i];
                        center = new PointF(col * grid_width + grid_width / 2, row * grid_height + grid_height / 2);
                        right = new PointF((float)(center.X + radius * Math.Cos(he.theta)), (float)(center.Y + radius * Math.Sin(he.theta)));
                        left = new PointF((float)(center.X - radius * Math.Cos(he.theta)), (float)(center.Y - radius * Math.Sin(he.theta)));
                        int idx = i + (col + row * hogGram.HogSize.Width) * hogGram.PartNumber;
                        // hog示意图体现在某个方向灰度的亮度上，而不是在该方向上线段的长短上
                        Pen p = new Pen(Color.FromArgb((int)transval[idx], (int)transval[idx], (int)transval[idx]));
                        g.DrawLine(p, right, left);
                    }
                }
            }
            //Pen p = new Pen(Color.Red);
            //g.DrawLine(p, new PointF(20.5f, 20.5f), new PointF(100.4f, 100.4f));
            return bmp;
        }

        /// <summary>
        /// 生成灰度图的积分图,位图像素是以从左到右、从下到上的顺序在内存中从低地址排列到高地址
        /// 但是C#的Bitmap对象好像把这顺序颠倒过来了，根据实际经验发现，在BitmapData中，位图数据以从左到
        /// 右、从上到下的顺序排列，和图像坐标的顺序一致
        /// </summary>
        /// <param name="bmpSource">原始灰度图</param>
        /// <param name="type">积分类型，1为原值积分，2为原值平方积分</param>
        /// <returns>返回积分图</returns>
        public static int[,] Integrogram(Bitmap bmpSource, int type)
        {
            int[,] igram = null;

            if (bmpSource != null && bmpSource.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                int width = bmpSource.Width;
                int height = bmpSource.Height;
                Rectangle rect = new Rectangle(0, 0, width, height);
                BitmapData dataSource = bmpSource.LockBits(rect, ImageLockMode.ReadOnly, bmpSource.PixelFormat);
                // Stride为位图中每一行以4字节对齐的行宽
                int strideSource = dataSource.Stride;

                igram = new int[height, width];
                for (int j = 0; j < height; j++)
                    for (int i = 0; i < width; i++)
                        igram[j, i] = 0;

                unsafe
                {
                    byte* ptrSource = (byte*)dataSource.Scan0.ToPointer();
                    byte* ptr1;

                    int[] ss = new int[width];   // 每一列对应的当前跨行积分
                    for (int row = 0; row < height; row++)
                    {
                        for (int col = 0; col < width; col++)
                        {
                            // 位图的在内存中的排列是从左到右，从下到上的，但BitmapData貌似做了优化，把位图数据在内存中的排列
                            // 顺序改为与坐标系一致，即从左到右、从上到下
                            ptr1 = ptrSource + row * strideSource + col;

                            switch (type)
                            {
                                case 1:
                                    ss[col] += *ptr1;
                                    break;
                                case 2:
                                    ss[col] += (*ptr1) * (*ptr1);
                                    break;
                                default:
                                    ss[col] += *ptr1;
                                    break;
                            }
                            
                            if (col > 0)
                            {
                                igram[row, col] = igram[row, col - 1] + ss[col];
                            }
                            else
                            {
                                igram[row, col] = ss[col];
                            }
                        }
                    }
                }

                bmpSource.UnlockBits(dataSource);
            }
            return igram;
        }

        /// <summary>
        /// 两幅灰度图像素相乘的积分图
        /// </summary>
        /// <param name="bmpX">第一幅灰度图</param>
        /// <param name="bmpY">第二幅灰度图</param>
        /// <param name="type">积分类型，1为原值积分，2为原值平方积分</param>
        /// <returns>积分图</returns>
        public static int[,] Integrogram(Bitmap bmpX, Bitmap bmpY, int type)
        {
            int[,] igram = null;

            if (bmpX != null && bmpX.PixelFormat == PixelFormat.Format8bppIndexed && 
                bmpY != null && bmpY.PixelFormat == PixelFormat.Format8bppIndexed && 
                bmpX.Width == bmpY.Width && bmpX.Height == bmpY.Height)
            {
                int width = bmpX.Width;
                int height = bmpX.Height;
                Rectangle rect = new Rectangle(0, 0, width, height);
                BitmapData dataSource1 = bmpX.LockBits(rect, ImageLockMode.ReadOnly, bmpX.PixelFormat);
                BitmapData dataSource2 = bmpY.LockBits(rect, ImageLockMode.ReadOnly, bmpY.PixelFormat);
                // Stride为位图中每一行以4字节对齐的行宽
                int strideSource = dataSource1.Stride;

                igram = new int[height, width];
                for (int j = 0; j < height; j++)
                    for (int i = 0; i < width; i++)
                        igram[j, i] = 0;

                unsafe
                {
                    byte* ptrSource1 = (byte*)dataSource1.Scan0.ToPointer();
                    byte* ptrSource2 = (byte*)dataSource2.Scan0.ToPointer();
                    byte* ptr1;
                    byte* ptr2;

                    int[] ss = new int[width];   // 每一列对应的当前跨行积分
                    for (int row = 0; row < height; row++)
                    {
                        for (int col = 0; col < width; col++)
                        {
                            // 位图的在内存中的排列是从左到右，从下到上的，但BitmapData貌似做了优化，把位图数据在内存中的排列
                            // 顺序改为与坐标系一致，即从左到右、从上到下
                            ptr1 = ptrSource1 + row * strideSource + col;
                            ptr2 = ptrSource2 + row * strideSource + col;

                            switch (type)
                            {
                                case 1:
                                    ss[col] += (*ptr1)*(*ptr2);
                                    break;
                                case 2:
                                    ss[col] += (*ptr1) * (*ptr2) * (*ptr1) * (*ptr2);
                                    break;
                                default:
                                    ss[col] += (*ptr1) * (*ptr2);
                                    break;
                            }

                            if (col > 0)
                            {
                                igram[row, col] = igram[row, col - 1] + ss[col];
                            }
                            else
                            {
                                igram[row, col] = ss[col];
                            }
                        }
                    }
                }

                bmpX.UnlockBits(dataSource1);
                bmpY.UnlockBits(dataSource2);
            }
            return igram;
        }

        /// <summary>
        /// 获取图像灰度值数据
        /// </summary>
        /// <param name="bmpSource">原始灰度图</param>
        /// <returns>返回byte数组</returns>
        public static byte[] GetGraybmpData(Bitmap bmpSource)
        {
            if (bmpSource == null || bmpSource.PixelFormat != PixelFormat.Format8bppIndexed)
            {
                return null;
            }

            int width = bmpSource.Width;
            int height = bmpSource.Height;
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData dataSource = bmpSource.LockBits(rect, ImageLockMode.ReadOnly, bmpSource.PixelFormat);
            // Stride为位图中每一行以4字节对齐的行宽
            int strideSource = dataSource.Stride;
            byte[] bmpdata = new byte[height*width];
            unsafe
            {
                byte* ptrSource = (byte*)dataSource.Scan0.ToPointer();
                byte* ptr1;
                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        // 位图的在内存中的排列是从左到右，从下到上的，但BitmapData貌似做了优化，把位图数据在内存中的排列
                        // 顺序改为与坐标系一致，即从左到右、从上到下
                        ptr1 = ptrSource + row * strideSource + col;
                        bmpdata[col+row*width] = *ptr1;
                    }
                }
                bmpSource.UnlockBits(dataSource);
            }
            return bmpdata;
        }
    }
}
