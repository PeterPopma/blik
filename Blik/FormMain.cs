using dshow;
using dshow.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using Tiger.Video.VFW;
using VideoSource;

namespace Blik
{
    public partial class FormMain : Form
    {
        private Camera camera = null;
        private static Object locker = new Object();

        private const int NUM_OBJECTS = 100;
        bool mouseDown = false;
        bool isPicking = false;
        bool snapshotOnTrigger = false;
        Point mouseDownPoint = Point.Empty;
        Point mousePoint = Point.Empty;
        System.Timers.Timer statisticsTimer;
        System.Timers.Timer analysisTimer;
        System.Timers.Timer sirenTimer;

        int currentObjectNumber = 1;
        DetectionObject currentObject = null;
        DetectionSystem detectionSystem = new DetectionSystem();

        SoundPlayer soundPlayer = new SoundPlayer();
        bool interfaceCardConnected;

        [DllImport("k8055d_x64.dll")]
        public static extern int OpenDevice(int CardAddress);

        [DllImport("k8055d_x64.dll")]
        public static extern void CloseDevice();

        [DllImport("k8055d_x64.dll")]
        public static extern void ClearDigitalChannel(int Channel);

        [DllImport("k8055d_x64.dll")]
        public static extern void ClearAllDigital();

        [DllImport("k8055d_x64.dll")]
        public static extern void SetDigitalChannel(int Channel);

        public Camera Camera
        {
            get { return camera; }
            set
            {
                // lock
                Monitor.Enter(this);

                // detach event
                if (camera != null)
                {
                    camera.NewFrame -= new EventHandler(camera_NewFrame);
                }

                camera = value;

                // atach event
                if (camera != null)
                {
                    camera.NewFrame += new EventHandler(camera_NewFrame);
                }

                // unlock
                Monitor.Exit(this);
            }
        }

        public FormMain()
        {
            soundPlayer.Stream = Resources.alarm;
            InitializeComponent();
            setCaptureDevice();
            loadReferenceFrames();
            loadObjects();
            SetStatisticsTimer();
            SetAnalysisTimer();
            SetSirenTimer();
            detectionSystem.MakeAnalysisImage = checkBoxMakeAnalysisImage.Checked;
            connectK8055();
        }

        private void connectK8055()
        {
            int CardAddr = 0;
            int h = OpenDevice(CardAddr);
            if(h==-1)
            {
                Console.WriteLine("USB interface card not found!");
                interfaceCardConnected = false;
            }
            else
            {
                interfaceCardConnected = true;
                ClearAllDigital();
            }
        }

        private void loadObjects()
        {
            RegistryKey keyObjects = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Blik\\Objects");
            if (keyObjects != null)
            {
                int counter = 1;
                RegistryKey keyObject = keyObjects.OpenSubKey("Object1");
                while (keyObject != null)
                {
                    int x = Convert.ToInt32(keyObject.GetValue("x"));
                    int y = Convert.ToInt32(keyObject.GetValue("y"));
                    int width = Convert.ToInt32(keyObject.GetValue("width"));
                    int height = Convert.ToInt32(keyObject.GetValue("height"));
                    DetectionObject newObject = new DetectionObject(x, y, width, height);
                    detectionSystem.DetectionObjects.Add(newObject);
                    keyObject = keyObjects.OpenSubKey("Object"+(++counter).ToString());
                } 
            }

            labelNumObjects.Text = detectionSystem.DetectionObjects.Count.ToString();
            if (detectionSystem.DetectionObjects.Count > 0)
            {
                currentObject = detectionSystem.DetectionObjects.ElementAtOrDefault(currentObjectNumber-1);
                DisplayObjectDetails();
            }
        }

        private void saveObjects()
        {
            // Create or get existing subkey
            RegistryKey key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Blik");
            RegistryKey keyObjects = key.CreateSubKey("Objects");
            int counter = 1;

            // Delete old entries
            try
            {
                RegistryKey keyObject = keyObjects.OpenSubKey("Object1", true);
                while (keyObject != null)
                {
                    keyObjects.DeleteSubKey("Object"+counter.ToString());
                    counter++;
                    keyObject = keyObjects.OpenSubKey("Object"+counter.ToString(), true);
                }
            } catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            counter = 1;
            foreach(DetectionObject obj in detectionSystem.DetectionObjects)
            {
                RegistryKey keycurrentObject = keyObjects.CreateSubKey("Object" + counter);
                keycurrentObject.SetValue("x", obj.Rectangle.X);
                keycurrentObject.SetValue("y", obj.Rectangle.Y);
                keycurrentObject.SetValue("width", obj.Rectangle.Width);
                keycurrentObject.SetValue("height", obj.Rectangle.Height);
                counter++;
            }
        }


        private void setCaptureDevice()
        {
            // Set capture device
            try
            {
                FilterCollection filters = new FilterCollection(FilterCategory.VideoInputDevice);
                string device = filters[0].MonikerString; 

                if (filters.Count == 0)
                {
                    throw new ApplicationException();
                }

                // create video source
                CaptureDevice localSource = new CaptureDevice();
                localSource.VideoSource = device;

                // open it
                OpenVideoSource(localSource);
            }
            catch (ApplicationException e)
            {
                MessageBox.Show("No video source available!");
                Console.WriteLine(e.Message);
            }

        }

        private DetectionObject AddObject(int x1, int y1, int x2, int y2)
        {
            DetectionObject newObject = new DetectionObject(x1, y1, x2 - x1, y2 - y1);
            detectionSystem.DetectionObjects.Add(newObject);

            return newObject;
        }

        private Rectangle getRectToScaled(Rectangle rect)
        {
            int x = (int)(rect.X * (float)pictureBoxCamera.Width / camera.Width);
            int y = (int)(rect.Y * (float)pictureBoxCamera.Height / camera.Height);
            int width = (int)(rect.Width * (float)pictureBoxCamera.Width / camera.Width);
            int height = (int)(rect.Height * (float)pictureBoxCamera.Height / camera.Height);

            return new Rectangle(x, y, width, height);
        }

        private Rectangle getScaledToRect(Rectangle scaledRect)
        {
            int x = (int)(scaledRect.X * (float)camera.Width / pictureBoxCamera.Width);
            int y = (int)(scaledRect.Y * (float)camera.Height / pictureBoxCamera.Height);
            int width = (int)(scaledRect.Width * (float)camera.Width / pictureBoxCamera.Width);
            int height = (int)(scaledRect.Height * (float)camera.Height / pictureBoxCamera.Height);

            return new Rectangle(x, y, width, height);
        }

        private void pictureBoxReference_Paint(object sender, PaintEventArgs e)
        {
            base.OnPaint(e);
            Pen redPen = new Pen(Color.Red, 2);
            if (isPicking && mouseDown)
            {
                Rectangle window = new Rectangle(
                    Math.Min(mouseDownPoint.X, mousePoint.X),
                    Math.Min(mouseDownPoint.Y, mousePoint.Y),
                    Math.Abs(mouseDownPoint.X - mousePoint.X),
                    Math.Abs(mouseDownPoint.Y - mousePoint.Y));
                e.Graphics.DrawRectangle(redPen, window);
            } else if (currentObject!=null && camera!=null)
            {
                e.Graphics.DrawRectangle(redPen, getRectToScaled(currentObject.Rectangle));
            }
        }

        private void pictureBoxReference_MouseMove(object sender, MouseEventArgs e)
        {
            mousePoint = e.Location;
            pictureBoxReference.Invalidate();
        }

        private void DisplayObjectDetails()
        {
            labelCurrentObject.Text = currentObjectNumber.ToString();

            if (currentObject != null)
            {
                textBoxObject_X1.Text = currentObject.Rectangle.Left.ToString();
                textBoxObject_Y1.Text = currentObject.Rectangle.Top.ToString();
                textBoxObject_X2.Text = currentObject.Rectangle.Right.ToString();
                textBoxObject_Y2.Text = currentObject.Rectangle.Bottom.ToString();
            }
            pictureBoxReference.Invalidate();
            pictureBoxCamera.Invalidate();
        }

        private void pictureBoxCamera_MouseDown(object sender, MouseEventArgs e)
        {
            mouseDown = true;
            mousePoint = mouseDownPoint = e.Location;
        }

        private void pictureBoxCamera_MouseMove(object sender, MouseEventArgs e)
        {
            mousePoint = e.Location;
            pictureBoxCamera.Invalidate();
        }

        private void pictureBoxCamera_MouseUp(object sender, MouseEventArgs e)
        {
            mouseUp();
        }

        private void pictureBoxReference_MouseUp(object sender, MouseEventArgs e)
        {
            mouseUp();
        }

        private void mouseUp()
        {
            mouseDown = false;
            if (isPicking)
            {
                currentObject = AddObject(Math.Min(mouseDownPoint.X, mousePoint.X),
                        Math.Min(mouseDownPoint.Y, mousePoint.Y),
                        Math.Max(mouseDownPoint.X, mousePoint.X),
                        Math.Max(mouseDownPoint.Y, mousePoint.Y));
                currentObjectNumber = detectionSystem.DetectionObjects.Count;
                labelNumObjects.Text = detectionSystem.DetectionObjects.Count.ToString();
                isPicking = false;
                Cursor = Cursors.Default;
                DisplayObjectDetails();
            }
        }

        private void pictureBoxReference_MouseDown(object sender, MouseEventArgs e)
        {
            mouseDown = true;
            mousePoint = mouseDownPoint = e.Location;
        }

        private void OnStatisticsTimerEvent(Object source, ElapsedEventArgs e)
        {
            if (camera != null && camera.LastFrame!=null)
            {
                textBoxObjectStatusInfo.Text = detectionSystem.displayObjectStatusInfo();
                textBoxObjectAnalysisInfo.Text = detectionSystem.displayObjectAnalysisInfo();
                textBoxDetectionStatistics.Text = detectionSystem.displayStatistics();
                if(detectionSystem.MakeAnalysisImage)
                {
                    pictureBoxAnalysis.Image = detectionSystem.BitmapAnalysis;
                    pictureBoxAnalysis.Invalidate();
                }
            }
        }

        private void OnSirenTimerEvent(Object source, ElapsedEventArgs e)
        {
            ClearDigitalChannel(1);
            sirenTimer.Enabled = false;
        }

        private void OnAnalysisTimerEvent(Object source, ElapsedEventArgs e)
        {
            if (camera != null && camera.LastFrame != null)
            {
                // Uncomment if image should be changed
                lock (locker)
                {
                    try
                    {
                        Bitmap bitmap = new Bitmap(camera.LastFrame);
                        detectionSystem.analyze(bitmap);
                        bitmap.Dispose();
                        bitmap = null;
                    }
                    catch (Exception ex)
                    {
                        if (ex is ArgumentException || ex is SystemException)
                        {
                            //handle it
                            Console.WriteLine(ex.Message);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }

            foreach(DetectionObject obj in detectionSystem.DetectionObjects)
            {
                if (obj.Active)
                {
                    if (obj.Trigger)
                    {
                        if (interfaceCardConnected)
                        {
                            SetDigitalChannel(1);
                            sirenTimer.Enabled = true;
                        }
                        obj.Active = false;
                        soundPlayer.Play();
                        if( snapshotOnTrigger )
                        {
                            Bitmap bmp = new Bitmap(camera.LastFrame);
                            if (File.Exists("TriggerSnapshot.png"))
                            {
                                File.Delete("TriggerSnapshot.png");
                            }
                            bmp.Save("TriggerSnapshot.png");
                            bmp.Dispose();
                            bmp = null;
                        }
                    }
                }
            }
        }

        private void SetStatisticsTimer()
        {
            statisticsTimer = new System.Timers.Timer(1000);
            statisticsTimer.SynchronizingObject = this;
            statisticsTimer.Elapsed += OnStatisticsTimerEvent;
            statisticsTimer.AutoReset = true;
            statisticsTimer.Enabled = true;
        }

        private void SetAnalysisTimer()
        {
            analysisTimer = new System.Timers.Timer(1000 / (int)numericUpDownAnalysisFPS.Value);
            analysisTimer.SynchronizingObject = this;
            analysisTimer.Elapsed += OnAnalysisTimerEvent;
            analysisTimer.AutoReset = true;
            analysisTimer.Enabled = true;
        }

        private void SetSirenTimer()
        {
            sirenTimer = new System.Timers.Timer(4000);
            sirenTimer.SynchronizingObject = this;
            sirenTimer.Elapsed += OnSirenTimerEvent;
            sirenTimer.AutoReset = true;
            sirenTimer.Enabled = false;
        }

        private void buttonGo_Click(object sender, EventArgs e)
        {
            foreach(DetectionObject obj in detectionSystem.DetectionObjects)
            {
                obj.Active = true;
            }
        }

        private void buttonAddObject_Click(object sender, EventArgs e)
        {
            if (isPicking)
            {
                isPicking = false;
                Cursor = Cursors.Default;
            }
            else
            {
                isPicking = true;
                Cursor = Cursors.Hand;
            }
        }

        private void buttonPrevious_Click(object sender, EventArgs e)
        {
            if (currentObjectNumber > 1)
            {
                currentObjectNumber--;
                currentObject = detectionSystem.DetectionObjects.ElementAtOrDefault(currentObjectNumber-1);
                DisplayObjectDetails();
            }
        }

        private void buttonNext_Click(object sender, EventArgs e)
        {
            if (currentObjectNumber < detectionSystem.DetectionObjects.Count)
            {
                currentObjectNumber++;
                currentObject = detectionSystem.DetectionObjects.ElementAtOrDefault(currentObjectNumber-1);
                DisplayObjectDetails();
            }
        }

        private void buttonUpdate_Click(object sender, EventArgs e)
        {
            currentObject.Rectangle = new Rectangle(Convert.ToInt32(textBoxObject_X1.Text), 
                Convert.ToInt32(textBoxObject_Y1.Text), 
                Convert.ToInt32(textBoxObject_X2.Text)-Convert.ToInt32(textBoxObject_X1.Text), 
                Convert.ToInt32(textBoxObject_Y2.Text)-Convert.ToInt32(textBoxObject_Y1.Text));

            pictureBoxReference.Invalidate();
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            if (detectionSystem.DetectionObjects.Count > 0)
            {
                detectionSystem.DetectionObjects.RemoveAt(currentObjectNumber-1);
                if (currentObjectNumber>detectionSystem.DetectionObjects.Count)
                {
                    currentObjectNumber--;
                    currentObject = detectionSystem.DetectionObjects.ElementAtOrDefault(currentObjectNumber - 1);
                }
                labelNumObjects.Text = detectionSystem.DetectionObjects.Count.ToString();
                DisplayObjectDetails();
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void openLocalDeviceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FormCaptureDevice form = new FormCaptureDevice();

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                // create video source
                CaptureDevice localSource = new CaptureDevice();
                localSource.VideoSource = form.Device;

                // open it
                OpenVideoSource(localSource);
            }
        }

        // Close current file
        private void CloseFile()
        {
            if (camera != null)
            {
                // signal camera to stop
                camera.SignalToStop();
                // wait for the camera
                camera.WaitForStop();

                camera = null;
            }
        }

        // On new frame
        private void camera_NewFrame(object sender, System.EventArgs e)
        {
           pictureBoxCamera.Invalidate();
        }
        
        // Open video source
        private void OpenVideoSource(IVideoSource source)
        {
            // set busy cursor
            this.Cursor = Cursors.WaitCursor;

            // close previous file
            CloseFile();

            // create camera
            camera = new Camera(source);
            // start camera
            camera.Start();

            // set event handlers
            camera.NewFrame += new EventHandler(camera_NewFrame);

            this.Cursor = Cursors.Default;
        }

        private void buttonReference_Click(object sender, EventArgs e)
        {
            try {
                // only valid images
                if (camera.LastFrame.RawFormat.Guid.ToString().Length > 0)
                {
                    if (radioButtonReferenceNormal.Checked)
                    {
                        lock(locker)
                        {
                            detectionSystem.BitmapNormal = new Bitmap(camera.LastFrame);
                        }
                        pictureBoxReference.Image = detectionSystem.BitmapNormal;
                        pictureBoxReference.Invalidate();
                    }
                    if (radioButtonReferenceTrigger.Checked)
                    {
                        lock (locker)
                        {
                            detectionSystem.BitmapTrigger = new Bitmap(camera.LastFrame);
                        }
                        pictureBoxReference.Image = detectionSystem.BitmapTrigger;
                        pictureBoxReference.Invalidate();
                    }
                }
            } catch(Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void radioButtonReferenceNormal_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (radioButtonReferenceNormal.Checked)
                {
                    if (detectionSystem.BitmapNormal != null)
                    {
                        pictureBoxReference.Image = detectionSystem.BitmapNormal;
                        pictureBoxReference.Invalidate();
                    }
                }
            } catch(ArgumentException aex)
            {
                Console.WriteLine(aex.StackTrace);
            }
        }

        private void radioButtonReferenceTrigger_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (radioButtonReferenceTrigger.Checked)
                {
                    if (detectionSystem.BitmapTrigger != null)
                    {
                        pictureBoxReference.Image = detectionSystem.BitmapTrigger;
                        pictureBoxReference.Invalidate();
                    }
                }
            } catch(ArgumentException aex)
            {
                Console.WriteLine(aex.StackTrace);
            }
}

        private void buttonSaveReferenceFrames_Click(object sender, EventArgs e)
        {
            try {
                lock(locker)
                {
                    if (detectionSystem.BitmapNormal != null)
                    {
                        Bitmap bmp = new Bitmap(detectionSystem.BitmapNormal);
                        if (File.Exists("Normal.png"))
                        {
                            File.Delete("Normal.png");
                        }
                        bmp.Save("Normal.png");
                        bmp.Dispose();
                        bmp = null;
                    }
                    if (detectionSystem.BitmapTrigger != null)
                    {
                        Bitmap bmp = new Bitmap(detectionSystem.BitmapTrigger);
                        if (File.Exists("Trigger.png"))
                        {
                            File.Delete("Trigger.png");
                        }
                        bmp.Save("Trigger.png");
                        bmp.Dispose();
                        bmp = null;
                    }
                }
            } catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void buttonLoadReferenceFrames_Click(object sender, EventArgs e)
        {
            loadReferenceFrames();
        }

        private void loadReferenceFrames()
        {
            try {
                using (var bmpTemp = new Bitmap("Normal.png"))
                {
                    detectionSystem.BitmapNormal = new Bitmap(bmpTemp);
                }
                if (radioButtonReferenceNormal.Checked)
                {
                    pictureBoxReference.Image = detectionSystem.BitmapNormal;
                    pictureBoxReference.Invalidate();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            try
            {
                using (var bmpTemp = new Bitmap("Trigger.png"))
                {
                    detectionSystem.BitmapTrigger = new Bitmap(bmpTemp);
                }
                if (radioButtonReferenceTrigger.Checked)
                {
                    pictureBoxReference.Image = detectionSystem.BitmapTrigger;
                    pictureBoxReference.Invalidate();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            saveObjects();
            CloseDevice();      // Close USB interface card
        }

        private void checkBoxMakeAnalysisImage_CheckedChanged(object sender, EventArgs e)
        {
            detectionSystem.MakeAnalysisImage = checkBoxMakeAnalysisImage.Checked;
        }

        private void pictureBoxCamera_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle rc = pictureBoxCamera.ClientRectangle;

            if (camera != null)
            {
                try
                {
                    camera.Lock();

                    // draw frame
                    if (camera.LastFrame != null)
                    {
                        g.DrawImage(camera.LastFrame, rc.X, rc.Y, rc.Width - 1, rc.Height - 1);
                    }
                    else
                    {
                        // Create font and brush
                        Font drawFont = new Font("Arial", 12);
                        SolidBrush drawBrush = new SolidBrush(Color.White);

                        g.DrawString("Connecting ...", drawFont, drawBrush, new PointF(5, 5));

                        drawBrush.Dispose();
                        drawFont.Dispose();
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    camera.Unlock();
                }

                Pen redPen = new Pen(Color.Red, 2);
                if (isPicking && mouseDown)
                {
                    Rectangle window = new Rectangle(
                        Math.Min(mouseDownPoint.X, mousePoint.X),
                        Math.Min(mouseDownPoint.Y, mousePoint.Y),
                        Math.Abs(mouseDownPoint.X - mousePoint.X),
                        Math.Abs(mouseDownPoint.Y - mousePoint.Y));
                    e.Graphics.DrawRectangle(redPen, window);
                }
                else if (currentObject != null && camera != null)
                {
                    e.Graphics.DrawRectangle(redPen, getRectToScaled(currentObject.Rectangle));
                }
            }
        }

        private void numericUpDownSamplesPerObject_ValueChanged(object sender, EventArgs e)
        {
            detectionSystem.SamplesPerObject = Convert.ToInt32(numericUpDownSamplesPerObject.Value);
        }

        private void numericUpDownAnalysisFPS_ValueChanged(object sender, EventArgs e)
        {
            analysisTimer.Interval = 1000/(int)numericUpDownAnalysisFPS.Value;
        }

        private void checkBoxSnaphotOnTrigger_CheckedChanged(object sender, EventArgs e)
        {
            snapshotOnTrigger = checkBoxSnaphotOnTrigger.Checked;
        }
    }
}
 