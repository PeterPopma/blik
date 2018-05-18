using dshow.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using VideoSource;

namespace Blik
{
    public class Camera
    {
        private IVideoSource videoSource = null;
        private Bitmap lastFrame = null;
        private int framesPerSecond = 10;

        // image width and height
        private int width = -1, height = -1;
       
        public event EventHandler NewFrame;

        // LastFrame property
        public Bitmap LastFrame
        {
            get { return lastFrame; }
        }
        // Width property
        public int Width
        {
            get { return width; }
        }
        // Height property
        public int Height
        {
            get { return height; }
        }
        // FramesReceived property
        public int FramesReceived
        {
            get { return (videoSource == null) ? 0 : videoSource.FramesReceived; }
        }
        // BytesReceived property
        public int BytesReceived
        {
            get { return (videoSource == null) ? 0 : videoSource.BytesReceived; }
        }
        // Running property
        public bool Running
        {
            get { return (videoSource == null) ? false : videoSource.Running; }
        }

        public int FramesPerSecond
        {
            get
            {
                return framesPerSecond;
            }

            set
            {
                framesPerSecond = value;
            }
        }

        // Constructor
        public Camera(IVideoSource source)
        {
            this.videoSource = source;
            videoSource.NewFrame += new CameraEventHandler(video_NewFrame);
        }

        // Start video source
        public void Start()
        {
            if (videoSource != null)
            {
                videoSource.Start();
            }
        }

        // Siganl video source to stop
        public void SignalToStop()
        {
            if (videoSource != null)
            {
                videoSource.SignalToStop();
            }
        }

        // Wait video source for stop
        public void WaitForStop()
        {
            // lock
            Monitor.Enter(this);

            if (videoSource != null)
            {
                videoSource.WaitForStop();
            }
            // unlock
            Monitor.Exit(this);
        }

        // Abort camera
        public void Stop()
        {
            // lock
            Monitor.Enter(this);

            if (videoSource != null)
            {
                videoSource.Stop();
            }
            // unlock
            Monitor.Exit(this);
        }

        // Lock it
        public void Lock()
        {
            Monitor.Enter(this);
        }

        // Unlock it
        public void Unlock()
        {
            Monitor.Exit(this);
        }

        // On new frame
        private void video_NewFrame(object sender, CameraEventArgs e)
        {
            try
            {
                // lock
                Monitor.Enter(this);

                // dispose old frame
                if (lastFrame != null)
                {
                    lastFrame.Dispose();
                }

                lastFrame = (Bitmap)e.Bitmap.Clone();

                // image dimension
                width = lastFrame.Width;
                height = lastFrame.Height;
            }
            catch (Exception)
            {
            }
            finally
            {
                // unlock
                Monitor.Exit(this);
            }

            // notify client
            if (NewFrame != null)
                NewFrame(this, new EventArgs());
        }
        /*
        private void SetTimer()
        {
            // Create a timer with a two second interval.
            aTimer = new System.Timers.Timer(1000);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }
        
        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            takeSnapShot();
        }

        /// <summary>
        /// Get the image from the Still pin.  The returned image can turned into a bitmap with
        /// Bitmap b = new Bitmap(cam.Width, cam.Height, cam.Stride, PixelFormat.Format24bppRgb, m_ip);
        /// If the image is upside down, you can fix it with
        /// b.RotateFlip(RotateFlipType.RotateNoneFlipY);
        /// </summary>
        /// <returns>Returned pointer to be freed by caller with Marshal.FreeCoTaskMem</returns>
        public void takeSnapShot()
        {
            int hr;

            /// <summary> so we can wait for the async job to finish </summary>
            ManualResetEvent pictureReady = null;
            // get ready to wait for new image
            pictureReady.Reset();
            IntPtr ipBuffer = Marshal.AllocCoTaskMem(Math.Abs(stillWidth) * height);

            try
            {
                m_WantOne = true;

                // If we are using a still pin, ask for a picture
                if (m_VidControl != null)
                {
                    // Tell the camera to send an image
                    hr = m_VidControl.SetMode(m_pinStill, VideoControlFlags.Trigger);
                    DsError.ThrowExceptionForHR(hr);
                }

                // Start waiting
                if (!pictureReady.WaitOne(9000, false))
                {
                    throw new Exception("Timeout waiting to get picture");
                }
                // Got one
                lastFrame = new Bitmap(cam.Width, cam.Height, cam.Stride, PixelFormat.Format24bppRgb, m_ip);
            }
            finally
            {
                Marshal.FreeCoTaskMem(ipBuffer);
                ipBuffer = IntPtr.Zero;
            }
        }

        private void SaveSizeInfo(ISampleGrabber sampGrabber)
        {
            int hr;
            // Get the media type from the SampleGrabber
            AMMediaType media = new AMMediaType();
            hr = sampGrabber.GetConnectedMediaType(media);
            DsError.ThrowExceptionForHR(hr);
            if ((media.formatType != FormatType.VideoInfo) || (media.formatPtr == IntPtr.Zero))
            {
                throw new NotSupportedException("Unknown Grabber Media Format");
            }

            // Grab the size info
            VideoInfoHeader videoInfoHeader = (VideoInfoHeader)Marshal.PtrToStructure(media.formatPtr, typeof(VideoInfoHeader));
            m_videoWidth = videoInfoHeader.BmiHeader.Width;
            m_videoHeight = videoInfoHeader.BmiHeader.Height;
            m_stride = m_videoWidth * (videoInfoHeader.BmiHeader.BitCount / 8);
            DsUtils.FreeAMMediaType(media);
            media = null;
        }
        */
    }
}
