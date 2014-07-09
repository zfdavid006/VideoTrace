using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.IO;
using AForge.Imaging;
using AForge;
using System.Collections;
using System.Threading;
using System.Windows.Forms;

namespace CvTest
{
    public partial class Form1 : Form
    {
        private string POS_DIR = "Image\\pos";
        private string NEG_DIR = "Image\\neg";
        private string BMPFILE = "Image\\m.jpg";

        private Bitmap pri_bmp = null;
        private RectangleCollection pri_obj_regions = null;

        private Bitmap pri_bmp_c = null;
        private RectangleCollection pri_obj_regions_c = null;

        private OpticalFlow pri_optical = null;
        /// <summary>
        /// 跟踪模块中，把小数位置舍入成整数会导致跟踪不精确，表现为跟踪模块
        /// 逐渐往左上角滑动，所以跟踪区域必须用RectangleF
        /// </summary>
        private RectangleF pri_tracker_rect = RectangleF.Empty;
        private RectangleF pri_obj_rect = RectangleF.Empty;

        public delegate void RefreshForm();

        private Tld pri_tld = null;

        #region private method

        /// <summary>
        /// 刷新，触发OnPaint事件
        /// </summary>
        private void RefreshView()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new RefreshForm(RefreshView));
            }
            else
            {
                this.Refresh();
            }
        }

        /// <summary>
        /// Hog训练
        /// </summary>
        /// <param name="posdir">正样本文件夹路径</param>
        /// <param name="negdir">负样本文件夹路径</param>
        private void HogTrain(string posdir, string negdir)
        {
            Bitmap bmp = null;
            // 训练负样本
            string[] files = Directory.GetFiles(negdir);
            foreach (string file in files)
            {
                bmp = AForge.Imaging.Image.FromFile(file);
                pri_tld.TrainNegative(bmp);
            }

            // 训练正样本
            files = Directory.GetFiles(posdir);
            foreach (string file in files)
            {
                bmp = AForge.Imaging.Image.FromFile(file);
                pri_tld.TrainPositive(bmp);  
            }
        }

        #endregion

        public Form1()
        {
            InitializeComponent();

            pri_tld = new Tld();

            double elapse = 0;
            DateTime dt;
            
            #region 显示Hog示意图
            //HogGram hogGram;
            //NormBlockVectorGram blockGram;
            //pri_bmp = AForge.Imaging.Image.FromFile("Image\\peop.jpg");
            //pri_bmp = ImgOper.ResizeImage(pri_bmp, 486, 500);
            //pri_bmp = ImgOper.Grayscale(pri_bmp);

            //dt = DateTime.Now;
            //hogGram = HogGram.GetHogFromBitmap(pri_bmp, CELL_SIZE.Width, CELL_SIZE.Height, PART_NUMBER);
            //blockGram = new NormBlockVectorGram(hogGram, BLOCK_SIZE.Width, BLOCK_SIZE.Height);
            //elapse = DateTime.Now.Subtract(dt).TotalMilliseconds;

            //pri_bmp = ImgOper.DrawHogGram(hogGram, 486 * 2, 500 * 2);
            //this.Refresh();
            #endregion

            //// 训练样本
            //HogTrain(POS_DIR, NEG_DIR);

            //dt = DateTime.Now;
            //// Hog检测
            //pri_bmp = AForge.Imaging.Image.FromFile(BMPFILE);
            //pri_bmp = ImgOper.ResizeImage(pri_bmp, pri_bmp.Width, pri_bmp.Height);
            //pri_obj_regions = pri_tld.HogDetect(pri_bmp);
            //elapse = DateTime.Now.Subtract(dt).TotalMilliseconds;

            //dt = DateTime.Now;
            //// 效果没有明显的提升，有待进一步检验
            //Rectangle r = pri_tld.MostAssociateObject(pri_obj_regions, pri_bmp);
            //pri_obj_regions = new RectangleCollection();
            //pri_obj_regions.Add(r);
            //elapse = DateTime.Now.Subtract(dt).TotalMilliseconds;
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            Pen p = null;

            if (pri_bmp != null)
            {
                e.Graphics.DrawImage(pri_bmp, 0, 0, pri_bmp.Width, pri_bmp.Height);
                if (pri_obj_regions != null && pri_obj_regions.Count > 0)
                {
                    p = new Pen(Color.Red);
                    foreach (Rectangle obj in pri_obj_regions)
                        e.Graphics.DrawRectangle(p, obj.X, obj.Y, obj.Width, obj.Height);
                }
            }

            if (pri_bmp_c != null)
            {
                if (pri_bmp != null)
                    e.Graphics.DrawImage(pri_bmp_c, pri_bmp.Width, 0, pri_bmp_c.Width, pri_bmp_c.Height);
                else
                    e.Graphics.DrawImage(pri_bmp_c, 0, 0, pri_bmp_c.Width, pri_bmp_c.Height);
                if (pri_obj_regions_c != null && pri_obj_regions_c.Count > 0)
                {
                    p = new Pen(Color.Red);
                    foreach (Rectangle obj in pri_obj_regions_c)
                    {
                        if (pri_bmp != null)
                            e.Graphics.DrawRectangle(p, obj.X + pri_bmp.Width, obj.Y, obj.Width, obj.Height);
                        else
                            e.Graphics.DrawRectangle(p, obj.X, obj.Y, obj.Width, obj.Height);
                    }
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // 初始化时wx和wy只要设成1，用来寻找初始点足够了
            pri_optical = new OpticalFlow(2, 1, 1, 10, 0.05, 0.1f, 3, 5);

            VideoOpp vo = new VideoOpp();
            videoSourcePlayer1.VideoSource = vo.videoSource;
            videoSourcePlayer1.Start();
        }

        private void videoSourcePlayer1_NewFrame(object sender, ref Bitmap image)
        {
            Bitmap nowImg = AForge.Imaging.Image.Clone(image);
            nowImg = ImgOper.Grayscale(nowImg);

            // 做高斯模糊，取样和检测都会影响到
            nowImg = ImgOper.GaussianConvolution(nowImg, Parameter.GAUSSIAN_SIGMA, Parameter.GAUSSIAN_SIZE);

            // 将图像传递到取样窗口
            pri_bmp = AForge.Imaging.Image.Clone(nowImg);

            double elapse = 0;
            DateTime dt = DateTime.Now;
            pri_obj_regions = pri_tld.HogDetect(nowImg);
            elapse = DateTime.Now.Subtract(dt).TotalMilliseconds;

            float[] vect = new float[3];   // 描述区域位移和缩放
            //nowImg.Save("Image\\VideoSave\\" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".jpg");
            ArrayList points = null;
            if (pri_optical.IPoints == null || pri_tracker_rect == Rectangle.Empty)
            {
                pri_optical.I = pri_optical.TransformBmpToLayerImg(nowImg);

                // 尚未确定跟踪对象，不需要PositiveExpert
                pri_tracker_rect = pri_tld.NegativeExpert(pri_obj_regions, pri_tracker_rect, nowImg);

                pri_optical.IPoints = new ArrayList();
                points = pri_optical.ChooseRectRandomPoints(pri_tracker_rect, Parameter.INITIAL_POINTS_NUMBER);
                foreach (PointF pt in points)
                    pri_optical.IPoints.Add(new PointF(pt.X, pt.Y));
            }
            else
            {
                if (pri_optical.J != null)
                {
                    pri_optical.I = pri_optical.J;
                    pri_optical.IPoints = pri_optical.JPoints;
                }
                pri_optical.J = pri_optical.TransformBmpToLayerImg(nowImg);

                //pri_optical.JPoints = new ArrayList();
                // 跟踪时wx和wy设为3，或者更大
                dt = DateTime.Now;
                vect = pri_optical.ComputerDisplacement(pri_optical.I, pri_optical.J, pri_optical.IPoints,
                    1, 3, 3, 20, 0.2f, ref pri_optical.IPoints, ref pri_optical.JPoints);
                elapse = DateTime.Now.Subtract(dt).TotalMilliseconds;

                if (vect != null)
                {
                    pri_tracker_rect.X = pri_tracker_rect.X + vect[0];
                    pri_tracker_rect.Y = pri_tracker_rect.Y + vect[1];
                    pri_tracker_rect.Width = pri_tracker_rect.Width * vect[2];
                    pri_tracker_rect.Height = pri_tracker_rect.Height * vect[2];

                    if (pri_tracker_rect.X < 0)
                    {
                        pri_tracker_rect.Width = pri_tracker_rect.Width + pri_tracker_rect.X;
                        pri_tracker_rect.X = 0;
                    }
                    else if (pri_tracker_rect.X + pri_tracker_rect.Width - 1 > nowImg.Width - 1)
                    {
                        pri_tracker_rect.Width = nowImg.Width - pri_tracker_rect.X;
                    }

                    if (pri_tracker_rect.Y < 0)
                    {
                        pri_tracker_rect.Height = pri_tracker_rect.Height + pri_tracker_rect.Y;
                        pri_tracker_rect.Y = 0;
                    }
                    else if (pri_tracker_rect.Y + pri_tracker_rect.Height - 1 > nowImg.Height - 1)
                    {
                        pri_tracker_rect.Height = nowImg.Height - pri_tracker_rect.Y;
                    }
                }
                else
                {
                    pri_tracker_rect = Rectangle.Empty;
                }

                // 在跟踪框被NExpert产生的最可信对象替代前保存原始状态
                if (pri_tracker_rect != Rectangle.Empty)
                {
                    pri_obj_rect = new RectangleF(pri_tracker_rect.X, pri_tracker_rect.Y, pri_tracker_rect.Width, pri_tracker_rect.Height);
                }

                dt = DateTime.Now;
                // 有跟踪对象时，PositvieExpert与NegativeExpert都开始工作
                pri_tld.PositiveExpert(pri_obj_regions, pri_tracker_rect, nowImg);
                elapse = DateTime.Now.Subtract(dt).TotalMilliseconds;

                dt = DateTime.Now;
                pri_tracker_rect = pri_tld.NegativeExpert(pri_obj_regions, pri_tracker_rect, nowImg);
                elapse = DateTime.Now.Subtract(dt).TotalMilliseconds;

                pri_optical.JPoints = new ArrayList();
                points = pri_optical.ChooseRectRandomPoints(pri_tracker_rect, Parameter.INITIAL_POINTS_NUMBER);
                // 如果pri_tracker_rect为空，则pri_optical.JPoints为空，那么下次跟踪的pri_optical.Ipoints也为空，无法继续跟踪
                foreach (PointF pt in points)
                    pri_optical.JPoints.Add(new PointF(pt.X, pt.Y));
            }

            Graphics g = Graphics.FromImage(image);
            Pen pen = new Pen(Color.Red);

            if (pri_tracker_rect != Rectangle.Empty)
            {
                g.DrawRectangle(pen, pri_tracker_rect.X, pri_tracker_rect.Y, pri_tracker_rect.Width, pri_tracker_rect.Height);

                FontFamily f = new FontFamily("宋体");
                Font font = new System.Drawing.Font(f, 12);
                SolidBrush myBrush = new SolidBrush(Color.Blue);

                StringBuilder str = new StringBuilder();
                if (vect != null)
                {
                    str.AppendFormat("跟踪框相对位移：({0}, {1})，缩放：{2} \r\n" +
                        "跟踪产生的Rectangle：({3}, {4}, {5}, {6}) \r\n" +
                        "最可信的Rectangle：({7}, {8}, {9}, {10})", vect[0], vect[1], vect[2],
                        pri_obj_rect.X, pri_obj_rect.Y, pri_obj_rect.Width, pri_obj_rect.Height,
                        pri_tracker_rect.X, pri_tracker_rect.Y, pri_tracker_rect.Width, pri_tracker_rect.Height);
                }
                else
                {
                    str.AppendFormat("最可信的Rectangle：({0}, {1}, {2}, {3})",  pri_tracker_rect.X, pri_tracker_rect.Y, pri_tracker_rect.Width, pri_tracker_rect.Height);
                }

                g.DrawString(str.ToString(),  font, myBrush, 0, 0);
            }

            //if (pri_obj_regions != null && pri_obj_regions.Count > 0)
            //{
            //    foreach (Rectangle rect in pri_obj_regions)
            //    {
            //        g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            //    }
            //}
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            videoSourcePlayer1.SignalToStop();
        }

        public Bitmap bmp { get; set; }

        private void btnSample_Click(object sender, EventArgs e)
        {
            videoSourcePlayer1.SignalToStop();
            FetchSample fs = new FetchSample(pri_tld, pri_bmp, videoSourcePlayer1);
            fs.Show();
        }
    }
}
