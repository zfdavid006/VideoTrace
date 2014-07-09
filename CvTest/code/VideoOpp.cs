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
using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Video.VFW;

namespace CvTest
{
    public class VideoOpp
    {
        private FilterInfoCollection videoDevices;
        public VideoCaptureDevice videoSource;

        public VideoOpp()
        {
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            //if (videoDevices.Count == 0)
            //    throw new ApplicationException();

            if (videoDevices.Count > 0)
            {
                videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
            }
        }
    }
}
