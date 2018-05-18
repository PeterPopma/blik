namespace dshow
{
	using System;
    using System.Drawing;
    using System.Runtime.InteropServices;
	using dshow.Core;

	/// <summary>
	/// Tools class
	/// </summary>
	public class DSTools
	{
		// Get pin of the filter
		public static IPin GetPin(IBaseFilter filter, PinDirection dir, int num)
		{
			IPin[] pin = new IPin[1];

			IEnumPins pinsEnum = null;

			// enum filter pins
			if (filter.EnumPins(out pinsEnum) == 0)
			{
				PinDirection pinDir;
				int n;

				// get next pin
				while (pinsEnum.Next(1, pin, out n) == 0)
				{
					// query pin`s direction
					pin[0].QueryDirection(out pinDir);

					if (pinDir == dir)
					{
						if (num == 0)
							return pin[0];
						num--;
					}

					Marshal.ReleaseComObject(pin[0]);
					pin[0] = null;
				}
			}
			return null;
		}

		// Get input pin of the filter
		public static IPin GetInPin(IBaseFilter filter, int num)
		{
			return GetPin(filter, PinDirection.Input, num);
		}

		// Get output pin of the filter
		public static IPin GetOutPin(IBaseFilter filter, int num)
		{
			return GetPin(filter, PinDirection.Output, num);
		}

        public static Point GetMaxFrameSize(IPin pStill)
        {
            VideoInfoHeader v;

            IAMStreamConfig videoStreamConfig = pStill as IAMStreamConfig;

            int iCount = 0, iSize = 0;
            videoStreamConfig.GetNumberOfCapabilities(out iCount, out iSize);

            IntPtr TaskMemPointer = Marshal.AllocCoTaskMem(iSize);

            int iMaxHeight = 0;
            int iMaxWidth = 0;

            for (int iFormat = 0; iFormat < iCount; iFormat++)
            {
                AMMediaType pmtConfig = null;
                IntPtr ptr = IntPtr.Zero;

                videoStreamConfig.GetStreamCaps(iFormat, out pmtConfig, TaskMemPointer);

                v = (VideoInfoHeader)Marshal.PtrToStructure(pmtConfig.formatPtr, typeof(VideoInfoHeader));
                if (v.BmiHeader.Width > iMaxWidth)
                {
                    iMaxWidth = v.BmiHeader.Width;
                    iMaxHeight = v.BmiHeader.Height;
                }
                FreeAMMediaType(pmtConfig);

            }

            Marshal.FreeCoTaskMem(TaskMemPointer);


            return new Point(iMaxWidth, iMaxHeight);
        }

        /// <summary>
        ///  Free the nested structures and release any
        ///  COM objects within an AMMediaType struct.
        /// </summary>
        public static void FreeAMMediaType(AMMediaType mediaType)
        {
            if (mediaType != null)
            {
                if (mediaType.formatSize != 0)
                {
                    Marshal.FreeCoTaskMem(mediaType.formatPtr);
                    mediaType.formatSize = 0;
                    mediaType.formatPtr = IntPtr.Zero;
                }
                if (mediaType.unkPtr != IntPtr.Zero)
                {
                    Marshal.Release(mediaType.unkPtr);
                    mediaType.unkPtr = IntPtr.Zero;
                }
            }
        }
    }
}
