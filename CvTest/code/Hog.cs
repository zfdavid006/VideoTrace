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
    /// 单个像素的梯度
    /// </summary>
    public struct GradientElement
    {
        /// <summary>
        /// 模长
        /// </summary>
        public double rho;
        /// <summary>
        /// 角度
        /// </summary>
        public double theta;
    }

    /// <summary>
    /// Hog直方图单元
    /// </summary>
    public struct HogHistElement
    {
        /// <summary>
        /// 模长
        /// </summary>
        public double rho;
        /// <summary>
        /// 角度
        /// </summary>
        public double theta;
    }

    /// <summary>
    /// Hog直方图单元
    /// </summary>
    public class HogHistCell
    {
        /// <summary>
        /// 将PI分成partnum等份
        /// </summary>
        /// <param name="partnum">等份数</param>
        public HogHistCell(int partnum)
        {
            HistElements = new HogHistElement[partnum];
            for (int i = 0; i < partnum; i++)
            {
                HistElements[i].theta = -Math.PI / 2 + i * Math.PI / partnum;
                HistElements[i].rho = 0;
            }
        }

        /// <summary>
        /// 梯度给Hog单元投票
        /// </summary>
        /// <param name="ge">梯度</param>
        public void VoteHogCell(GradientElement ge, int partnum)
        {
            for (int i = 0; i < partnum; i++)
            {
                if (ge.theta >= HistElements[i].theta && ge.theta < HistElements[i].theta + Math.PI / partnum)
                {
                    HistElements[i].rho += ge.rho;
                    break;
                }
            }
        }

        /// <summary>
        /// 单元内的直方图
        /// </summary>
        public HogHistElement[] HistElements;
    }

    /// <summary>
    /// 单个窗口检测结果
    /// </summary>
    public class WindowResult
    {
        /// <summary>
        /// 标签，也就是检测结果
        /// </summary>
        public double label;
        /// <summary>
        /// Hog检测窗口对应的位图区域（像素区域）
        /// </summary>
        public Rectangle ImageRegion;
        /// <summary>
        /// 离正样本中心距离
        /// </summary>
        public double PosDistance;
        /// <summary>
        /// 离负样本中心
        /// </summary>
        public double NegDistance;
    }

    /// <summary>
    /// Hog图
    /// </summary>
    public class HogGram
    {
        /// <summary>
        /// 初始化Hog图
        /// </summary>
        /// <param name="bmpWidth">源位图宽</param>
        /// <param name="bmpHeight">源位图高</param>
        /// <param name="cellWidth">Hog单元宽</param>
        /// <param name="cellHeight">Hog单元高</param>
        /// <param name="partnum">等分数</param>
        public HogGram(int bmpWidth, int bmpHeight, int cellWidth, int cellHeight, int partnum)
        {
            if (cellWidth > 0 && cellHeight > 0 && bmpWidth >= cellWidth && bmpHeight >= cellHeight)
            {
                CellSize.Width = cellWidth;
                CellSize.Height = cellHeight;
                HogSize.Width = bmpWidth / cellWidth;
                HogSize.Height = bmpHeight / cellHeight;
                PartNumber = partnum;
                StartX = bmpWidth % cellWidth / 2;
                StartY = bmpHeight % cellHeight / 2;
                HogCells = new HogHistCell[HogSize.Width * HogSize.Height];
                for (int i = 0; i < HogCells.Length; i++)
                {
                    HogCells[i] = new HogHistCell(partnum);
                }
            }
        }

        /// <summary>
        /// 给Hog图投票
        /// </summary>
        /// <param name="bmpX">投票像素点X坐标</param>
        /// <param name="bmpY">投票像素点Y坐标</param>
        /// <param name="ge">投票像素点梯度向量(rho, theta)</param>
        private void VoteHog(int bmpX, int bmpY, GradientElement ge)
        {
            if (bmpX >= StartX && bmpX < StartX + HogSize.Width * CellSize.Width
                && bmpY >= StartY && bmpY < StartY + HogSize.Height * CellSize.Height)
            {
                int hogCellX = (bmpX - StartX) / CellSize.Width;
                int hogCellY = (bmpY - StartY) / CellSize.Height;
                int hogCellIndex = hogCellX + hogCellY * HogSize.Width;
                HogCells[hogCellIndex].VoteHogCell(ge, PartNumber);
            }
        }

        /// <summary>
        /// 从位图（灰度图）中获取Hog图，位图像素是以从左到右、从下到上的顺序在内存中从低地址排列到高地址
        /// 但是C#的Bitmap对象好像把这顺序颠倒过来了，根据实际经验发现，在BitmapData中，位图数据以从左到
        /// 右、从上到下的顺序排列，和图像坐标的顺序一致
        /// </summary>
        /// <param name="bitmapSource">源位图</param>
        /// <param name="cellHeight">单元的高度</param>
        /// <param name="cellWidth">单元的宽度</param>
        /// <param name="partNumber">PI弧度等分数</param>
        /// <returns>Hog图</returns>
        public static HogGram GetHogFromBitmap(Bitmap bitmapSource, int cellWidth, int cellHeight, int partNumber)
        {
            HogGram hogGram = null;
            if (bitmapSource != null && bitmapSource.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                int width = bitmapSource.Width;
                int height = bitmapSource.Height;

                hogGram = new HogGram(width, height, cellWidth, cellHeight, partNumber);

                if (hogGram == null)
                {
                    return hogGram;
                }

                Rectangle rect = new Rectangle(0, 0, width, height);

                // 获得位图内容数据
                BitmapData dataSource = bitmapSource.LockBits(rect, ImageLockMode.ReadOnly, bitmapSource.PixelFormat);

                // Stride为位图中每一行以4字节对齐的行宽
                int strideSource = dataSource.Stride;

                unsafe
                {
                    byte* ptrSource = (byte*)dataSource.Scan0.ToPointer();
                    byte* ptr1 = null;
                    for (int row = 0; row < height; row++)
                    {
                        ptr1 = ptrSource + strideSource * row;
                        for (int col = 0; col < width; col++)
                        {
                            GradientElement ge = new GradientElement();
                            if (row == 0 || row == height - 1 || col == 0 || col == width - 1)
                            {
                                ge.rho = 0;
                                ge.theta = 0;
                            }
                            else
                            {
                                // 位图的在内存中的排列是从左到右，从下到上的，但BitmapData貌似做了优化，把位图数据在内存中的排列
                                // 顺序改为与坐标系一致，即从左到右、从上到下
                                double gradX = *(ptr1 + 1) - *(ptr1 - 1);
                                double gradY = *(ptr1 + strideSource) - *(ptr1 - strideSource); 
                                ge.rho = Math.Sqrt(gradX * gradX + gradY * gradY);
                                ge.theta = Math.Atan2(gradY, gradX);   // 注意坐标系是垂直翻转的
                            }

                            int bmpX = col;
                            int bmpY = row;
                            //int bmpY = height - 1 - row;  // 由于BitmapData自动颠倒了位图存储序，所以这个写法是错的。
                            hogGram.VoteHog(bmpX, bmpY, ge);

                            ptr1++;
                        }
                    }
                }

                bitmapSource.UnlockBits(dataSource);
            }
            return hogGram;
        }

        /// <summary>
        /// 在源图像中开始计算Hog的起始点X坐标
        /// </summary>
        public int StartX;
        /// <summary>
        /// 在源图像中开始计算Hog的起始点Y坐标
        /// </summary>
        public int StartY;
        /// <summary>
        /// Hog图的尺寸，以Cell为单位
        /// </summary>
        public Size HogSize;
        /// <summary>
        /// Cell的尺寸，以像素为单位
        /// </summary>
        public Size CellSize;
        /// <summary>
        /// PI等分数
        /// </summary>
        public int PartNumber;
        /// <summary>
        /// Hog单元列表
        /// </summary>
        public HogHistCell[] HogCells;
    }

    /// <summary>
    /// 以块为单位归一化后的Hog向量图，以Cell为坐标点，每个坐标点对应一个以该点为
    /// 左下角的块且已经完成块内归一化的向量
    /// </summary>
    public class NormBlockVectorGram
    {
        /// <summary>
        /// 构造函数，由Hog图生成块内归一化后的向量图
        /// </summary>
        /// <param name="hogGram">Hog图</param>
        /// <param name="blockWidth">块宽</param>
        /// <param name="blockHeight">块高</param>
        public NormBlockVectorGram(HogGram hogGram, int blockWidth, int blockHeight)
        {
            BlockSize.Width = blockWidth;
            BlockSize.Height = blockHeight;

            int hogWidth = hogGram.HogSize.Width;
            int hogHeight = hogGram.HogSize.Height;

            int hogPartNumber = hogGram.PartNumber;

            BlockGramWidth = hogWidth - blockWidth + 1;
            BlockGramHeight = hogHeight - blockHeight + 1;

            NormBlockVectors = new ArrayList();

            if (hogGram != null && hogWidth >= blockWidth && hogHeight >= blockWidth)
            {
                HGram = hogGram;

                for (int row = 0; row < BlockGramHeight; row++)
                {
                    for (int col = 0; col < BlockGramWidth; col++)
                    {
                        double[] vec = new double[blockWidth * blockHeight * hogPartNumber];
                        double vecsum = 0;
                        for (int i = 0; i < blockHeight; i++)
                        {
                            for (int j = 0; j < blockWidth; j++)
                            {
                                for (int p = 0; p < hogPartNumber; p++)
                                {
                                    double r = hogGram.HogCells[(col + j) + (row + i) * hogWidth].HistElements[p].rho;

                                    // hogGram.HogCells的索引是从小到大。
                                    vec[p + j * hogPartNumber + i * hogPartNumber * blockWidth] = r;
                                    //vecsum += r * r;    // 这种归一化是将向量约束在“半径”为1的“球面”上，因为r>=0，实际上是分布在1/4个球面
                                    vecsum += r;         // 这种归一化是将向量约束在“边长为1的”立方体“内
                                }
                            }
                        }

                        // 归一化向量（非常重要）
                        for (int i = 0; i < vec.Length; i++)
                        {
                            if (vecsum != 0)
                            {
                                //vec[i] = vec[i] / Math.Sqrt(vecsum);  // 对应上述第一种归一化法
                                vec[i] = vec[i] / vecsum;
                            }
                            else
                            {
                                vec[i] = 0;
                            }
                        }

                        NormBlockVectors.Add(vec);
                    }
                }
            }
        }

        /// <summary>
        /// 对检测窗口覆盖的Hog图进行特征向量提取,!!!!!!检测窗口的像素尺寸必须能被cell尺寸整除!!!!!!
        /// </summary>
        /// <param name="hogWindow">检测窗口Hog图尺寸</param>
        /// <returns>返回hogWindow覆盖处的特征向量</returns>
        /// <remarks>!!!!!!检测窗口的像素尺寸必须能被cell尺寸整除!!!!!!</remarks>
        public double[] GetHogWindowVec(Rectangle hogWindow)
        {
            #region 直接用原始HOG向量代替块内归一化的HOG向量，速度快，但对正负样本非常敏感，准确率低，效果不佳
            //double[] predictVec = new double[HGram.PartNumber * hogWindow.Width *hogWindow.Height];
            //int cnt = 0;
            //for (int row = 0; row < hogWindow.Height; row++)
            //{
            //    for (int col = 0; col < hogWindow.Width; col++)
            //    {
            //        for (int i = 0; i < HGram.PartNumber; i++)
            //        {
            //            predictVec[cnt] = HGram.HogCells[(hogWindow.X + col) + (hogWindow.Y + row) * HGram.HogSize.Width].HistElements[i].rho;
            //            cnt++;
            //        }
            //    }
            //}
            #endregion

            if (hogWindow.Width < BlockSize.Width || hogWindow.Height < BlockSize.Height)
            {
                return null;
            }

            double[] predictVec = new double[HGram.PartNumber * BlockSize.Width * BlockSize.Height *
                (hogWindow.Height - BlockSize.Height + 1) * (hogWindow.Width - BlockSize.Width + 1)];
            int cnt = 0;
            for (int row = 0; row < hogWindow.Height - BlockSize.Height + 1; row++)
            {
                for (int col = 0; col < hogWindow.Width - BlockSize.Width + 1; col++)
                {
                    double[] vec = (double[])NormBlockVectors[(hogWindow.X + col) + (hogWindow.Y + row) * BlockGramWidth];
                    for (int i = 0; i < vec.Length; i++)
                    {
                        predictVec[cnt] = vec[i];
                        cnt++;
                    }
                }
            }
            return predictVec;
        }

        #region svm中的方法，已废弃
        ///// <summary>
        ///// 将归一化后的向量列表写入文件，按照libsvm训练样本的格式
        ///// </summary>
        ///// <param name="filepath">文件路径</param>
        ///// <param name="samplelabel">样本标签，1:正样本,-1:负样本</param>
        //public void WriteSamepleToFile(string filepath, int samplelabel)
        //{
        //    if (this.NormBlockVectors != null && this.NormBlockVectors.Count > 0)
        //    {
        //        FileStream fs = null;
        //        StreamWriter sw = null;

        //        try
        //        {
        //            fs = File.Open(filepath, FileMode.Append);
        //            sw = new StreamWriter(fs);
        //            sw.Write(samplelabel);
        //            int cnt = 0;
        //            foreach (object obj in this.NormBlockVectors)
        //            {
        //                double[] vec = (double[])obj;
        //                for (int i = 0; i < vec.Length; i++)
        //                {
        //                    cnt++;
        //                    if (vec[i] != 0)
        //                    {
        //                        sw.Write(" {0}:{1}", cnt, vec[i]);
        //                    }
        //                }
        //            }
        //            sw.Write("\n");
        //        }
        //        catch (Exception ex)
        //        {
        //            throw new Exception("File Operation fail!" + ex.Message);
        //        }
        //        finally
        //        {
        //            if (sw != null)
        //            {
        //                sw.Close();
        //                sw.Dispose();
        //            }
        //            if (fs != null)
        //            {
        //                fs.Close();
        //                fs.Dispose();
        //            }
        //        }
        //    }
        //}

        ///// <summary>
        ///// 预测给出的特征向量所属分类
        ///// </summary>
        ///// <param name="model">训练好的模型</param>
        ///// <param name="predictvec">待预测的特征向量</param>
        ///// <returns>返回预测值，1为正类，-1为负类</returns>
        //private double PredictVecClass(svm_model model, double[] predictvec)
        //{
        //    if (model != null && predictvec != null && predictvec.Length > 0)
        //    {
        //        svm_node[] x = new svm_node[predictvec.Length];
        //        for (int i = 0; i < predictvec.Length; i++)
        //        {
        //            x[i] = new svm_node();
        //            x[i].index = i + 1;
        //            x[i].value_Renamed = predictvec[i];
        //        }
        //        //DateTime dt = DateTime.Now;
        //        double r = svm.svm_predict(model, x);
        //        //double elapse = DateTime.Now.Subtract(dt).TotalMilliseconds;
        //        return r;
        //    }
        //    else
        //    {
        //        return 0;
        //    }
        //}
        #endregion

        /// <summary>
        /// 移动检测窗口匹配待检测图像，步长为一个Hog单元
        /// </summary>
        /// <param name="hogwindowsize">检测窗口的hog尺寸</param>
        /// <param name="neg_center">负样本中心</param>
        /// <param name="pos_center">正样本中心</param>
        /// <param name="distancecoef">正距离系数，让检测到的目标物体稍微靠近正样本中心，排除决策边界上的点，一般取1.1到1.2之间</param>
        /// <returns>返回检测结果序列</returns>
        public WindowResult[] DetectImgByHogWindow(Size hogwindowsize, double[] neg_center, double[] pos_center, double distancecoef)
        {
            if (hogwindowsize.Width > HGram.HogSize.Width || hogwindowsize.Height > HGram.HogSize.Height 
                || neg_center == null || pos_center == null)
            {
                return null;
            }

            if (neg_center.Length != pos_center.Length)
            {
                return null;
            }

            WindowResult[] windowresult = new WindowResult[(HGram.HogSize.Height - hogwindowsize.Height + 1) * (HGram.HogSize.Width - hogwindowsize.Width + 1)];
            int cnt = 0;
            for (int row = 0; row < HGram.HogSize.Height - hogwindowsize.Height + 1; row++)
            {
                for (int col = 0; col < HGram.HogSize.Width - hogwindowsize.Width + 1; col++)
                {
                    double neg_distance = 0;
                    double pos_distance = 0;
                    Rectangle rect = new Rectangle(col, row, hogwindowsize.Width, hogwindowsize.Height);
                    double[] vec = GetHogWindowVec(rect);
                    if (vec.Length != pos_center.Length)
                    {
                        return null;
                    }

                    windowresult[cnt] = new WindowResult();
                    for (int i = 0; i < vec.Length; i++)
                    {
                        neg_distance += Math.Abs(vec[i] - neg_center[i]);
                        pos_distance += Math.Abs(vec[i] - pos_center[i]);
                    }
                    windowresult[cnt].PosDistance = pos_distance;
                    windowresult[cnt].NegDistance = neg_distance;

                    if (pos_distance * distancecoef < neg_distance)
                    {
                        windowresult[cnt].label = 1;
                    }
                    else
                    {
                        windowresult[cnt].label = -1;
                    }

                    // 将HOG检测窗口转化为位图像素窗口
                    windowresult[cnt].ImageRegion = new Rectangle(
                        HGram.StartX + col * HGram.CellSize.Width,
                        HGram.StartY + row * HGram.CellSize.Height,
                        hogwindowsize.Width * HGram.CellSize.Width,
                        hogwindowsize.Height * HGram.CellSize.Height);
                    cnt++;
                }
            }
            return windowresult;
        }

        /// <summary>
        /// 对应的HogGram
        /// </summary>
        public HogGram HGram;
        /// <summary>
        /// 块尺寸，以Cell为单位
        /// </summary>
        public Size BlockSize;
        /// <summary>
        /// 块内归一向量图宽，hog图宽-块宽+1
        /// </summary>
        public int BlockGramWidth;
        /// <summary>
        /// 块内归一向量图高，hog图高-块高+1
        /// </summary>
        public int BlockGramHeight;
        /// <summary>
        /// 归一化后的向量列表
        /// </summary>
        public ArrayList NormBlockVectors;
    }

    public class DetectResultLayer
    {
        /// <summary>
        /// 本层检索结果
        /// </summary>
        public WindowResult[] DetectResult;
        /// <summary>
        /// 缩放系数
        /// </summary>
        public double ScaleCoef;
    }

    /// <summary>
    /// 储存最小距离的相关信息
    /// </summary>
    public class RectVariance
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="lindex">层索引</param>
        /// <param name="windex">检测窗口索引</param>
        /// <param name="dist">距离值</param>
        public RectVariance(int lindex, int windex, double variance)
        {
            LayerIndex = lindex;
            WindowIndex = windex;
            Variance = variance;
        }
        /// <summary>
        /// 层索引
        /// </summary>
        public int LayerIndex;
        /// <summary>
        /// 检测窗口索引
        /// </summary>
        public int WindowIndex;
        /// <summary>
        /// 距离值
        /// </summary>
        public double Variance;
    };
}
