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

namespace CvTest
{
    public class Tld
    {
        private int poscnt = 0;
        private int negcnt = 0;

        /// <summary>
        /// 初始化tld
        /// </summary>
        public Tld()
        {
            PosMapCollection = new ValuedBitmapCollection();
            NegMapCollection = new ValuedBitmapCollection();
            PosLength = 0;
            NegLength = 0;
            PosCenter = null;
            NegCenter = null;
            Dimension = 0;
        }

        /// <summary>
        /// 训练正样本
        /// </summary>
        /// <param name="bmp">正样本位图</param>
        public void TrainPositive(Bitmap bmp)
        {
            Bitmap samplebmp = null;
            double neg_distance = 0;
            double pos_distance = 0;
            bool hasinserted = false;   // 指明样本是否已插入队列

            samplebmp = ImgOper.ResizeImage(bmp, Parameter.DETECT_WINDOW_SIZE.Width, Parameter.DETECT_WINDOW_SIZE.Height);
            samplebmp = ImgOper.Grayscale(samplebmp);

            for (double angle = (-1) * Parameter.ANGLE_BORDER; angle < Parameter.ANGLE_BORDER; angle += Parameter.ANGLE_INTERVAL)
            {
                Bitmap bmpclone = ImgOper.RotateImage(samplebmp, angle);
                bmpclone = ImgOper.ResizeImage(bmpclone, Parameter.DETECT_WINDOW_SIZE.Width, Parameter.DETECT_WINDOW_SIZE.Height);

                for (double scale = (-1) * Parameter.SCALE_BORDER; scale < Parameter.SCALE_BORDER; scale += Parameter.SCALE_INTERVAL)
                {
                    // 往两个方向去，所以是减号
                    IntPoint lt = new IntPoint((int)(bmpclone.Width * scale / 2), (int)(bmpclone.Height * scale / 2));
                    IntPoint rt = new IntPoint(bmpclone.Width - 1 - (int)(bmpclone.Width * scale / 2), (int)(bmpclone.Height * scale / 2));
                    IntPoint rb = new IntPoint(bmpclone.Width - 1 - (int)(bmpclone.Width * scale / 2), bmpclone.Height - 1 - (int)(bmpclone.Height * scale / 2));
                    IntPoint lb = new IntPoint((int)(bmpclone.Width * scale / 2), bmpclone.Height - 1 - (int)(bmpclone.Height * scale / 2));
                    Bitmap scalebmp = ImgOper.QuadrilateralTransform(bmpclone, lt, rt, rb, lb);

                    HogGram hogGram = HogGram.GetHogFromBitmap(scalebmp, Parameter.CELL_SIZE.Width, Parameter.CELL_SIZE.Height, Parameter.PART_NUMBER);
                    NormBlockVectorGram blockGram = new NormBlockVectorGram(hogGram, Parameter.BLOCK_SIZE.Width, Parameter.BLOCK_SIZE.Height);

                    Rectangle rect = new Rectangle(0, 0, hogGram.HogSize.Width, hogGram.HogSize.Height);
                    double[] vect = blockGram.GetHogWindowVec(rect);

                    if (Dimension != 0 && vect.Length != Dimension)
                    {
                        throw new Exception("输入正样本的尺寸与其他样本尺寸不一致！");
                    }

                    ValuedBitmap vbmp = null;
                    if (NegCenter != null && PosCenter != null)
                    {
                        // 计算离正负中心的距离
                        for (int i = 0; i < vect.Length; i++)
                        {
                            neg_distance += Math.Abs(vect[i] - NegCenter[i]);
                            pos_distance += Math.Abs(vect[i] - PosCenter[i]);
                        }

                        // 与负样本中心重合时，说明是负样本，不能插入正样本队列
                        if (neg_distance == 0)
                        {
                            return;
                        }

                        // 检测到的正样本加入样本队列的第二道关，如果不够接近正样本中心，就无法加入队列
                        // 按照Hog检测的判定条件，正距离乘以Parameter.POS_DIST_COEF，使其避开边界
                        if (neg_distance < pos_distance * Parameter.POS_DIST_COEF)
                        {
                            return;
                        }

                        // 带归一化的系数，如果用pos_distance/neg_distance，值可能会溢出;
                        // 将pos_distance / (pos_distance + neg_distance)作为正样本的评价系数，值越小越接近正样本
                        vbmp = new ValuedBitmap(scalebmp, pos_distance / (pos_distance + neg_distance));
                    }
                    else
                    {
                        // 如果正或负样本库还没建立起来，则Val暂时赋值为1
                        vbmp = new ValuedBitmap(scalebmp, 1);
                    }

                    // 检测到的正样本加入样本队列的第三道关，与正样本评价系数的有序队列比较后，决定是否加入样本队列
                    hasinserted = InsertValuedBitmap(ref PosMapCollection, vbmp, Parameter.POS_LIMITED_NUMBER);
                    PosLength = PosMapCollection.Count;

                    //// 人工观察正样本插入情况
                    //if (hasinserted && vbmp != null)
                    //{
                    //    vbmp.VBitmap.Save("Image\\pos_save\\" + poscnt + "_" + vbmp.Val + ".jpg");
                    //    poscnt++;
                    //}

                    // 如果样本已经插入队列，说明样本比较可信，重新计算样本中心
                    if (hasinserted)
                    {
                        if (PosCenter == null)
                        {
                            Dimension = vect.Length;
                            PosCenter = new double[Dimension];
                        }

                        for (int i = 0; i < Dimension; i++)
                        {
                            PosCenter[i] = (PosCenter[i] * PosLength + vect[i]) / (PosLength + 1);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 训练负样本
        /// </summary>
        /// <param name="bmp">负样本位图</param>
        public void TrainNegative(Bitmap bmp)
        {
            Bitmap samplebmp = null;
            double neg_distance = 0;
            double pos_distance = 0;
            bool hasinserted = false;   // 指明样本是否已插入队列

            if (bmp.Width / Parameter.DETECT_WINDOW_SIZE.Width > bmp.Height / Parameter.DETECT_WINDOW_SIZE.Height)
            {
                samplebmp = ImgOper.ResizeImage(bmp,
                    (int)(bmp.Width * Parameter.DETECT_WINDOW_SIZE.Height / bmp.Height), Parameter.DETECT_WINDOW_SIZE.Height);
            }
            else
            {
                samplebmp = ImgOper.ResizeImage(bmp, Parameter.DETECT_WINDOW_SIZE.Width,
                    (int)(bmp.Height * Parameter.DETECT_WINDOW_SIZE.Width / bmp.Width));
            }
            samplebmp = ImgOper.CutImage(samplebmp, 0, 0, Parameter.DETECT_WINDOW_SIZE.Width, Parameter.DETECT_WINDOW_SIZE.Height);
            samplebmp = ImgOper.Grayscale(samplebmp);

            HogGram hogGram = HogGram.GetHogFromBitmap(samplebmp, Parameter.CELL_SIZE.Width, Parameter.CELL_SIZE.Height, Parameter.PART_NUMBER);
            NormBlockVectorGram blockGram = new NormBlockVectorGram(hogGram, Parameter.BLOCK_SIZE.Width, Parameter.BLOCK_SIZE.Height);

            Rectangle rect = new Rectangle(0, 0, hogGram.HogSize.Width, hogGram.HogSize.Height);
            double[] vect = blockGram.GetHogWindowVec(rect);

            if (Dimension != 0 && vect.Length != Dimension)
            {
                throw new Exception("输入负样本的尺寸与其他样本尺寸不一致！");
            }

            ValuedBitmap vbmp = null;
            if (PosCenter != null && NegCenter != null)
            {
                // 计算离正负中心的距离
                for (int i = 0; i < vect.Length; i++)
                {
                    neg_distance += Math.Abs(vect[i] - NegCenter[i]);
                    pos_distance += Math.Abs(vect[i] - PosCenter[i]);
                }

                // 与正样本中心重合时，说明是正样本，不能插入负样本队列
                if (pos_distance == 0)
                {
                    return;
                }

                // 负样本加入样本队列的第二道关，如果不够接近负样本中心，就无法加入队列
                // 按照Hog检测的判定条件，正距离乘以Parameter.POS_DIST_COEF，使其避开边界
                if (pos_distance * Parameter.POS_DIST_COEF < neg_distance)
                {
                    return;
                }

                // 带归一化的系数，如果用neg_distance / pos_distance，值可能会溢出;
                // 将neg_distance / (pos_distance + neg_distance)作为负样本的评价系数，值越小越接近负样本
                vbmp = new ValuedBitmap(samplebmp, neg_distance / (pos_distance + neg_distance));
            }
            else
            {
                // 如果正样本库还没建立起来，则Val暂时赋值为1
                vbmp = new ValuedBitmap(samplebmp, 1);
            }

            // 负样本加入样本队列的第三道关，与负样本评价系数的有序队列比较后，决定是否加入样本队列
            hasinserted = InsertValuedBitmap(ref NegMapCollection, vbmp, Parameter.NEG_LIMITED_NUMBER);
            NegLength = NegMapCollection.Count;

            //// 人工观察负样本插入情况
            //if (hasinserted && vbmp != null)
            //{
            //    vbmp.VBitmap.Save("Image\\neg_save\\" + negcnt + "_" + vbmp.Val + ".jpg");
            //    negcnt++;
            //}

            // 如果样本已经插入队列，说明样本比较可信，重新计算样本中心
            if (hasinserted)
            {
                if (NegCenter == null)
                {
                    Dimension = vect.Length;
                    NegCenter = new double[Dimension];
                }

                for (int i = 0; i < Dimension; i++)
                {
                    NegCenter[i] = (NegCenter[i] * NegLength + vect[i]) / (NegLength + 1);
                }
            }
        }

        /// <summary>
        /// Hog检测, 被检测到图像自动缩放到BMPLIMITSIZE容忍范围内，并在检测完后将检测框自动放大之前缩小的倍率
        /// </summary>
        /// <param name="bmp">位图</param>
        public RectangleCollection HogDetect(Bitmap bmp)
        {
            RectangleCollection resultCollection = null;
            if (bmp == null)
            {
                return null;
            }

            if (NegCenter == null && PosCenter == null)
            {
                return null;
            }

            DateTime dt = DateTime.Now;
            double elapse = 0;

            // 针对原图的缩放倍率
            double se = 1;

            if (bmp.Width > Parameter.BMPLIMITSIZE.Width || bmp.Height > Parameter.BMPLIMITSIZE.Height)
            {
                se = bmp.Width / (double)Parameter.BMPLIMITSIZE.Width > bmp.Height / (double)Parameter.BMPLIMITSIZE.Height ?
                    bmp.Width / (double)Parameter.BMPLIMITSIZE.Width : bmp.Height / (double)Parameter.BMPLIMITSIZE.Height;
                bmp = ImgOper.ResizeImage(bmp, (int)(bmp.Width / se), (int)(bmp.Height / se));
            }
            bmp = ImgOper.Grayscale(bmp);

            //bmp = ImgOper.GaussianConvolution(bmp, GAUSSIAN_SIGMA, GAUSSIAN_SIZE);   // 高斯卷积，使得图像平滑

            // 所有层的检测结果
            ArrayList resultlayers = new ArrayList();
            // 初始缩放因子
            double scalecoef = 1.0;
            Bitmap scalebmp = null;
            int newwidth = (int)(bmp.Width / scalecoef);
            int newheight = (int)(bmp.Height / scalecoef);
            // 每层最小距离点的集合
            ArrayList idx_layermindistance = new ArrayList();
            int cnt = 0;
            do
            {
                scalebmp = ImgOper.ResizeImage(bmp, newwidth, newheight);
                HogGram hogGram = HogGram.GetHogFromBitmap(scalebmp, Parameter.CELL_SIZE.Width, Parameter.CELL_SIZE.Height, Parameter.PART_NUMBER);
                NormBlockVectorGram blockGram = new NormBlockVectorGram(hogGram, Parameter.BLOCK_SIZE.Width, Parameter.BLOCK_SIZE.Height);

                DetectResultLayer detectlayer = new DetectResultLayer();
                // !!!!!!检测窗口的像素尺寸必须能被cell尺寸整除!!!!!!像素尺寸除以hog尺寸就是检测窗口的尺寸
                detectlayer.DetectResult = blockGram.DetectImgByHogWindow(
                    new Size(Parameter.DETECT_WINDOW_SIZE.Width / Parameter.CELL_SIZE.Width, Parameter.DETECT_WINDOW_SIZE.Height / Parameter.CELL_SIZE.Height),
                    NegCenter, PosCenter, Parameter.POS_DIST_COEF);
                if (detectlayer.DetectResult == null)
                {
                    return null;
                }

                detectlayer.ScaleCoef = scalecoef;
                resultlayers.Add(detectlayer);         // 本层检测结果加入队列

                scalecoef *= Parameter.SCALE_COEF;    // 逐次缩小图像
                newwidth = (int)(bmp.Width / scalecoef);
                newheight = (int)(bmp.Height / scalecoef);
                cnt++;
            } while (newwidth > 2 * Parameter.DETECT_WINDOW_SIZE.Width && newheight > 2 * Parameter.DETECT_WINDOW_SIZE.Height);

            elapse = DateTime.Now.Subtract(dt).TotalSeconds;

            // 框出所有可能的物体
            WindowResult[] wr = null;
            Rectangle rect;
            double mindist = -1;
            WindowResult min_obj = null;
            double min_scalecoef = 1;
            resultCollection = new RectangleCollection();

            foreach (DetectResultLayer layer in resultlayers)
            {
                wr = layer.DetectResult;
                for (int i = 0; i < wr.Length; i++)
                {
                    if (wr[i].label == 1)
                    {
                        if (mindist == -1 || mindist > wr[i].PosDistance)
                        {
                            mindist = wr[i].PosDistance;
                            min_obj = wr[i];
                            min_scalecoef = layer.ScaleCoef;
                        }

                        rect = new Rectangle((int)(wr[i].ImageRegion.X * layer.ScaleCoef * se),
                            (int)(wr[i].ImageRegion.Y * layer.ScaleCoef * se),
                            (int)(wr[i].ImageRegion.Width * layer.ScaleCoef * se),
                            (int)(wr[i].ImageRegion.Height * layer.ScaleCoef * se));
                        resultCollection.Add(rect);
                    }
                }
            }

            //rect = new Rectangle((int)(min_obj.ImageRegion.X * min_scalecoef * se),
            //    (int)(min_obj.ImageRegion.Y * min_scalecoef * se),
            //    (int)(min_obj.ImageRegion.Width * min_scalecoef * se),
            //    (int)(min_obj.ImageRegion.Height * min_scalecoef * se));
            //resultCollection.Add(rect);
            return resultCollection;
        }

        #region 这两个方法效率太低，效果也一般，不建议使用
        /// <summary>
        /// 计算最近正样本距离系数，按照距离而不是相关系数，这样效率高，系数越小，检测对象越接近目标
        /// </summary>
        /// <param name="rect">检测目标区域</param>
        /// <param name="bmp">位图</param>
        /// <returns>最近距离系数， double.MaxValue表示计算异常或没计算</returns>
        private double NearestNeighbour(Rectangle rect, Bitmap bmp)
        {
            Bitmap sample = null;
            Bitmap detect = null;

            Rectangle gramRect = Rectangle.Empty;

            HogGram sampleHGram = null;
            HogGram detectHGram = null;

            NormBlockVectorGram sampleBlock = null;
            NormBlockVectorGram detectBlock = null;

            double[] detectvect = null;
            double[] samplevect = null;
            ArrayList posvects = null;
            ArrayList negvects = null;

            double minposdist = double.MaxValue;
            double minnegdist = double.MaxValue;
            double dist = 0;
            double nearestdist = double.MaxValue;


            if (PosLength == 0 || NegLength == 0)
            {
                return nearestdist;
            }

            // 正样本载入
            posvects = new ArrayList();
            for (int i = 0; i < PosLength; i++)
            {
                sample = PosMapCollection[i].VBitmap;
                sample = ImgOper.ResizeImage(sample, Parameter.DETECT_WINDOW_SIZE.Width, Parameter.DETECT_WINDOW_SIZE.Height);
                sample = ImgOper.Grayscale(sample);

                sampleHGram = HogGram.GetHogFromBitmap(sample, Parameter.CELL_SIZE.Width, Parameter.CELL_SIZE.Height, Parameter.PART_NUMBER);
                sampleBlock = new NormBlockVectorGram(sampleHGram, Parameter.BLOCK_SIZE.Width, Parameter.BLOCK_SIZE.Height);

                gramRect = new Rectangle(0, 0, sampleHGram.HogSize.Width, sampleHGram.HogSize.Height);
                samplevect = sampleBlock.GetHogWindowVec(gramRect);
                posvects.Add(samplevect);
            }

            // 负样本载入
            negvects = new ArrayList();
            for (int i = 0; i < NegLength; i++)
            {
                sample = NegMapCollection[i].VBitmap;
                sample = ImgOper.ResizeImage(sample, Parameter.DETECT_WINDOW_SIZE.Width, Parameter.DETECT_WINDOW_SIZE.Height);
                sample = ImgOper.Grayscale(sample);

                sampleHGram = HogGram.GetHogFromBitmap(sample, Parameter.CELL_SIZE.Width, Parameter.CELL_SIZE.Height, Parameter.PART_NUMBER);
                sampleBlock = new NormBlockVectorGram(sampleHGram, Parameter.BLOCK_SIZE.Width, Parameter.BLOCK_SIZE.Height);

                gramRect = new Rectangle(0, 0, sampleHGram.HogSize.Width, sampleHGram.HogSize.Height);
                samplevect = sampleBlock.GetHogWindowVec(gramRect);
                negvects.Add(samplevect);
            }


            detect = ImgOper.CutImage(bmp, rect.X, rect.Y, rect.Width, rect.Height);
            detect = ImgOper.ResizeImage(detect, Parameter.DETECT_WINDOW_SIZE.Width, Parameter.DETECT_WINDOW_SIZE.Height);
            detect = ImgOper.Grayscale(detect);

            detectHGram = HogGram.GetHogFromBitmap(detect, Parameter.CELL_SIZE.Width, Parameter.CELL_SIZE.Height, Parameter.PART_NUMBER);
            detectBlock = new NormBlockVectorGram(detectHGram, Parameter.BLOCK_SIZE.Width, Parameter.BLOCK_SIZE.Height);

            gramRect = new Rectangle(0, 0, detectHGram.HogSize.Width, detectHGram.HogSize.Height);
            detectvect = detectBlock.GetHogWindowVec(gramRect);

            foreach (double[] svect in posvects)
            {
                dist = ImgStatCompute.ComputeDistance(detectvect, svect);
                if (dist < minposdist)
                {
                    minposdist = dist;
                }
            }

            foreach (double[] svect in negvects)
            {
                dist = ImgStatCompute.ComputeDistance(detectvect, svect);

                if (dist < minnegdist)
                {
                    minnegdist = dist;
                }
            }

            if (minnegdist != 0 || minposdist != 0)
            {
                nearestdist = minposdist / (minposdist + minnegdist);
            }

            return nearestdist;
        }

        /// <summary>
        /// 在疑似目标集合中找到最可信的目标
        /// </summary>
        /// <param name="rectCollection">目标集合</param>
        /// <param name="bmp">位图</param>
        /// <returns>最可信目标</returns>
        private Rectangle MostConfidentObject(RectangleCollection rectCollection, Bitmap bmp)
        {
            double distcoef = double.MaxValue;
            double mindistcoef = double.MaxValue;
            Rectangle confidentRect = Rectangle.Empty;

            if (rectCollection == null || rectCollection.Count == 0)
            {
                return confidentRect;
            }

            foreach (Rectangle rect in rectCollection)
            {
                distcoef = NearestNeighbour(rect, bmp);
                if (distcoef < mindistcoef)
                {
                    mindistcoef = distcoef;
                    confidentRect = rect;
                }
            }

            return confidentRect;
        }
        #endregion

        #region 这两个方法效率一般，效果不错，可以通过优化训练样本使用
        /// <summary>
        /// 计算正样本最大相关系数，数值越大，被检测对象越接近目标
        /// </summary>
        /// <param name="rect">检测目标区域</param>
        /// <param name="bmp">位图</param>
        /// <returns>正样本最大相关系数</returns>
        public double MostAssociate(Rectangle rect, Bitmap bmp)
        {
            double maxcoef = 0;   // 返回值
            double coef = 0;
            double maxposcoef = 0;
            double maxnegcoef = 0;

            if (rect == Rectangle.Empty)
            {
                return maxcoef;
            }
            Bitmap patch = ImgOper.CutImage(bmp, rect.X, rect.Y, rect.Width, rect.Height);
            patch = ImgOper.ResizeImage(patch, Parameter.DETECT_WINDOW_SIZE.Width, Parameter.DETECT_WINDOW_SIZE.Height);
            patch = ImgOper.Grayscale(patch);

            foreach (ValuedBitmap posbmp in PosMapCollection)
            {
                coef = ImgStatCompute.ComputeAssociationCoef(patch, posbmp.VBitmap);
                if (maxposcoef < coef)
                {
                    maxposcoef = coef;
                }
            }

            foreach (ValuedBitmap negbmp in NegMapCollection)
            {
                coef = ImgStatCompute.ComputeAssociationCoef(patch, negbmp.VBitmap);
                if (maxnegcoef < coef)
                {
                    maxnegcoef = coef;
                }
            }

            if (maxnegcoef != 0 || maxposcoef != 0)
            {
                maxcoef = maxposcoef / (maxposcoef + maxnegcoef);
            }

            return maxcoef;
        }

        /// <summary>
        /// 从目标集合中找出最大相关系数的目标，即最可信对象
        /// </summary>
        /// <param name="rectCollection">目标集合</param>
        /// <param name="bmp">位图</param>
        /// <returns>最可信对象</returns>
        public Rectangle MostAssociateObject(RectangleCollection rectCollection, Bitmap bmp)
        {
            double coef = 0;
            double maxcoef = 0;
            Rectangle confidentRect = Rectangle.Empty;

            if (rectCollection == null || rectCollection.Count == 0)
            {
                return confidentRect;
            }

            foreach (Rectangle rect in rectCollection)
            {
                coef = MostAssociate(rect, bmp);
                if (maxcoef < coef)
                {
                    maxcoef = coef;
                    confidentRect = rect;
                }
            }

            return confidentRect;
        }
        #endregion

        #region 这两个方法效率不错，效果还可以，可进一步研究使用
        /// <summary>
        /// 计算最近正样本距离系数，按照距离而不是相关系数，这样效率高，系数越小，检测对象越接近目标
        /// </summary>
        /// <param name="rect">检测目标区域</param>
        /// <param name="bmp">整幅位图</param>
        /// <returns>最近距离系数， double.MaxValue表示计算异常或没计算</returns>
        public double MinDistance(RectangleF rect, Bitmap bmp)
        {
            double mindistance = double.MaxValue;   // 返回值
            double dist = double.MaxValue;
            double minposdist = double.MaxValue;
            double minnegdist = double.MaxValue;

            if (rect == Rectangle.Empty)
            {
                return mindistance;
            }

            DateTime dt = DateTime.Now;
            double elapse = 0;
            Bitmap patch = ImgOper.CutImage(bmp, (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
            patch = ImgOper.ResizeImage(patch, Parameter.DETECT_WINDOW_SIZE.Width, Parameter.DETECT_WINDOW_SIZE.Height);
            patch = ImgOper.Grayscale(patch);

            int[,] patchgram = ImgOper.Integrogram(patch, 1);

            byte[] patchdata = ImgOper.GetGraybmpData(patch);
            double patchmean = (double)patchgram[patch.Height - 1, patch.Width - 1] / (double)(patch.Width * patch.Height);
            double[] patchdatad = new double[patchdata.Length];
            for (int i = 0; i < patchdata.Length; i++)
            {
                patchdatad[i] = patchdata[i] - patchmean;
            }

            foreach (ValuedBitmap posbmp in PosMapCollection)
            {
                int[,] posgram = ImgOper.Integrogram(posbmp.VBitmap, 1);
                byte[] posdata = ImgOper.GetGraybmpData(posbmp.VBitmap);
                double posmean = (double)posgram[posbmp.VBitmap.Height - 1, posbmp.VBitmap.Width - 1] / (double)(posbmp.VBitmap.Width * posbmp.VBitmap.Height);
                double[] posdatad = new double[posdata.Length];
                for (int i = 0; i < posdata.Length; i++)
                {
                    posdatad[i] = posdata[i] - posmean;
                }
                dist = ImgStatCompute.ComputeDistance(patchdatad, posdatad);
                if (dist < minposdist)
                {
                    minposdist = dist;
                }
            }

            foreach (ValuedBitmap negbmp in NegMapCollection)
            {
                int[,] neggram = ImgOper.Integrogram(negbmp.VBitmap, 1);
                byte[] negdata = ImgOper.GetGraybmpData(negbmp.VBitmap);
                double negmean = (double)neggram[negbmp.VBitmap.Height - 1, negbmp.VBitmap.Width - 1] / (double)(negbmp.VBitmap.Width * negbmp.VBitmap.Height);
                double[] negdatad = new double[negdata.Length];
                for (int i = 0; i < negdata.Length; i++)
                {
                    negdatad[i] = negdata[i] - negmean;
                }
                dist = ImgStatCompute.ComputeDistance(patchdatad, negdatad);
                if (dist < minnegdist)
                {
                    minnegdist = dist;
                }
            }

            if (minnegdist != 0 || minposdist != 0)
            {
                // 带归一化的系数，如果用minposdist/minnegdist，值可能会溢出
                mindistance = minposdist / (minposdist + minnegdist);  
            }

            elapse = DateTime.Now.Subtract(dt).TotalMilliseconds;
            return mindistance;
        }

        /// <summary>
        /// 从目标集合中找出最小距离系数的目标，即最可信对象
        /// </summary>
        /// <param name="rectCollection">目标集合</param>
        /// <param name="bmp">位图</param>
        /// <returns>最可信对象</returns>
        public Rectangle MinDistanceObject(RectangleCollection rectCollection, Bitmap bmp)
        {
            double dist = double.MaxValue;
            double mindist = double.MaxValue;
            Rectangle confidentRect = Rectangle.Empty;

            if (rectCollection == null || rectCollection.Count == 0)
            {
                return confidentRect;
            }

            foreach (Rectangle rect in rectCollection)
            {
                dist = MinDistance(rect, bmp);
                if (mindist > dist)
                {
                    mindist = dist;
                    confidentRect = rect;
                }
            }

            // mindist必须小于0.5,这样才能说明是正样例
            if (mindist < Parameter.MEDIAN_COEF)
            {
                return confidentRect;
            }
            else
            {
                return Rectangle.Empty;
            }
        }
        #endregion

        /// <summary>
        /// 正样本检测专家，专门检测被误判为负的正样本
        /// </summary>
        /// <param name="detectCollection">检测模块产生的区域集合</param>
        /// <param name="trackerRect">跟踪模块产生的区域</param>
        /// <param name="bmp">被检测位图</param>
        public void PositiveExpert(RectangleCollection detectCollection, RectangleF trackerRect, Bitmap bmp)
        {
            double areaproportion = 0;
            bool nointersect = true;         // 指明跟踪模块和检测模块得到的区域是否无交集

            if (trackerRect == Rectangle.Empty)
            {
                return;
            }

            foreach (Rectangle rect in detectCollection)
            {
                areaproportion = AreaProportion(trackerRect, rect);
                if (areaproportion > Parameter.AREA_INTERSECT_PROPORTION)
                {
                    nointersect = false;
                    break;
                }
            }

            // 没有交集，说明存在被误判为负的正样例
            if (nointersect)
            {
                // 判断跟踪到的目标是否确实为要识别的物体，正距离归一化系数小于0.5
                // 目标加入正样本队列的第一道关，目标必须看起来像正样本（正距离归一化系数小于0.5）
                if (MinDistance(trackerRect, bmp) < Parameter.MEDIAN_COEF)
                {
                    for (double lrshift = (-1) * Parameter.SHIFT_BORDER; lrshift < Parameter.SHIFT_BORDER + Parameter.SHIFT_INTERVAL; lrshift += Parameter.SHIFT_INTERVAL)
                    {
                        for (double tbshift = (-1) * Parameter.SHIFT_BORDER; tbshift < Parameter.SHIFT_BORDER + Parameter.SHIFT_INTERVAL; tbshift += Parameter.SHIFT_INTERVAL)
                        {
                            if (trackerRect.X + lrshift >= 0 && trackerRect.X + trackerRect.Width - 1 + lrshift < bmp.Width - 1 &&
                                trackerRect.Y + tbshift >= 0 && trackerRect.Y + trackerRect.Height - 1 + tbshift < bmp.Height - 1)
                            {
                                Bitmap patch = ImgOper.CutImage(bmp, (int)(trackerRect.X + lrshift), (int)(trackerRect.Y + tbshift),
                                    (int)trackerRect.Width, (int)trackerRect.Height);
                                TrainPositive(patch);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 负样本检测专家，专门检测被误判为正的负样本和增加负样本集合
        /// </summary>
        /// <param name="detectCollection">检测模块产生的区域集合</param>
        /// <param name="trackerRect">跟踪模块产生的区域</param>
        /// <param name="bmp">被检测的位图</param>
        /// <returns>返回最可信对象区域</returns>
        public Rectangle NegativeExpert(RectangleCollection detectCollection, RectangleF trackerRect, Bitmap bmp)
        {
            // 复制一个矩形集合是为了让NExpert独立与PExpert
            RectangleCollection newRectCollection = new RectangleCollection();
            if (detectCollection != null)
            {
                foreach (Rectangle detectrect in detectCollection)
                {
                    newRectCollection.Add(detectrect);
                }
            }
            if (trackerRect != Rectangle.Empty)
            {
                // 将跟踪到的目标也加入待评估的对象集合
                newRectCollection.Add(new Rectangle((int)trackerRect.X, (int)trackerRect.Y, (int)trackerRect.Width, (int)trackerRect.Height));
            }

            DateTime dt = DateTime.Now;
            // 最可信的对象
            Rectangle confidentRect = MinDistanceObject(newRectCollection, bmp);
            double elapse = DateTime.Now.Subtract(dt).TotalMilliseconds;

            if (confidentRect != Rectangle.Empty)
                newRectCollection.Remove(confidentRect);

            dt = DateTime.Now;
            foreach (Rectangle rect in newRectCollection)
            {
                // 判断目标是否确实为背景，正距离归一化系数大于0.5
                // 目标加入负样本队列的第一道关，目标必须看起来像负样本（正距离归一化系数大于0.5）
                if (MinDistance(rect, bmp) > Parameter.MEDIAN_COEF)
                {
                    double areainsect = AreaProportion(confidentRect, rect);
                    // 与最可信对象交集面积小于AREA_INTERSECT_PROPORTION的为负样本，加入负样本列表
                    if (areainsect < Parameter.AREA_INTERSECT_PROPORTION)
                    {
                        Bitmap patch = ImgOper.CutImage(bmp, rect.X, rect.Y, rect.Width, rect.Height);
                        TrainNegative(patch);
                    }
                }
            }
            elapse = DateTime.Now.Subtract(dt).TotalMilliseconds;

            return confidentRect;
        }

        /// <summary>
        /// 计算两个矩形相交面积占小的那个矩形的比例
        /// </summary>
        /// <param name="rect1">第一个矩形</param>
        /// <param name="rect2">第二个矩形</param>
        /// <returns>面积占比</returns>
        public double AreaProportion(RectangleF rect1, RectangleF rect2)
        {
            double proportion = 0;
            if (rect1 == RectangleF.Empty || rect2 == RectangleF.Empty)
            {
                return proportion;
            }

            double area1 = rect1.Width * rect1.Height;
            double area2 = rect2.Width * rect2.Height;

            double intersectwidth = 0;
            double intersectheight = 0;

            if (rect1.X > rect2.X)
            {
                intersectwidth = rect2.Width - (rect1.X - rect2.X);
                if (intersectwidth > rect1.Width)
                {
                    intersectwidth = rect1.Width;
                }
            }
            else
            {
                intersectwidth = rect1.Width - (rect2.X - rect1.X);
                if (intersectwidth > rect2.Width)
                {
                    intersectwidth = rect2.Width;
                }
            }

            // 如果为负，说明无交集，而且远离
            if (intersectwidth < 0)
            {
                intersectwidth = 0;
            }

            if (rect1.Y > rect2.Y)
            {
                intersectheight = rect2.Height - (rect1.Y - rect2.Y);
                if (intersectheight > rect1.Height)
                {
                    intersectheight = rect1.Height;
                }
            }
            else
            {
                intersectheight = rect1.Height - (rect2.Y - rect1.Y);
                if (intersectheight > rect2.Height)
                {
                    intersectheight = rect2.Height;
                }
            }

            // 如果为负，说明无交集，而且远离
            if (intersectheight < 0)
            {
                intersectheight = 0;
            }

            proportion = area1 < area2 ? intersectwidth * intersectheight / area1 : intersectwidth * intersectheight / area2;
            return proportion;
        }

        /// <summary>
        /// 将ValuedBitmapCollection按照val值从小到大排序
        /// </summary>
        /// <param name="vpCollection">vbCollection</param>
        /// <returns></returns>
        public static ValuedBitmapCollection SortValuedBitmapCollection(ValuedBitmapCollection vbCollection)
        {
            if (vbCollection == null || vbCollection.Count == 0)
            {
                return new ValuedBitmapCollection();
            }

            int min;
            for (int i = 0; i < vbCollection.Count - 1; i++)
            {
                min = i;
                for (int j = i + 1; j < vbCollection.Count; j++)
                {
                    if (((ValuedBitmap)vbCollection[j]).Val < ((ValuedBitmap)vbCollection[min]).Val)
                    {
                        min = j;
                    }
                }
                ValuedBitmap t = (ValuedBitmap)vbCollection[min];
                vbCollection[min] = vbCollection[i];
                vbCollection[i] = t;
            }
            return vbCollection;
        }

        /// <summary>
        /// 在有序（val值从小到大）的ValuedBitmapCollection中插入ValuedBitmap，ValuedBitmapCollection中元素的数量
        /// 不能超出limitedNumber，插入成功后返回集合元素数量
        /// </summary>
        /// <param name="vbCollection">集合</param>
        /// <param name="vbmp">集合元素</param>
        /// <param name="limitedNumer">集合元素数量上限</param>
        /// <returns>返回是否插入成功</returns>
        public static bool InsertValuedBitmap(ref ValuedBitmapCollection vbCollection, ValuedBitmap vbmp, int limitedNumer)
        {
            ValuedBitmap tmpMap = null;
            bool hasinserted = false;   // 返回值，是否已经插入

            if (vbmp == null)
            {
                return hasinserted;
            }

            if (vbCollection == null || vbCollection.Count == 0)
            {
                vbCollection = new ValuedBitmapCollection();
                vbCollection.Add(vbmp);
                hasinserted = true;
                return hasinserted;
            }

            for (int i = 0; i < vbCollection.Count; i++)
            {
                if (vbmp.Val < vbCollection[i].Val)
                {
                    // 有新元素要插入，暂时保存队列最后一个元素，由limitedNumer来决定是否加入队列末尾
                    tmpMap = vbCollection[vbCollection.Count - 1];   
                    for (int j = vbCollection.Count - 1; j>i; j--)
                    {
                        vbCollection[j] = vbCollection[j - 1];
                    }
                    vbCollection[i] = vbmp;
                    hasinserted =  true;
                    // 队列未满，可加入末尾
                    if (vbCollection.Count < limitedNumer)
                    {
                        vbCollection.Add(tmpMap);
                    }
                    break;
                }
            }

            if (!hasinserted && vbCollection.Count < limitedNumer)
            {
                vbCollection.Add(vbmp);
                hasinserted = true;
            }

            return hasinserted;
        }

        /// <summary>
        /// 正样本中心
        /// </summary>
        public double[] PosCenter;
        /// <summary>
        /// 负样本中心
        /// </summary>
        public double[] NegCenter;
        /// <summary>
        /// 检测向量维数
        /// </summary>
        public int Dimension;
        /// <summary>
        /// 正样本集合
        /// </summary>
        ValuedBitmapCollection PosMapCollection;
        /// <summary>
        /// 正样本数量
        /// </summary>
        public int PosLength;
        /// <summary>
        /// 负样本集合
        /// </summary>
        ValuedBitmapCollection NegMapCollection;
        /// <summary>
        /// 负样本数量
        /// </summary>
        public int NegLength;
    }

    /// <summary>
    /// 带值的位图
    /// </summary>
    public class ValuedBitmap
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="bmp">位图</param>
        /// <param name="bmpvalue">位图值</param>
        public ValuedBitmap(Bitmap bmp, double bmpvalue)
        {
            VBitmap = bmp;
            Val = bmpvalue;
        }
        /// <summary>
        /// 位图
        /// </summary>
        public Bitmap VBitmap;
        /// <summary>
        /// 位图对应的权重值
        /// </summary>
        public double Val;
    }
    public class ValuedBitmapCollection : ArrayList
    {
        /// <summary>
        /// 根据索引，获取Bitmap对象
        /// </summary>
        /// <param name="index">索引</param>
        /// <returns>Bitmap对象</returns>
        public ValuedBitmap this[int index]
        {
            get
            {
                if (this == null || this.Count == 0)
                {
                    return null;
                }
                else
                {
                    return (ValuedBitmap)base[index];
                }
            }
            set
            {
                if (value.GetType() != typeof(ValuedBitmap))
                {
                    throw new Exception("类型不匹配，不能赋值。");
                }
                base[index] = value;
            }
        }

        /// <summary>
        /// 集合中添加Bitmap
        /// </summary>
        /// <param name="bmp"></param>
        public void Add(ValuedBitmap bmp)
        {
            base.Add(bmp);
        }

        /// <summary>
        /// 集合中增加Bitmap
        /// </summary>
        /// <param name="bmp"></param>
        public void Remove(ValuedBitmap bmp)
        {
            base.Remove(bmp);
        }
    }

    /// <summary>
    /// Rectangle集合
    /// </summary>
    public class RectangleCollection : ArrayList
    {
        public Rectangle this[int index]
        {
            get
            {
                if (this == null || this.Count == 0)
                {
                    return Rectangle.Empty;
                }
                else
                {
                    return (Rectangle)base[index];
                }
            }
        }

        public void Add(Rectangle rect)
        {
            base.Add(rect);
        }
        public void Remove(Rectangle rect)
        {
            base.Remove(rect);
        }
    }
}
