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
    /// 光流类
    /// </summary>
    public class OpticalFlow
    {
        
        /// <summary>
        /// 构造函数, 确定参数，生成两幅图像金字塔
        /// </summary>
        /// <param name="bmpI">第一幅位图</param>
        /// <param name="bmpJ">第二幅位图</param>
        /// <param name="ln">金字塔层数，一般取值是3，4，5</param>
        /// <param name="wx">计算每个点时，积分邻域横向半径，一般取值是2,3,4,5,6,7，在选取初始点和跟踪时最好取不同值，选取初始点时只要取1，跟踪时需要更大</param>
        /// <param name="wy">计算每个点时，积分邻域纵向半径，一般取值是2,3,4,5,6,7，在选取初始点和跟踪时最好取不同值，选取初始点时只要取1，跟踪时需要更大</param>
        /// <param name="k">每层计算迭代次数，一般5次左右就能收敛</param>
        /// <param name="accuracy">迭代误差精度阈值，比如0.03个像素</param>
        /// <param name="percentage">选取初始点时，最大较小lambda值的百分比，低于这个百分比的值被丢弃，可取5%或10%</param>
        /// <param name="arealen">选取初始点时，待选值需要比较长度为AreaLen的邻域内的所有值，只有待选值最大时才被留下来，可取3</param>
        /// <param name="minditance">选取初始点时，确定待选点之间的最小距离，小于这个距离的待选点需要删除，可取5或10</param>
        public OpticalFlow(int ln, int wx, int wy, int k, double accuracy,
            float percentage, int arealen, int minditance)
        {
            LN = ln;
            Wx = wx;
            Wy = wy;
            K = k;
            Accuracy = accuracy;
            Percentage = percentage;
            AreaLen = arealen;
            MinDistance = minditance;  
        }

        /// <summary>
        /// 将位图转换为层信息
        /// </summary>
        /// <param name="bmp">位图</param>
        /// <returns>返回LayerImageCollection</returns>
        public LayerImageCollection TransformBmpToLayerImg(Bitmap bmp)
        {
            if (bmp == null || bmp.PixelFormat != PixelFormat.Format8bppIndexed)
            {
                throw new Exception("原位图为空或者不是灰度图。");
            }

            int width = bmp.Width;
            int height = bmp.Height;
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData bmpdata = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);
            // Stride为位图中每一行以4字节对齐的行宽
            int strideSource = bmpdata.Stride;
            byte[] byteData = new byte[width * height];
            LayerImage li = null;
            LayerImageCollection layerCollection = null;
            unsafe
            {
                byte* ptr = (byte*)bmpdata.Scan0.ToPointer();
                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        // 位图的在内存中的排列是从左到右，从下到上的，但BitmapData貌似做了优化，把位图数据在内存中的排列
                        // 顺序改为与坐标系一致，即从左到右、从上到下
                        byteData[col + row * width] = *ptr;

                        ptr++;
                    }
                }
                bmp.UnlockBits(bmpdata);
            }

            layerCollection = new LayerImageCollection();
            li = new LayerImage(byteData, width, height);
            layerCollection.Add(li);   // 加入I的第0层数据

            for (int l = 1; l < LN; l++)
            {
                int w, h;  // 当前层图像的宽和高
                // width和height为前一层图像的宽和高
                w = (width + 1) / 2;
                h = (height + 1) / 2;
                byteData = new byte[w * h];
                for (int row = 0; row < h; row++)
                {
                    for (int col = 0; col < w; col++)
                    {
                        int leftX = 2 * col - 1;
                        int rightX = 2 * col + 1;
                        int lowY = 2 * row - 1;
                        int upY = 2 * row + 1;
                        int centerX = 2 * col;
                        int centerY = 2 * row;
                        if (leftX < 0)
                        {
                            leftX = 0;
                        }
                        if (rightX >= width)
                        {
                            rightX = width - 1;
                        }
                        if (lowY < 0)
                        {
                            lowY = 0;
                        }
                        if (upY >= height)
                        {
                            upY = height - 1;
                        }

                        // 根据上一层的数据生成当前层的数据
                        byteData[col + row * w] = (byte)(0.25 * li.ImageData[centerX + centerY * width] +
                            0.125 * (li.ImageData[leftX + centerY * width] + li.ImageData[rightX + centerY * width] +
                            li.ImageData[centerX + lowY * width] + li.ImageData[centerX + upY * width]) +
                            0.0625 * (li.ImageData[leftX + lowY * width] + li.ImageData[rightX + lowY * width] +
                            li.ImageData[leftX + upY * width] + li.ImageData[rightX + upY * width]));
                    }
                }
                li = new LayerImage(byteData, w, h);
                layerCollection.Add(li);

                width = w;
                height = h;
            }

            return layerCollection;
        }

        /// <summary>
        /// （这个方法基本用不到）在全图像范围内选择光流初始点, 返回值是PointF类型的列表
        /// </summary>
        /// <param name="rectchoice">选中的矩形区域</param>
        /// <param name="percentage">最大lambda乘以percentage，大于该值的留下来，可取5%或10%</param>
        /// <param name="arealen">区域长度，该范围区域内求最大的lambda，留下来，可取3</param>
        /// <param name="minditance">最小距离，超过该距离的lambda，留下来，可取5或10</param>
        /// <returns>返回被选中的数据点, PointF类型的</returns>
        public ArrayList InitialPointChoice(Rectangle rectchoice, float percentage, int arealen, int minditance)
        {
            float[] G = new float[4] { 0, 0, 0, 0 };
            float tmpvalue = 0;  // 临时储存中间值
            float lambda1 = 0;   // 较小的lambda
            float lambda2 = 0;   // 较大的lambda
            float lambda_max = 0;   // 图像中最大的较小lambda值
            ArrayList residual_points = new ArrayList();     // 返回值

            int width = (int)rectchoice.Width;
            int height = (int)rectchoice.Height;

            float[,] lambda_array = new float[height, width];

            // 初始化值
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    lambda_array[i, j] = 0;

            // 取得每一点处G矩阵的较小lambda值，和整个图像最大的较小lambda值
            for (int row = arealen; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    G = new float[4] { 0, 0, 0, 0 };
                    // 只有在涉及到灰度值时才需要用绝对位置，其他都用相对位置
                    for (float x = rectchoice.X + col - Wx; x <= rectchoice.X + col + Wx; x++)
                    {
                        for (float y = rectchoice.Y + row - Wy; y <= rectchoice.Y + row + Wy; y++)
                        {
                            float ix = Ix(0, x, y);
                            float iy = Iy(0, x, y);
                            G[0] += ix * ix;       // 第0行第0列
                            G[1] += ix * iy;       // 第0行第1列
                            G[2] += ix * iy;       // 第1行第0列
                            G[3] += iy * iy;       // 第1行第1列
                        }
                    }

                    if ((G[0] - G[3]) * (G[0] - G[3]) + 4 * G[1] * G[2] >= 0)
                    {
                        tmpvalue = (float)(Math.Sqrt((G[0] - G[3]) * (G[0] - G[3]) + 4 * G[1] * G[2]));
                        lambda1 = Math.Abs((float)((G[0] + G[3] + tmpvalue) / 2));   // 不知道是否要取绝对值？
                        lambda2 = Math.Abs((float)((G[0] + G[3] - tmpvalue) / 2));    // 不知道是否要取绝对值？
                        if (lambda1 > lambda2)
                        {
                            // lambda2被冲掉，事实上，后面的计算不需要用到lambda2
                            lambda1 = lambda2;
                        }

                        if (lambda1 > lambda_max)
                        {
                            lambda_max = lambda1;
                        }

                        lambda_array[row, col] = lambda1;
                    }
                }
            }

            // 将小于最大lambda值percentage的lambda值丢弃
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    if (lambda_array[row, col] < lambda_max * percentage)
                    {
                        lambda_array[row, col] = 0;
                    }
                }
            }

            ArrayList cancelPointArray = new ArrayList();      // 准备被丢弃的点坐标集合
            // 在arealen×arealen范围内选取局部lambda值最大的点
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    if (lambda_array[row, col] != 0)
                    {
                        // 边缘arealen/2的范围内的点都置为0
                        if (row - arealen / 2 < 0 || row + arealen / 2 > height - 1 ||
                        col - arealen / 2 < 0 || col - arealen / 2 > width - 1)
                        {
                            lambda_array[row, col] = 0;
                        }
                        else
                        {
                            bool terminate = false;
                            // 不是局部最大值的也置为0
                            for (int i = row - arealen / 2; i <= row + arealen / 2; i++)
                            {
                                for (int j = col - arealen / 2; j < col + arealen / 2; j++)
                                {
                                    if (lambda_array[row, col] < lambda_array[i, j])
                                    {
                                        cancelPointArray.Add(new PointF(col, row));
                                        terminate = true;
                                        break;
                                    }
                                }
                                if (terminate)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // 将设置为丢弃的lambda值真正丢弃
            foreach (PointF p in cancelPointArray)
            {
                lambda_array[(int)p.Y, (int)p.X] = 0;
            }

            // 只剩下相互之间距离超过minditance的点
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    if (lambda_array[row, col] != 0)
                    {
                        // 输出的时候也要用绝对位置
                        residual_points.Add(new PointF(rectchoice.X + col, rectchoice.Y + row));
                        for (int i = row - minditance; i <= row + minditance; i++)
                        {
                            for (int j = col - minditance; j < col + minditance; j++)
                            {
                                if (!(i == row && j == col))
                                {
                                    if (i >= 0 && i < height && j >= 0 && j < width)
                                    {
                                        lambda_array[i, j] = 0;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return residual_points;
        }

        /// <summary>
        /// 在Rect范围内随机选取percentage的点作为光流初始点
        /// </summary>
        /// <param name="rect">Rect范围</param>
        /// <param name="ptscount">光流点数量，比如100个点</param>
        /// <returns>返回被选中的数据点，PointF类型的</returns>
        public ArrayList ChooseRectRandomPoints(RectangleF rect, int ptscount)
        {
            ArrayList pointsCollection = new ArrayList();
            ArrayList choicePoints = new ArrayList();

            for (int row = 0; row < rect.Height; row++)
                for (int col = 0; col < rect.Width; col++)
                    pointsCollection.Add(new PointF(rect.X + col, rect.Y + row));

            // 如果rect为Empty，那么pointsCollection中将没有元素，返回点也就为空集
            if (pointsCollection.Count == 0)
            {
                return choicePoints;
            }

            if (ptscount > rect.Height * rect.Width)
            {
                ptscount = (int)(rect.Height * rect.Width);
            }

            for (int i = 0; i < ptscount; i++)
            {
                Random ra = new Random();
                ra = new Random(ra.Next() * unchecked((int)DateTime.Now.Ticks) * i);
                int idx = ra.Next(pointsCollection.Count);
                choicePoints.Add(pointsCollection[idx]);
                pointsCollection.RemoveAt(idx); 
            }
            return choicePoints;
        }

        /// <summary>
        /// 在rect范围内，按照interval间隔选取初始光流点
        /// </summary>
        /// <param name="rect">Rect范围</param>
        /// <param name="interval">间隔距离，比如5像素</param>
        /// <returns>返回被选中的数据点，PointF类型的</returns>
        public ArrayList ChooseRectIntervalPoints(Rectangle rect, int interval)
        {
            ArrayList choicePoints = new ArrayList();
            for (int row = 0; row < rect.Height; row++)
                for (int col = 0; col < rect.Width; col++)
                    if (row % interval == 0 && col % interval == 0)
                        choicePoints.Add(new PointF(rect.X + col, rect.Y + row));
            return choicePoints;
        }

        /// <summary>
        /// 跟踪给定点的算法
        /// </summary>
        /// <param name="firstlayercollection">第一幅图像金字塔</param>
        /// <param name="secondlayercollection">第二幅图像金字塔</param>
        /// <param name="u">第一幅图像中给定的点</param>
        /// <param name="wx">计算每个点时，积分邻域横向半径，此值寻找初始点时只需要取1，跟踪时要取的大一些</param>
        /// <param name="wy">计算每个点时，积分邻域纵向半径，此值寻找初始点时只需要取1，跟踪时要取的大一些</param>
        /// <returns>返回第二幅图像中对应的点</returns>
        public PointF TrackPoint(LayerImageCollection firstlayercollection, LayerImageCollection secondlayercollection, PointF u, int wx, int wy)
        {
            float[] G = null;
            float[] G_1 = null;   // G的逆矩阵
            PointF[] g = new PointF[LN];   // 前一层估计的光流
            PointF[] d = new PointF[LN];   // 本层计算出来的剩余光流
            PointF[] p = new PointF[LN];   // 原始点坐标在本层的映射坐标
            PointF[] v = new PointF[K + 1];     // 本层每次迭代计算出来的剩余光流

            Wx = wx;
            Wy = wy;

            if (firstlayercollection == null || secondlayercollection == null)
            {
                throw new Exception("有图像为空，无法跟踪。");
            }

            if (firstlayercollection[0].Width != secondlayercollection[0].Width)
            {
                throw new Exception("两幅图像大小不一致，无法跟踪。");
            }

            g[LN - 1].X = 0;
            g[LN - 1].Y = 0;

            for (int l = LN - 1; l >= 0; l--)
            {
                p[l].X = (float)(u.X / Math.Pow(2, l));
                p[l].Y = (float)(u.Y / Math.Pow(2, l));

                // 原图中给定的点已经在边界上，无法跟踪
                if (p[l].X > firstlayercollection[l].Width - 1 || p[l].Y > firstlayercollection[l].Height - 1)
                {
                    // 返回值中带有负数表示没有正确的结果
                    return new PointF(-1, -1);
                }

                G = new float[4] {0, 0, 0, 0};
                for (float x = p[l].X - Wx; x <= p[l].X + Wx; x++)
                {
                    for (float y = p[l].Y - Wy; y <= p[l].Y + Wy; y++)
                    {
                        float ix = Ix(l, x, y);
                        float iy = Iy(l, x, y);
                        G[0] += ix * ix;       // 第0行第0列
                        G[1] += ix * iy;       // 第0行第1列
                        G[2] += ix * iy;       // 第1行第0列
                        G[3] += iy * iy;       // 第1行第1列
                    }
                }

                // 求G的逆矩阵G_1
                float gdet = G[0] * G[3] - G[1] * G[2];

                // G的行列式为0，无法计算，跟踪丢失
                if (gdet == 0)
                {
                    return new PointF(-1, -1);
                }

                G_1 = new float[4] { 0, 0, 0, 0 };
                G_1[0] = G[3] / gdet;
                G_1[1] = -G[2] / gdet;
                G_1[2] = -G[1] / gdet;
                G_1[3] = G[0] / gdet;

                v[0].X = 0;
                v[0].Y = 0;
                for (int k = 1; k < K + 1; k++)
                {
                    int delt;
                    PointF b = new PointF(0, 0);
                    PointF yeta = new PointF(0, 0);
                    for (float x = p[l].X - Wx; x <= p[l].X + Wx; x++)
                    {
                        for (float y = p[l].Y - Wy; y <= p[l].Y + Wy; y++)
                        {
                            // 第二幅图的点已超出边界，跟踪丢失
                            if (x + g[l].X + v[k - 1].X < 0 || x + g[l].X + v[k - 1].X > secondlayercollection[l].Width - 1 ||
                                y + g[l].Y + v[k - 1].Y < 0 || y + g[l].Y + v[k - 1].Y > secondlayercollection[l].Height - 1)
                            {
                                return new PointF(-1, -1);
                            }

                            delt = firstlayercollection[l][x, y] - secondlayercollection[l][x + g[l].X + v[k - 1].X, y + g[l].Y + v[k - 1].Y];
                            b.X += delt * Ix(l, x, y);
                            b.Y += delt * Iy(l, x, y);
                        }
                    }

                    yeta.X = G_1[0] * b.X + G_1[1] * b.Y;
                    yeta.Y = G_1[2] * b.X + G_1[3] * b.Y;

                    v[k].X = v[k - 1].X + yeta.X;
                    v[k].Y = v[k - 1].Y + yeta.Y;
                }

                d[l].X = v[K].X;
                d[l].Y = v[K].Y;

                if (l > 0)
                {
                    g[l - 1].X = 2 * (g[l].X + d[l].X);
                    g[l - 1].Y = 2 * (g[l].Y + d[l].Y);
                }
            }
            return new PointF(u.X + g[0].X + d[0].X, u.Y + g[0].Y + d[0].Y);
        }

        /// <summary>
        /// 根据两幅图像金字塔和已选定的初始点，计算整体位移，返回位移值和缩放系数
        /// </summary>
        /// <param name="firstlayercollection">第一幅图像金字塔</param>
        /// <param name="secondlayercollection">第二幅图像金字塔</param>
        /// <param name="ipoints">第一幅图像中选中的初始点</param>
        ///  <param name="wx">计算每个点时，积分邻域横向半径，此值寻找初始点时只需要取1，跟踪时要取的大一些</param>
        /// <param name="wy">计算每个点时，积分邻域纵向半径，此值寻找初始点时只需要取1，跟踪时要取的大一些</param>
        /// <param name="reversebias">反向跟踪时，输出点和原来点之间的距离差阈值（像素值），该差值超过阈值被认为不是好的跟踪点，当被删除，建议取1</param>
        /// <param name="medianbias">每个点的位移与中值位移之间的最大距离(像素值)，超过这个距离的点认为是跟踪失败的，建议取10</param>
        /// <param name="scalestep">缩放台阶，缩放系数的变化刻度，如变化差小于0.2的视为不变，大于0.2小于0.4的视为0.2，以此类推</param>
        /// <param name="firstlayerpoints">第一幅图像的最终输出点</param>
        /// <param name="secondlayerpoints">第二幅图像的最终输出点</param>
        /// <returns>位移值和缩放系数</returns>
        public float[] ComputerDisplacement(LayerImageCollection firstlayercollection, LayerImageCollection secondlayercollection, ArrayList ipoints,
            float reversebias, // 这个参数不好控制，导致跟踪非常不稳定
            int wx, int wy,  float medianbias, float scalestep, 
            ref ArrayList firstlayerpoints, ref ArrayList secondlayerpoints)
        {
            if (ipoints == null || secondlayercollection == null || firstlayercollection == null)
            {
                return null;
            }

            // 输出结果，前两个维度表示位移，第三个维度表示区域缩放系数
            float[] outvector = new float[3];

            firstlayerpoints = new ArrayList();
            secondlayerpoints = new ArrayList();

            ArrayList jpoints = new ArrayList();
            ValuePointCollection fbPointsArray = new ValuePointCollection();      // 用于计算Forward-Backward的集合，ValuePoint类型的
            ValuePoint vp = null;

            foreach (PointF p in ipoints)
            {
                PointF jpoint = TrackPoint(firstlayercollection, secondlayercollection, p, wx, wy);    // 正跟踪，第一幅图跟踪到第二幅图
                PointF ipoint;

                if (jpoint.X >= 0 && jpoint.Y >= 0)
                {
                    vp = new ValuePoint();

                    vp.Ptf1 = p;          // 第一幅图的点加入队列
                    vp.Ptf2 = jpoint;   // 第二幅图的对应点加入队列
                    fbPointsArray.Add(vp);

                    // 这段代码导致跟踪极不稳定
                    ipoint = TrackPoint(secondlayercollection, firstlayercollection, jpoint, wx, wy);    // 反跟踪，第二幅图跟踪到第一幅图
                    vp.Val = (float)Math.Sqrt((p.X - ipoint.X) * (p.X - ipoint.X) + (p.Y - ipoint.Y) * (p.Y - ipoint.Y));  // 反向跟踪后产生的点与原来点的距离
                    if (vp.Val < reversebias)
                    {
                        fbPointsArray.Add(vp);
                    }
                }
            }

            if (fbPointsArray.Count == 0)
            {
                return null;
            }

            //// 这段代码不是很合适
            //fbPointsArray = ValuePoint.SortValuePointCollection(fbPointsArray);
            //// 根据Forward-Backward值，去掉一半的不稳定点
            //int arraycont = fbPointsArray.Count;
            //for (int i = arraycont / 2; i < arraycont; i++)
            //{
            //    fbPointsArray.RemoveAt(arraycont / 2);
            //}  // Forward-Backward计算完成

            // 将fbPointsArray用于中值位移计算
            for (int i = 0; i < fbPointsArray.Count; i++)
            {
                vp = fbPointsArray[i];
                // 求每一点的位移值，不作开方减小计算量，不影响结果
                vp.Val = (vp.Ptf2.X - vp.Ptf1.X) * (vp.Ptf2.X - vp.Ptf1.X) + (vp.Ptf2.Y - vp.Ptf1.Y) * (vp.Ptf2.Y - vp.Ptf1.Y);
            }

            fbPointsArray = ValuePoint.SortValuePointCollection(fbPointsArray);

            ValuePoint median_vp = fbPointsArray[fbPointsArray.Count / 2];

            outvector[0] = median_vp.Ptf2.X - median_vp.Ptf1.X;
            outvector[1] = median_vp.Ptf2.Y - median_vp.Ptf1.Y;                   // 中值位移计算完毕

            // 去掉位移偏离中值位移过大的点
            ValuePointCollection tmpCollection = new ValuePointCollection();

            foreach (ValuePoint vpoint in fbPointsArray)
            {
                float dist = (float)Math.Sqrt(Math.Pow(vpoint.Ptf2.X - vpoint.Ptf1.X - median_vp.Ptf2.X + median_vp.Ptf1.X, 2) +
                    Math.Pow(vpoint.Ptf2.Y - vpoint.Ptf1.Y - median_vp.Ptf2.Y + median_vp.Ptf1.Y, 2));
                if (dist > medianbias)      // 超过中值偏移的点删掉
                {
                    tmpCollection.Add(vpoint);
                }
                else      // 没超过中值位移的点加入图像金字塔
                {
                    firstlayerpoints.Add(vpoint.Ptf1);
                    secondlayerpoints.Add(vpoint.Ptf2);
                }
            }
            foreach (ValuePoint vpoint in tmpCollection)
            {
                fbPointsArray.Remove(vpoint);
            }

            // 计算区域的缩放值
            float[] scales = new float[fbPointsArray.Count - 1];
            for (int i = 0; i < fbPointsArray.Count - 1; i++)
            {
                ValuePoint v1 = fbPointsArray[i];
                ValuePoint v2 = fbPointsArray[i+1];
                if ((v2.Ptf1.X - v1.Ptf1.X) * (v2.Ptf1.X - v1.Ptf1.X) + (v2.Ptf1.Y - v1.Ptf1.Y) * (v2.Ptf1.Y - v1.Ptf1.Y) == 0)
                {
                    scales[i] = 1;
                }
                else
                {
                    scales[i] = (float)(Math.Sqrt((v2.Ptf2.X - v1.Ptf2.X) * (v2.Ptf2.X - v1.Ptf2.X) + (v2.Ptf2.Y - v1.Ptf2.Y) * (v2.Ptf2.Y - v1.Ptf2.Y)) /
                        Math.Sqrt((v2.Ptf1.X - v1.Ptf1.X) * (v2.Ptf1.X - v1.Ptf1.X) + (v2.Ptf1.Y - v1.Ptf1.Y) * (v2.Ptf1.Y - v1.Ptf1.Y)));
                }
            }
            outvector[2] = GetMedian(scales);
            if (outvector[2] < 0)
                outvector[2] = 1;
            else
                outvector[2] = 1 + (int)((outvector[2] - 1) / scalestep) * scalestep;

            return outvector;
        }

        /// <summary>
        /// 求得(x,y)处x的偏导数
        /// </summary>
        /// <param name="l">层号</param>
        /// <param name="x">x坐标</param>
        /// <param name="y">y坐标</param>
        /// <returns>偏导数值</returns>
        private float Ix(int l, float x, float y)
        {
            LayerImage li = I[l];
            return (li[x + 1, y] - li[x - 1, y]) / 2;
        }

        /// <summary>
        /// 求得(x,y)处y的偏导数
        /// </summary>
        /// <param name="l">层号</param>
        /// <param name="x">x坐标</param>
        /// <param name="y">y坐标</param>
        /// <returns>偏导数值</returns>
        private float Iy(int l, float x, float y)
        {
            LayerImage li = I[l];
            return (li[x, y + 1] - li[x, y - 1]) / 2;
        }

        /// <summary>
        /// 获取中值
        /// </summary>
        /// <param name="f">输入数组</param>
        /// <returns>返回中值</returns>
        private float GetMedian(float[] f)
        {
            if (f == null || f.Length == 0)
            {
                return -1;
            }

            int min;
            for (int i = 0; i < f.Length - 1; i++)
            {
                min = i;
                for (int j = i + 1; j < f.Length; j++)
                {
                    if (f[j] < f[min])
                    {
                        min = j;
                    }
                }
                float t = f[min];
                f[min] = f[i];
                f[i] = t;
            }
            return f[f.Length / 2];
        }

        /// <summary>
        /// 层数
        /// </summary>
        public int LN;
        /// <summary>
        /// 区域横向半径
        /// </summary>
        public int Wx;
        /// <summary>
        /// 区域纵向半径
        /// </summary>
        public int Wy;
        /// <summary>
        /// 每一层计算迭代次数
        /// </summary>
        public int K;
        /// <summary>
        /// 迭代误差精度阈值
        /// </summary>
        public double Accuracy;
        /// <summary>
        /// 选取初始点时，最大较小lambda值的百分比，低于这个百分比的值被丢弃
        /// </summary>
        public float Percentage;
        /// <summary>
        /// 选取初始点时，待选值需要比较长度为AreaLen的邻域内的所有值，只有待选值最大时才被留下来
        /// </summary>
        public int AreaLen;
        /// <summary>
        /// 选取初始点时，确定待选点之间的最小距离，小于这个距离的待选点需要删除
        /// </summary>
        public int MinDistance;
        /// <summary>
        /// 第一幅图像金字塔
        /// </summary>
        public LayerImageCollection I;
        /// <summary>
        /// 第一幅图像对应的关键点集合，PointF类型集合
        /// </summary>
        public ArrayList IPoints;
        /// <summary>
        /// 第二幅图像金字塔
        /// </summary>
        public LayerImageCollection J;
        /// <summary>
        /// 第二幅图像对应的关键点集合，PointF类型集合
        /// </summary>
        public ArrayList JPoints;
    }

    /// <summary>
    /// 一个层上的图像数据
    /// </summary>
    public class LayerImage
    {
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="imgdata">图像数据</param>
        /// <param name="width">图像宽</param>
        /// <param name="height">图像高</param>
        public LayerImage(byte[] imgdata, int width, int height)
        {
            ImageData = imgdata;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// 获得本层图像(x, y)处的值
        /// </summary>
        /// <param name="x">x坐标</param>
        /// <param name="y">y坐标</param>
        /// <returns></returns>
        public byte this[int x, int y]
        {
            get
            {
                if (x < 0)
                {
                    x = 0;
                }
                else if (x > Width - 1)
                {
                    x = Width - 1;
                }

                if (y < 0)
                {
                    y = 0;
                }
                else if (y > Height - 1)
                {
                    y = Height - 1;
                }

                return ImageData[x + y * Width];
            }
        }

        /// <summary>
        /// 获得本层图像(x, y)处的值， x和y非整数
        /// </summary>
        /// <param name="x">x坐标</param>
        /// <param name="y">y坐标</param>
        /// <returns></returns>
        public byte this[float x, float y]
        {
            get
            {
                if (x < 0)
                {
                    x = 0;
                }
                else if (x > Width - 1)
                {
                    x = Width - 1;
                }

                if (y < 0)
                {
                    y = 0;
                }
                else if (y > Height - 1)
                {
                    y = Height - 1;
                }

                int x0 = (int)x;
                int y0 = (int)y;
                float ax = x - x0;
                float ay = y - y0;
                int x1 = x0 + 1;
                int y1 = y0 + 1;

                if (x1 > Width - 1)
                {
                    x1 = x0;
                }

                if (y1 > Height - 1)
                {
                    y1 = y0;
                }

                return (byte)((1 - ax) * (1 - ay) * ImageData[x0 + y0 * Width] + ax * (1 - ay) * ImageData[x1 + y0 * Width] +
                        (1 - ax) * ay * ImageData[x0 + y1 * Width] + ax * ay * ImageData[x1 + y1 * Width]);
            }
        }

        /// <summary>
        /// 本层图像数据
        /// </summary>
        public byte[] ImageData;
        /// <summary>
        /// 本层图像宽度
        /// </summary>
        public int Width;
        /// <summary>
        /// 本层图像高度
        /// </summary>
        public int Height;
    }

    /// <summary>
    /// 图像层集合
    /// </summary>
    public class LayerImageCollection : ArrayList
    {
        /// <summary>
        /// 根据索引号，获取LayerImage对象
        /// </summary>
        /// <param name="index">索引号</param>
        /// <returns>LayerImage对象</returns>
        public LayerImage this[int index]
        {
            get
            {
                if (this == null || this.Count == 0)
                {
                    return null;
                }
                else
                {
                    return (LayerImage)base[index];
                }
            }
        }

        /// <summary>
        /// 集合中增加元素(线程安全), 必须调用base，否则会造成无限递归
        /// </summary>
        /// <param name="li">LayerImage元素</param>
        public void Add(LayerImage li)
        {
            lock (this.SyncRoot)
            {
                base.Add(li);
            }
        }

        /// <summary>
        /// 集合中删除元素(线程安全), 必须调用base，否则会造成无限递归
        /// </summary>
        /// <param name="ts"></param>
        public void Remove(LayerImage li)
        {
            lock (this.SyncRoot)
            {
                base.Remove(li);
            }
        }
    }

    /// <summary>
    /// 带值的点
    /// </summary>
    public class ValuePoint
    {
        /// <summary>
        /// 将ValuePointCollection按照val值从小到大排序
        /// </summary>
        /// <param name="vpCollection">ValuePointCollection</param>
        /// <returns></returns>
        public static ValuePointCollection SortValuePointCollection(ValuePointCollection vpCollection)
        {
            if (vpCollection == null || vpCollection.Count == 0)
            {
                return new ValuePointCollection();
            }

            int min;
            for (int i = 0; i < vpCollection.Count - 1; i++)
            {
                min = i;
                for (int j = i + 1; j < vpCollection.Count; j++)
                {
                    if (((ValuePoint)vpCollection[j]).Val < ((ValuePoint)vpCollection[min]).Val)
                    {
                        min = j;
                    }
                }
                ValuePoint t = (ValuePoint)vpCollection[min];
                vpCollection[min] = vpCollection[i];
                vpCollection[i] = t;
            }
            return vpCollection;
        }

        /// <summary>
        /// 点1
        /// </summary>
        public PointF Ptf1;
        /// <summary>
        /// 点2
        /// </summary>
        public PointF Ptf2;
        /// <summary>
        /// 值
        /// </summary>
        public float Val;
    }

    /// <summary>
    /// 带值点集合
    /// </summary>
    public class ValuePointCollection:ArrayList
    {
        /// <summary>
        /// 根据索引号，获取ValuePoint对象
        /// </summary>
        /// <param name="index">索引号</param>
        /// <returns>ValuePoint对象</returns>
        public ValuePoint this[int index]
        {
            get
            {
                if (this == null || this.Count == 0)
                {
                    return null;
                }
                else
                {
                    return (ValuePoint)base[index];
                }
            }
            set
            {
                if (value.GetType() != typeof(ValuePoint))
                {
                    throw new Exception("类型不匹配，不能赋值。");
                }
                base[index] = value;
            }
        }

        /// <summary>
        /// 集合中增加元素(线程安全), 必须调用base，否则会造成无限递归
        /// </summary>
        /// <param name="vp">ValuePoint元素</param>
        public void Add(ValuePoint vp)
        {
            if (vp.GetType() != typeof(ValuePoint))
            {
                throw new Exception("类型不匹配，不能加入集合。");
            }

            lock (this.SyncRoot)
            {
                base.Add(vp);
            }
        }

        /// <summary>
        /// 集合中删除元素(线程安全), 必须调用base，否则会造成无限递归
        /// </summary>
        /// <param name="vp"></param>
        public void Remove(ValuePoint vp)
        {
            lock (this.SyncRoot)
            {
                base.Remove(vp);
            }
        }

        /// <summary>
        /// 集合中删除指定索引位置的元素，必须调用base，否则会造成无限递归
        /// </summary>
        /// <param name="index"></param>
        public void RemoveAt(int index)
        {
            lock (this.SyncRoot)
            {
                base.RemoveAt(index);
            }
        }
    }
}
