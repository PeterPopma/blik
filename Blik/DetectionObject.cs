using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Blik
{
    class DetectionObject
    {
        private Rectangle rectangle;
        private float differenceNormal;
        private float differenceTrigger;
        private float secondLevelDifferenceNormal;
        private float secondLevelDifferenceTrigger;
        private bool trigger;
        private bool normal;
        private bool active = true;
        private int offsetX;
        private int offsetY;

        public DetectionObject()
        {
        }

        public DetectionObject(int x, int y, int width, int height)
        {
            rectangle = new Rectangle(x, y, width, height);
        }

        public Rectangle Rectangle
        {
            get
            {
                return rectangle;
            }

            set
            {
                rectangle = value;
            }
        }

        public float DifferenceNormal
        {
            get
            {
                return differenceNormal;
            }

            set
            {
                differenceNormal = value;
            }
        }

        public float DifferenceTrigger
        {
            get
            {
                return differenceTrigger;
            }

            set
            {
                differenceTrigger = value;
            }
        }

        public bool Active
        {
            get
            {
                return active;
            }

            set
            {
                active = value;
            }
        }

        public bool Normal
        {
            get
            {
                return normal;
            }

            set
            {
                normal = value;
            }
        }

        public bool Trigger
        {
            get
            {
                return trigger;
            }

            set
            {
                trigger = value;
            }
        }

        public int OffsetX
        {
            get
            {
                return offsetX;
            }

            set
            {
                offsetX = value;
            }
        }

        public int OffsetY
        {
            get
            {
                return offsetY;
            }

            set
            {
                offsetY = value;
            }
        }

        public float SecondLevelDifferenceNormal
        {
            get
            {
                return secondLevelDifferenceNormal;
            }

            set
            {
                secondLevelDifferenceNormal = value;
            }
        }

        public float SecondLevelDifferenceTrigger
        {
            get
            {
                return secondLevelDifferenceTrigger;
            }

            set
            {
                secondLevelDifferenceTrigger = value;
            }
        }

        public string displayStatus()
        {
            if (trigger)
            {
                return "Trigger";
            }
            if (normal)
            {
                return "Normal";
            }

            return "Undecided";
        }

        public void updateStatus()
        {
/*            if (differenceNormal < differenceTrigger && differenceNormal < 0.05 && secondLevelDifferenceNormal < 1200)
            {
                normal = true;
            }
            else
            {
                normal = false;
            }
*/
            if (/*differenceTrigger < differenceNormal &&*/ differenceTrigger < 0.12 && secondLevelDifferenceTrigger < 2500)
            {
                trigger = true;
            }
            else
            {
                trigger = false;
            }
        }

        public String displayStatusInfo()
        {
            return "Active: " + active.ToString() + " Status: " + displayStatus();
        }

        public String displayAnalysisInfo()
        {
            return "normal%: " + differenceNormal.ToString("0.000") + "  trigger%: " + differenceTrigger.ToString("0.000") + "  2L-Normal:" + SecondLevelDifferenceNormal.ToString("0.000") + "  2L-Trigger:" + SecondLevelDifferenceTrigger.ToString("0.000") + "  OffsetX: " + offsetX + "  OffsetY: " + offsetY;
        }
    }
}
