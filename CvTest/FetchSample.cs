using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections;

namespace CvTest
{
    public partial class FetchSample : Form
    {
        private Tld pri_tld = null;
        private Bitmap pri_bmp = null;
        private AForge.Controls.VideoSourcePlayer pri_player = null;
        /// <summary>
        /// 鼠标按下点，作为选择框的起始点
        /// </summary>
        private System.Drawing.Point pri_startpoint = System.Drawing.Point.Empty;
        /// <summary>
        /// 指示鼠标是否被按下
        /// </summary>
        private bool pri_buttondown = false;
        /// <summary>
        /// 选择框区域
        /// </summary>
        private Rectangle pri_choose_rect = Rectangle.Empty;

        public FetchSample(Tld tld, Bitmap mapsource, AForge.Controls.VideoSourcePlayer vsPlayer)
        {
            pri_tld = tld;
            pri_bmp = mapsource;
            pri_player = vsPlayer;

            InitializeComponent();

            // 设置双缓存绘图的属性
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        private void FetchSample_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                pri_choose_rect = Rectangle.Empty;
                this.Refresh();
                
                pri_startpoint = new Point(e.X, e.Y);
                pri_buttondown = true;
            }
        }

        private void FetchSample_MouseMove(object sender, MouseEventArgs e)
        {
            if (pri_buttondown)
            {
                pri_choose_rect = new Rectangle(pri_startpoint.X, pri_startpoint.Y, e.X - pri_startpoint.X, e.Y - pri_startpoint.Y);
                this.Refresh();
            }
        }

        private void FetchSample_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                pri_buttondown = false;
            }
        }

        private void FetchSample_Paint(object sender, PaintEventArgs e)
        {
            // 双缓存绘图
            BufferedGraphicsContext currentContext = BufferedGraphicsManager.Current;
            BufferedGraphics myBuffer = currentContext.Allocate(e.Graphics, e.ClipRectangle);
            Graphics g = myBuffer.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            g.Clear(this.BackColor);
            //使用g进行绘图

            if (pri_bmp != null)
            {
                g.DrawImage(pri_bmp, 0, 0, pri_bmp.Width, pri_bmp.Height);
            }

            if (pri_choose_rect != null)
            {
                Pen p = new Pen(Color.Yellow);
                g.DrawRectangle(p, pri_choose_rect);
            }

            myBuffer.Render(e.Graphics);
            g.Dispose();
            myBuffer.Dispose();//释放资源
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            Bitmap patch = null;

            if (pri_tld != null && pri_bmp != null && pri_choose_rect != Rectangle.Empty)
            {
                for (double lrshift = (-1) * Parameter.SHIFT_BORDER; lrshift < Parameter.SHIFT_BORDER + Parameter.SHIFT_INTERVAL; lrshift += Parameter.SHIFT_INTERVAL)
                {
                    for (double tbshift = (-1) * Parameter.SHIFT_BORDER; tbshift < Parameter.SHIFT_BORDER + Parameter.SHIFT_INTERVAL; tbshift += Parameter.SHIFT_INTERVAL)
                    {
                        if (pri_choose_rect.X + lrshift >= 0 && pri_choose_rect.X + pri_choose_rect.Width - 1 + lrshift < pri_bmp.Width - 1 &&
                            pri_choose_rect.Y + tbshift >= 0 && pri_choose_rect.Y + pri_choose_rect.Height - 1 + tbshift < pri_bmp.Height - 1)
                        {
                            patch = ImgOper.CutImage(pri_bmp, (int)(pri_choose_rect.X + lrshift), (int)(pri_choose_rect.Y + tbshift),
                                (int)pri_choose_rect.Width, (int)pri_choose_rect.Height);
                            pri_tld.TrainPositive(patch);
                            //patch.Save("Image\\VideoSave\\" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".jpg");
                        }
                    }
                }

                for (int row = 0; row < pri_bmp.Height - Parameter.DETECT_WINDOW_SIZE.Height + 1; row += Parameter.DETECT_WINDOW_SIZE.Height)
                {
                    for (int col = 0; col < pri_bmp.Width - Parameter.DETECT_WINDOW_SIZE.Width + 1; col += Parameter.DETECT_WINDOW_SIZE.Width)
                    {
                        Rectangle rect = new Rectangle(col, row, pri_choose_rect.Width, pri_choose_rect.Height);
                        double areaportion = pri_tld.AreaProportion(rect, pri_choose_rect);
                        if (areaportion < Parameter.AREA_INTERSECT_PROPORTION)
                        {
                            patch = ImgOper.CutImage(pri_bmp, rect.X, rect.Y, rect.Width, rect.Height);
                            pri_tld.TrainNegative(patch);
                        }
                    }
                }
            }
            pri_player.Start();
            this.Close();
        }
    }
}
