using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;

namespace Blik
{
    class DetectionSystem
    {
        List<DetectionObject> detectionObjects = new List<DetectionObject>();
        long analysisTime;
        bool makeAnalysisImage;
        Bitmap bitmapAnalysis;
        Bitmap bitmapNormal = new Bitmap(640, 480);
        Bitmap bitmapTrigger = new Bitmap(640, 480);
        int samplesPerObject = 10000;     // setting this to a high number caused invalidargumentexceptions when copying camera lastframe.

        public List<DetectionObject> DetectionObjects
        {
            get
            {
                return detectionObjects;
            }

            set
            {
                detectionObjects = value;
            }
        }

        public bool MakeAnalysisImage
        {
            get
            {
                return makeAnalysisImage;
            }

            set
            {
                makeAnalysisImage = value;
            }
        }

        public int SamplesPerObject
        {
            get
            {
                return samplesPerObject;
            }

            set
            {
                samplesPerObject = value;
            }
        }

        public Bitmap BitmapAnalysis
        {
            get
            {
                return bitmapAnalysis;
            }

            set
            {
                bitmapAnalysis = value;
            }
        }

        public Bitmap BitmapNormal
        {
            get
            {
                return bitmapNormal;
            }

            set
            {
                bitmapNormal = value;
            }
        }

        public Bitmap BitmapTrigger
        {
            get
            {
                return bitmapTrigger;
            }

            set
            {
                bitmapTrigger = value;
            }
        }

        public string displayObjectStatusInfo()
        {
            string text = "";
            foreach (DetectionObject obj in detectionObjects)
            {
                text += obj.displayStatusInfo() + System.Environment.NewLine;
            }

            return text;
        }

        public string displayObjectAnalysisInfo()
        {
            string text = "";
            foreach (DetectionObject obj in detectionObjects)
            {
                text += obj.displayAnalysisInfo() + System.Environment.NewLine;
            }

            return text;
        }

        public string displayStatistics()
        {
            string text = "analysis timing: " + analysisTime.ToString() + " ms" + System.Environment.NewLine;

            return text;
        }

        unsafe public void analyze(Bitmap bitmapCamera)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            BitmapData bitmapDataCamera = null;
            BitmapData bitmapDataNormal = null;
            BitmapData bitmapDataTrigger = null;
            try
            {
                bitmapDataCamera = bitmapCamera.LockBits(new Rectangle(0, 0, bitmapCamera.Width, bitmapCamera.Height), ImageLockMode.ReadOnly, bitmapCamera.PixelFormat);
                bitmapDataNormal = BitmapNormal.LockBits(new Rectangle(0, 0, BitmapNormal.Width, BitmapNormal.Height), ImageLockMode.ReadOnly, BitmapNormal.PixelFormat);
                bitmapDataTrigger = BitmapTrigger.LockBits(new Rectangle(0, 0, BitmapTrigger.Width, BitmapTrigger.Height), ImageLockMode.ReadOnly, BitmapTrigger.PixelFormat);

                int bytesPerPixel = System.Drawing.Bitmap.GetPixelFormatSize(bitmapCamera.PixelFormat) / 8;
                int heightInPixels = bitmapDataCamera.Height;
                int widthInBytes = bitmapDataCamera.Width * bytesPerPixel;

                byte* PtrFirstPixel = (byte*)bitmapDataCamera.Scan0;
                byte* PtrFirstPixelNormal = (byte*)bitmapDataNormal.Scan0;
                byte* PtrFirstPixelTrigger = (byte*)bitmapDataTrigger.Scan0;

                foreach (DetectionObject obj in detectionObjects)
                {
                    if (!obj.Active)
                    {
                        continue;
                    }

                    int numPixels = obj.Rectangle.Width * obj.Rectangle.Height;     // TODO: make property of DetectionObject
                    int step = (numPixels / SamplesPerObject);
                    if (step == 0)
                    {
                        step = 1;
                    }

                    int y = obj.Rectangle.Top;
                    int totalDifferenceNormal = 0;
                    int totalDifferenceTrigger = 0;

                    float maxDifference = ((numPixels / step) * bytesPerPixel * 255);

                    while (y < obj.Rectangle.Bottom)
                    {
                        byte* currentLine = PtrFirstPixel + (y * bitmapDataCamera.Stride);
                        byte* currentLineNormal = PtrFirstPixelNormal + (y * bitmapDataCamera.Stride);
                        byte* currentLineTrigger = PtrFirstPixelTrigger + (y * bitmapDataCamera.Stride);
                        byte* currentLineAnalysis = null;

                        for (int x = obj.Rectangle.Left + (y % step); x < obj.Rectangle.Right; x += step)
                        {
                            int offsetX = x * bytesPerPixel;
/*
                            totalDifferenceNormal += Math.Abs(currentLine[offsetX] - currentLineNormal[offsetX]);
                            totalDifferenceNormal += Math.Abs(currentLine[offsetX + 1] - currentLineNormal[offsetX + 1]);
                            totalDifferenceNormal += Math.Abs(currentLine[offsetX + 2] - currentLineNormal[offsetX + 2]);
*/
                            totalDifferenceTrigger += Math.Abs(currentLine[offsetX] - currentLineTrigger[offsetX]);
                            totalDifferenceTrigger += Math.Abs(currentLine[offsetX + 1] - currentLineTrigger[offsetX + 1]);
                            totalDifferenceTrigger += Math.Abs(currentLine[offsetX + 2] - currentLineTrigger[offsetX + 2]);
                        }
                        y++;
                    }

                    obj.DifferenceNormal = totalDifferenceNormal / maxDifference;

                    float numSamples = numPixels / step;

                    // When the difference is small enough, we make further investigation
/*                    if (obj.DifferenceNormal < 0.05)
                    {
                        obj.SecondLevelDifferenceNormal = secondLevelAnalysis(bitmapDataCamera, bitmapDataNormal, obj, step, bytesPerPixel) / numSamples;
                    }
*/
                    obj.DifferenceTrigger = totalDifferenceTrigger / maxDifference;
                    // When the difference is small enough, we make further investigation
                    if (obj.DifferenceTrigger < 0.05)
                    {
                        obj.SecondLevelDifferenceTrigger = secondLevelAnalysis(bitmapDataCamera, bitmapDataTrigger, obj, step, bytesPerPixel) / numSamples;
                    }
                    obj.updateStatus();
                }

            } catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                bitmapCamera.UnlockBits(bitmapDataCamera);
                bitmapNormal.UnlockBits(bitmapDataNormal);
                bitmapTrigger.UnlockBits(bitmapDataTrigger);
            }

            watch.Stop();
            analysisTime = watch.ElapsedMilliseconds;

            if (makeAnalysisImage)
            {
                buildAnalysisImage(bitmapCamera);
            }
        }

        // value < 3000 means the same object.
        unsafe int secondLevelAnalysis(BitmapData bitmapData1, BitmapData bitmapData2, DetectionObject obj, int step, int bytesPerPixel)
        {
            int totalDifference = 0;
            try
            {
                byte* PtrFirstPixel1 = (byte*)bitmapData1.Scan0;
                byte* PtrFirstPixel2 = (byte*)bitmapData2.Scan0;
                if (step == 0)
                {
                    step = 1;
                }
                int diff = 0;
                int y = obj.Rectangle.Top;
                while (y < obj.Rectangle.Bottom)
                {
                    for (int x = obj.Rectangle.Left; x < obj.Rectangle.Right; x += step)
                    {
                        byte* currentLine = PtrFirstPixel1 + (y * bitmapData1.Stride);

                        for (int color = 0; color < 3; color++)   // for red, green and blue
                        {
                            int difference = 99999999;
                            for (int adjustY = -1; adjustY < 2; adjustY++)
                            {
                                /*
                                if ((y + adjustY) < 0)
                                {
                                    continue;       // skip looking above on first line
                                }
                                if ((y + adjustY) > bitmap1.Height)
                                {
                                    continue;       // skip looking below on last line
                                }*/

                                byte* currentLineTrigger = PtrFirstPixel2 + ((y + adjustY) * bitmapData1.Stride);

                                for (int adjustX = -1; adjustX < 2; adjustX++)
                                {
                                    /*
                                    if ((x + adjustX) < 0)
                                    {
                                        continue;       // skip looking left on first pixel
                                    }
                                    if ((x + adjustX) > bitmap1.Width)
                                    {
                                        continue;       // skip looking right on last pixel
                                    }*/

                                    diff = Math.Abs(adjustX) + Math.Abs(adjustY) + Math.Abs(currentLine[x * bytesPerPixel + color] - currentLineTrigger[(x + adjustX) * bytesPerPixel + color]);
                                    if (diff < difference)
                                    {
                                        difference = diff;
                                    }
                                }
                            }
                            totalDifference += (difference * difference);
                        }
                    }

                    y++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return totalDifference;
        }
       
        private void buildAnalysisImage(Bitmap bitmapCamera)
        {
            bitmapAnalysis = (Bitmap)bitmapCamera.Clone();
            Graphics g = Graphics.FromImage(bitmapAnalysis);
            g.Clear(Color.Black);

            unsafe
            {
                BitmapData bitmapDataCamera = null;
                BitmapData bitmapDataNormal = null;
                BitmapData bitmapDataTrigger = null;
                BitmapData bitmapDataAnalysis = null;
                try
                {
                    bitmapDataCamera = bitmapCamera.LockBits(new Rectangle(0, 0, bitmapCamera.Width, bitmapCamera.Height), ImageLockMode.ReadOnly, bitmapCamera.PixelFormat);
                    bitmapDataNormal = BitmapNormal.LockBits(new Rectangle(0, 0, BitmapNormal.Width, BitmapNormal.Height), ImageLockMode.ReadOnly, BitmapNormal.PixelFormat);
                    bitmapDataTrigger = BitmapTrigger.LockBits(new Rectangle(0, 0, BitmapTrigger.Width, BitmapTrigger.Height), ImageLockMode.ReadOnly, BitmapTrigger.PixelFormat);
                    bitmapDataAnalysis = BitmapAnalysis.LockBits(new Rectangle(0, 0, BitmapTrigger.Width, BitmapTrigger.Height), ImageLockMode.WriteOnly, BitmapTrigger.PixelFormat);

                    int bytesPerPixel = System.Drawing.Bitmap.GetPixelFormatSize(bitmapCamera.PixelFormat) / 8;
                    int heightInPixels = bitmapDataCamera.Height;
                    int widthInBytes = bitmapDataCamera.Width * bytesPerPixel;

                    byte* PtrFirstPixel = (byte*)bitmapDataCamera.Scan0;
                    byte* PtrFirstPixelNormal = (byte*)bitmapDataNormal.Scan0;
                    byte* PtrFirstPixelTrigger = (byte*)bitmapDataTrigger.Scan0;
                    byte* PtrFirstPixelAnalysisImage = (byte*)bitmapDataAnalysis.Scan0;

                    foreach (DetectionObject obj in detectionObjects)
                    {
                        int numPixels = obj.Rectangle.Width * obj.Rectangle.Height;   
                        int step = (numPixels / SamplesPerObject);
                        if (step == 0)
                        {
                            step = 1;
                        }
                        int y = obj.Rectangle.Top;
                        float maxDifference = (SamplesPerObject * bytesPerPixel * 256);

                        while (y < obj.Rectangle.Bottom)
                        {
                            byte* currentLine = PtrFirstPixel + (y * bitmapDataCamera.Stride);
                            byte* currentLineNormal = PtrFirstPixelNormal + (y * bitmapDataCamera.Stride);
                            byte* currentLineTrigger = PtrFirstPixelTrigger + (y * bitmapDataCamera.Stride);
                            byte* currentLineAnalysis = PtrFirstPixelAnalysisImage + (y * bitmapDataAnalysis.Stride);

                            for (int x = obj.Rectangle.Left + (y % step); x < obj.Rectangle.Right; x += step)
                            {
                                int offsetX = x * bytesPerPixel;

                                int differenceNormal = Math.Abs(currentLine[offsetX] - currentLineNormal[offsetX]);
                                differenceNormal += Math.Abs(currentLine[offsetX + 1] - currentLineNormal[offsetX + 1]);
                                differenceNormal += Math.Abs(currentLine[offsetX + 2] - currentLineNormal[offsetX + 2]);

                                int differenceTrigger = Math.Abs(currentLine[offsetX] - currentLineTrigger[offsetX]);
                                differenceTrigger += Math.Abs(currentLine[offsetX + 1] - currentLineTrigger[offsetX + 1]);
                                differenceTrigger += Math.Abs(currentLine[offsetX + 2] - currentLineTrigger[offsetX + 2]);

                                if (differenceNormal < differenceTrigger)
                                {
                                    byte color = (byte)(255 - (differenceNormal / 3)); // difference is maximum 3*255 = 765. In that case outcome must be 255.
                                    currentLineAnalysis[offsetX + 1] = color;
                                }
                                else
                                {
                                    byte color = (byte)(255 - (differenceTrigger / 3)); // difference is maximum 3*255 = 765. In that case outcome must be 255.
                                    currentLineAnalysis[offsetX + 2] = color;
                                }
                            }
                            y++;
                        }
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    bitmapCamera.UnlockBits(bitmapDataCamera);
                    bitmapNormal.UnlockBits(bitmapDataNormal);
                    bitmapTrigger.UnlockBits(bitmapDataTrigger);
                    bitmapAnalysis.UnlockBits(bitmapDataAnalysis);
                }
            }
        }
    }
}
