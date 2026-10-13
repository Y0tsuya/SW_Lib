using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

// SimplePixViewer v3.5.0	Implement FrameCache

namespace GifMotion
{
    public class AnimatedGif
    {
        private Image gifImage;
        private FrameDimension dimension;
        private int frameCount;
        private int currentFrame = -1;
        private bool reverse;
        private int step = 1;
        private int delay;

        /// <summary>
        /// Create a new Animated GIF
        /// </summary>
        /// <param name="filePath">The Path where the Animated GIF gets saved</param>
        /// <param name="delay">Delay between frames</param>
        /// <param name="repeat">GIF Repeat count (0 meaning forever)</param>
        /// <returns></returns>
        public static GifCreator Create(string filePath, int delay, int repeat = 0) =>
            new GifCreator(filePath, delay, repeat);


        public AnimatedGif(string path)
        {
            gifImage = Image.FromFile(path);
            dimension = new FrameDimension(gifImage.FrameDimensionsList[0]); // gets the GUID
            frameCount = gifImage.GetFrameCount(dimension); // total frames in the animation
            delay = BitConverter.ToInt32(gifImage.GetPropertyItem(20736).Value, 0) * 10;
			InitializeFrameCache(frameCount);
        }

        public AnimatedGif(Image img) {
            gifImage = img;
            dimension = new FrameDimension(gifImage.FrameDimensionsList[0]); // gets the GUID
            frameCount = gifImage.GetFrameCount(dimension); // total frames in the animation
            delay = BitConverter.ToInt32(gifImage.GetPropertyItem(20736).Value, 0) * 10;
			InitializeFrameCache(frameCount);
		}


		public bool ReverseAtEnd
        {
            get { return reverse; }
            set { reverse = value; }
        }

        public int Delay {
            get { return delay; }
        }

        public int FrameCount
        {
            get { return frameCount; }
        }

        public Image GetNextFrame()
        {
            currentFrame += step;

            // if the animation reaches a boundary
            if (currentFrame >= frameCount || currentFrame < 1)
            {
                if (reverse)
                {
                    step *= -1; // reverse the count
                    currentFrame += step; // apply it
                }
                else
                {
                    currentFrame = 0; // or start over
                }
            }
            return GetFrame(currentFrame);
        }

		public Image[] FrameCache = null;

		public void InitializeFrameCache(int frames) {
			if (FrameCache != null) {
				for (int i = 0; i < FrameCache.Length; i++) {
					FrameCache[i]?.Dispose();
				}
			}
			FrameCache = new Image[frames];
		}

        public Image GetFrame(int index)
        {
			Debug.Assert(FrameCache != null);
			if (FrameCache[index] == null) {
				gifImage.SelectActiveFrame(dimension, index); // find the frame
				FrameCache[index] = (Image)gifImage.Clone();
			}
			return (Image)FrameCache[index].Clone();
		}

	}
}