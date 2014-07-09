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
    public class Fern
    {
        /// <summary>
        /// 初始化，指定Fern的编码长度
        /// </summary>
        /// <param name="featuresize">每个组特征的长度</param>
        public Fern(int featuresize)
        {
            CodeSize = featuresize;
            UInt32 cap = (UInt32)Math.Pow(2, CodeSize);
            Positive = new int[cap];
            Negative = new int[cap];
            Probability = new double[cap];
            for (uint i = 0; i < cap; i++)
            {
                Positive[i] = 0;
                Negative[i] = 0;
                Probability[i] = 0;
            }
        }

        /// <summary>
        /// 训练Fern特征
        /// </summary>
        /// <param name="code">特征编码</param>
        /// <param name="codetype">特征类型，1正，－1负</param>
        public void TrainFeature(uint code, int codetype)
        {
            if (CodeSize == 0)
            {
                return;
            }
            uint mask = 0;
            for (int i = 0; i < CodeSize; i++)
            {
                mask <<= 1;
                mask ++;
            }

            uint ncode = (code & mask);
            if (codetype == 1)
            {
                Positive[ncode]++;
            }
            else if (codetype == -1)
            {
                Negative[ncode]++;
            }
            if (Positive[ncode] == 0 && Negative[ncode] == 0)
            {
                Probability[ncode] = 0;
            }
            else
            {
                // probability = #p/(#p+#n)
                Probability[ncode] = Positive[ncode] / (Positive[ncode] + Negative[ncode]);
            } 
        }

        /// <summary>
        /// 正样例数
        /// </summary>
        public int[] Positive;
        /// <summary>
        /// 负样例数
        /// </summary>
        public int[] Negative;
        /// <summary>
        /// 概率
        /// </summary>
        public double[] Probability;
        /// <summary>
        /// 特征编码的长度，是组内特征数的2倍
        /// 2^CodeSize表示编码的所有可能组合
        /// </summary>
        public int CodeSize;
    }

    /// <summary>
    /// 定义特征的属性
    /// </summary>
    public class SampleFeature
    {
        /// <summary>
        /// 取得一个样本的特征
        /// </summary>
        /// <param name="bmpSource">源图像，需24位或32位真彩位图</param>
        /// <param name="template">基础分类器模板</param>
        /// <param name="sampletype">样本类型，1表示正样本，－1表示负样本</param>
        /// <returns>返回一个样本的特征</returns>
        public static SampleFeature GetSampleFeature(Bitmap bmpSource, BaseClassifierTemplate template, int sampletype)
        {
            SampleFeature sf = null;
            Bitmap bmp = bmpSource;

            if (bmp != null && (bmp.PixelFormat == PixelFormat.Format24bppRgb || bmp.PixelFormat ==
               PixelFormat.Format32bppRgb || bmp.PixelFormat == PixelFormat.Format32bppArgb))
            {
                // 将图像调整至模板大小
                if (bmp.Width != template.BmpWidth || bmp.Height != template.BmpHeight)
                {
                    bmp = ImgOper.ResizeImage(bmpSource, template.BmpWidth, template.BmpHeight);
                }

                bmp = ImgOper.Grayscale(bmpSource);

                int[,] igram = ImgOper.Integrogram(bmp, 1);

                sf = new SampleFeature();
                sf.GroupNum = template.GroupNum;
                sf.FeatureNum = template.FeatureNum;
                sf.SampleType = sampletype;
                sf.FeatureValue = BaseClassifierTemplate.GetFeatureCodeGroup(igram, bmp.Width, bmp.Height, template, 0, 0);
            }
            return sf;
        }

        /// <summary>
        /// 样本类型，1表示正样本，-1表示负样本
        /// </summary>
        public int SampleType;
        /// <summary>
        /// 一个样本中组的数量
        /// </summary>
        public int GroupNum;
        /// <summary>
        /// 一个组中特征的个数
        /// </summary>
        public int FeatureNum;
        /// <summary>
        /// 一个样本中所有组的特征值，每个组的特征存储在一个UInt32中
        /// 每个特征2bit，意味着每个组的特征数不能超过16个
        /// </summary>
        public UInt32[] FeatureValue;
    }

    /// <summary>
    /// 基本分类器的分组模板
    /// </summary>
    public class BaseClassifierTemplate
    {
        private  Size pri_horizontal_size = new Size(3, 2);
        private  Size pri_vertical_size = new Size(2, 3);
        /// <summary>
        /// 基础分类机模板初始化
        /// </summary>
        /// <param name="bmpwidth">检测窗口宽</param>
        /// <param name="bmpheight">检测窗口高</param>
        /// <param name="featurenum">每组特征数</param>
        public BaseClassifierTemplate(int bmpwidth, int bmpheight, int featurenum)
        {
            Random ra = null;
            BmpWidth = bmpwidth;
            BmpHeight = bmpheight;
            FeatureNum = featurenum;

            GroupNum = 0;  // 组数先设为0；

            ArrayList featureCollection = new ArrayList();
            // 横向特征
            for (int h = 0; h < bmpheight - pri_horizontal_size.Height + 1; h++)
            {
                for (int w = 0; w < bmpwidth - pri_horizontal_size.Width + 1; w ++)
                {
                    featureCollection.Add(new Rectangle(w, h, pri_horizontal_size.Width, pri_horizontal_size.Height));
                }
            }
            // 纵向特征
            for (int w = 0; w < bmpwidth - pri_vertical_size.Width + 1; w++)
            {
                for (int h = 0; h < bmpheight - pri_vertical_size.Height + 1; h++)
                {
                    featureCollection.Add(new Rectangle(w, h, pri_vertical_size.Width, pri_vertical_size.Height));
                }
            }

            GroupNum = featureCollection.Count / FeatureNum;

            BitFeatures = new Rectangle[GroupNum, FeatureNum];

            for (int i = 0; i < GroupNum; i++)
            {
                for (int j = 0; j < FeatureNum; j++)
                {
                    BitFeatures[i, j] = new Rectangle();
                    ra = new Random();
                    ra = new Random(ra.Next() * unchecked((int)DateTime.Now.Ticks) * (j+i*FeatureNum));
                    int idx = ra.Next(featureCollection.Count);
                    Rectangle r = (Rectangle)featureCollection[idx];

                    BitFeatures[i, j].X = r.X;
                    BitFeatures[i, j].Y = r.Y;
                    BitFeatures[i, j].Width = r.Width;
                    BitFeatures[i, j].Height = r.Height;
                    featureCollection.RemoveAt(idx);   // 这种写法是错误的，会引起很大问题
                }
            }
        }

        /// <summary>
        /// 获取一个检测窗口对应的图像中所有特征组
        /// </summary>
        /// <param name="igram">积分图</param>
        /// <param name="gramwidth">图像宽度</param>
        /// <param name="gramheight">图像高度</param>
        /// <param name="template">模板</param>
        /// <param name="wx">检测窗口左上角X坐标</param>
        /// <param name="wy">检测窗口左上角Y坐标</param>
        /// <returns></returns>
        public static UInt32[] GetFeatureCodeGroup(int[,] igram, int gramwidth, int gramheight, BaseClassifierTemplate template, int wx, int wy)
        {
            UInt32[] featurevalue = new UInt32[template.GroupNum];

            if (wx + template.BmpWidth > gramwidth || wy + template.BmpHeight > gramheight)
            {
                return featurevalue;
            }

            for (int i = 0; i < template.GroupNum; i++)
            {
                UInt32 fvalue = 0;
                for (int j = 0; j < template.FeatureNum; j++)
                {
                    fvalue = fvalue << 1;    // 左移1位
                    Rectangle r = template.BitFeatures[i, j];
                    int x = r.X + wx;  // 最左侧的x
                    int y = r.Y + wy;  // 最上面的y
                    int w = r.Width;
                    int h = r.Height;
                    int xm = x + (w - 1) / 2;  // 中间的x
                    int ym = y + (h - 1) / 2;  // 中间的y
                    int xb = x + (w - 1);       // 最右侧的x
                    int yb = y + (h - 1);        // 最下面的y
                    
                    if (r.Width > r.Height)    // 横向特征
                    {
                        int feature_left = igram[yb, xm] + igram[y, x] - igram[y, xm] - igram[yb, x];
                        int feature_right = igram[yb, xb] + igram[y, xm] - igram[y, xb] - igram[yb, xm];
                        if (feature_left > feature_right)
                        {
                            fvalue++;
                        }
                    }
                    else if (r.Width == 1)  // 纵向特征
                    {
                        int feature_up = igram[ym, xb] + igram[y, x] - igram[y, xb] - igram[ym, x];
                        int feature_bottom = igram[yb, xb] + igram[ym, x] - igram[ym, xb] - igram[yb, x];
                        if (feature_up > feature_bottom)
                        {
                            fvalue++;
                        }
                    }
                }
                featurevalue[i] = fvalue;    // 一个组中的特征数不能超过16个
            }
            return featurevalue;
        }

        /// <summary>
        /// 放大或缩小模板的尺寸
        /// </summary>
        /// <param name="coef">变化系数</param>
        public void ResizeTemplate(double coef)
        {
            BmpWidth = (int)(BmpWidth * coef);
            BmpHeight = (int)(BmpHeight * coef);
            for (int i = 0; i < GroupNum; i++)
            {
                for (int j = 0; j < FeatureNum; j++)
                {
                    BitFeatures[i, j].X = (int)(BitFeatures[i, j].X * coef);
                    BitFeatures[i, j].Y = (int)(BitFeatures[i, j].Y * coef);
                    BitFeatures[i, j].Width = (int)(BitFeatures[i, j].Width * coef);
                    BitFeatures[i, j].Height = (int)(BitFeatures[i, j].Height * coef);
                }
            }
        }

        /// <summary>
        /// 模板对应检测窗口图像的宽度
        /// </summary>
        public int BmpWidth;
        /// <summary>
        /// 模板对应检测窗口图像的高度
        /// </summary>
        public int BmpHeight;
        /// <summary>
        /// 模板对应的特征分组数量
        /// </summary>
        public int GroupNum;
        /// <summary>
        /// 每一组对应的特征数量
        /// </summary>
        public int FeatureNum;
        /// <summary>
        /// 模板中特征位置、大小
        /// </summary>
        public Rectangle[,] BitFeatures;
    }



    public class QuickDetect
    {
        /// <summary>
        /// 用模板去检测图像中的物体
        /// </summary>
        /// <param name="bmpSource">源图像，需24位或32位真彩位图</param>
        /// <param name="template">基础分类器模板</param>
        /// <param name="fern">Fern</param>
        /// <returns></returns>
        public static RectangleCollection DetectObject(Bitmap bmpSource, BaseClassifierTemplate template, Fern fern)
        {
            RectangleCollection rc = new RectangleCollection();
            Bitmap bmp = bmpSource;
            if (bmpSource.Width < template.BmpWidth || bmpSource.Height < template.BmpHeight)
            {
                return null;
            }

            if (bmp != null && (bmp.PixelFormat == PixelFormat.Format24bppRgb || bmp.PixelFormat ==
               PixelFormat.Format32bppRgb || bmp.PixelFormat == PixelFormat.Format32bppArgb))
            {
                UInt32[] featurecode = null;
                bmp = ImgOper.Grayscale(bmpSource);
                int[,] igram = ImgOper.Integrogram(bmp, 1);

                while (template.BmpWidth < bmp.Width && template.BmpHeight < bmp.Height)
                {
                    for (int y = 0; y < bmp.Height - template.BmpHeight + 1; y += (template.BmpHeight / 10))
                    {
                        for (int x = 0; x < bmp.Width - template.BmpWidth + 1; x += (template.BmpWidth / 10))
                        {
                            //int posnum = 0;
                            //int negnum = 0;
                            featurecode = BaseClassifierTemplate.GetFeatureCodeGroup(igram, bmp.Width, bmp.Height, template, x, y);
                            double prob = 0;
                            for (int i=0; i<template.GroupNum; i++)
                            {
                                prob += fern.Probability[featurecode[i]];
                                //prob = fern.Probability[featurecode[i]];
                                //if (prob > 0.5)
                                //{
                                //    posnum++;
                                //}
                                //else
                                //{
                                //    negnum++;
                                //}
                            }
                            prob = prob / template.GroupNum;
                            if (prob > 0.5)
                            //if (posnum > negnum)
                            {
                                Rectangle rect = new Rectangle(x, y, template.BmpWidth, template.BmpHeight);
                                rc.Add(rect);
                            }
                        }
                    }
                    template.ResizeTemplate(1.2);
                }
            }
            return rc;
        }

        /// <summary>
        /// 用训练好的模型测试已标记样本的类别
        /// </summary>
        /// <param name="sample">已标记样本图像</param>
        /// <param name="template">基础分类器模板</param>
        /// <param name="fern">Fern</param>
        /// <returns>返回测试结果，类别</returns>
        public static int DetectSample(Bitmap sample, BaseClassifierTemplate template, Fern fern)
        {
            Bitmap bmp = null;
            if (sample.Width != template.BmpWidth || sample.Height != template.BmpHeight)
            {
                bmp = ImgOper.ResizeImage(sample, template.BmpWidth, template.BmpHeight);
            }
            else
            {
                bmp = sample;
            }

            if (bmp != null && (bmp.PixelFormat == PixelFormat.Format24bppRgb || bmp.PixelFormat ==
              PixelFormat.Format32bppRgb || bmp.PixelFormat == PixelFormat.Format32bppArgb))
            {
                bmp = ImgOper.Grayscale(bmp);
                int[,] igram = ImgOper.Integrogram(bmp, 1);
                UInt32[] featurecode = BaseClassifierTemplate.GetFeatureCodeGroup(igram, bmp.Width, bmp.Height, template, 0, 0);
                double prob = 0;
                for (int i = 0; i < template.GroupNum; i++)
                {
                    prob += fern.Probability[featurecode[i]];
                }
                prob = prob / template.GroupNum;
                if (prob > 0.5)
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }
            return 0;
        }
    }
}
