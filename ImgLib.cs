//using ILGPU.Util;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
//using MathNet.Numerics.LinearAlgebra;
//using OpenCvSharp;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

//using ILGPU;
//using ILGPU.Runtime;
//using ILGPU.Runtime.CPU;
//using DlibDotNet;

// Mustang v1.9.3		EM/EM2 scaler speed optimization
//						Fix EM/EM2 x/y factor float rounding error in EM/EM2
// Beowulf v4.0.0		EM/EM2 Pre-scale optimization
//						EM/EM2 Throw upscale exception
//						Remove unused extraneous scalers
// Beowulf v4.0.2		Change ScalingQuality -> ScalerType
// Hummingbird v2.1.0	Change GreyImage from Avg to YUV
//						Processed images carries new ImageTag
//						unsafe operations now check for BPP
//						Add QuickCopy() function
//						Rename Copy() to DeepCopy()
// SimplePixViewer v3.3.0	Change most doubles to floats
// SimplePixViewer v3.7.5	Workaround ConvertTo32bppPargbMT not working with CMYK PixelFormat 8207
//  -------------------
// For Mustang			Split Bitmap.Tag into multi-field usage

namespace CustomLibs {

	public enum ScalerType {
		LQ, MQ, HQ, EM, EM2, RTL, RTLM
	};

	public enum ImageTag {
		UNDEF, RGB, MONO, YUV
	}

	public class ImageSizeException : Exception {
		public ImageSizeException() {
		}

		public ImageSizeException(string message)
			: base(message) {
		}

		public ImageSizeException(string message, Exception inner)
			: base(message, inner) {
		}
	}

	public static class BitmapExtensions {
		// Tag byte order:
		// [RSVC] [ImageTag] [Serial Hi] [Serial Lo]

		public static int GetSerialTag(this Bitmap bmp) {
			if (bmp.Tag != null) {
				return ((int)bmp.Tag & 0x0000FFFF);
			} else {
				return 0;
			}
		}

		public static void SetSerialTag(this Bitmap bmp, int serial) {
			UInt32 currentTag;
			if (bmp.Tag != null) {
				currentTag = (UInt32)bmp.Tag;
			} else {
				currentTag = 0;
			}
			bmp.Tag = (currentTag & 0xFFFF0000) | ((UInt32)serial & 0x0000FFFF);
		}

		public static ImageTag GetImageTag(this Bitmap bmp) {
			if (bmp.Tag != null) {
				return (ImageTag)(((UInt32)bmp.Tag >> 16) & 0xFF);
			} else {
				return ImageTag.UNDEF;
			}
		}

		public static void SetImageTag(this Bitmap bmp, ImageTag tag) {
			UInt32 currentTag;
			if (bmp.Tag != null) {
				currentTag = (UInt32)bmp.Tag;
			} else {
				currentTag = 0;
			}
			bmp.Tag = (currentTag & 0xFF00FFFF) | (((UInt32)tag << 16) & 0x00FF0000);
		}
	}


	public class ImgLib {

		public static int Threads = 1;

		/*public static byte[] GetBytesFromStream(MemoryStream memStream, int offset, int length) {
			byte[] bytes = new byte[length];

			memStream.Read(bytes, offset, length);
			return bytes;
		}

		public static Size GetImageSizeFromStream(MemoryStream memStream) {
			Size imgSize = new Size();
			int ptr;
			byte[] header;
			ushort jpegMarker;

			// test for JPEG
			header = GetBytesFromStream(memStream, 0, 256);
			jpegMarker = Header.ByteArrayToUShort(header, 0);
			if (jpegMarker == 0xD8FF) {

			}

			return imgSize;
		}*/

		public static Bitmap MakeBlankBitmap(Size size, Color clr) {
			Bitmap blank = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
			using (Graphics g = Graphics.FromImage(blank)) {
				g.Clear(clr);
			}
			return blank;
		}

		public static void SaveJpeg(Bitmap bmp, string path, int quality) {
			ImageCodecInfo myImageCodecInfo;
			Encoder myEncoder;
			EncoderParameter myEncoderParameter;
			EncoderParameters myEncoderParameters;

			// Get an ImageCodecInfo object that represents the JPEG codec.
			myImageCodecInfo = GetEncoderInfo("image/jpeg");
			// Create an Encoder object based on the GUID
			// for the Quality parameter category.
			myEncoder = Encoder.Quality;
			// Create an EncoderParameters object.
			// An EncoderParameters object has an array of EncoderParameter
			// objects. In this case, there is only one
			// EncoderParameter object in the array.
			myEncoderParameters = new EncoderParameters(1);
			// Save the bitmap as a JPEG file with quality level 25.
			myEncoderParameter = new EncoderParameter(myEncoder, quality);
			myEncoderParameters.Param[0] = myEncoderParameter;
			bmp.Save(path, myImageCodecInfo, myEncoderParameters);
		}

		private static ImageCodecInfo GetEncoderInfo(String mimeType) {
			int j;
			ImageCodecInfo[] encoders;
			encoders = ImageCodecInfo.GetImageEncoders();
			for (j = 0; j < encoders.Length; ++j) {
				if (encoders[j].MimeType == mimeType)
					return encoders[j];
			}
			return null;
		}

		public static Bitmap GreyImage(Bitmap srcBitmap) {
			int i, j;
			int width, height;

			Debug.Assert((Image.GetPixelFormatSize(srcBitmap.PixelFormat) == 32));
			//if ((srcBitmap.GetImageTag() != ImageTag.UNDEF) && (srcBitmap.GetImageTag() == ImageTag.MONO)) {
			if (srcBitmap.GetImageTag() == ImageTag.MONO) {
				return DeepCopy(srcBitmap);
			}
			Bitmap dstBitmap = new Bitmap(srcBitmap);

			BitmapData srcData = srcBitmap.LockBits(new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			BitmapData dstData = dstBitmap.LockBits(new Rectangle(0, 0, dstBitmap.Width, dstBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			width = srcBitmap.Width;
			height = srcBitmap.Height;
			int r, g, b, y;

			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* dstPtr = (byte*)dstData.Scan0;
				byte* srcOffset;
				byte* dstOffset;

				srcOffset = srcPtr;
				dstOffset = dstPtr;
				for (j = 0; j < height; j++) {
					for (i = 0; i < width; i++) {
						b = srcOffset[0];
						g = srcOffset[1];
						r = srcOffset[2];
						//y = (299 * r + 587 * g + 114 * b) / 1000;
						y = (306 * r + 601 * g + 117 * b) >> 10;
						dstOffset[0] = (byte)y;
						dstOffset[1] = (byte)y;
						dstOffset[2] = (byte)y;
						dstOffset[3] = 255;
						srcOffset += 4;
						dstOffset += 4;
					}
				}
			}
			srcBitmap.UnlockBits(srcData);
			dstBitmap.UnlockBits(dstData);
			dstBitmap.SetImageTag(ImageTag.MONO);
			return dstBitmap;
		}

		public static byte[,] ArrayRgbToMono(byte[][,] inMap) {
			int j, k;
			int width, height;
			width = inMap.GetLength(1);
			height = inMap.GetLength(0);
			byte[,] map = new byte[height, width];
			for (j = 0; j < height; j++) {
				for (k = 0; k < width; k++) {
					map[j, k] = (byte)((inMap[0][j, k] + inMap[1][j, k] + inMap[2][j, k]) / 3);
				}
			}
			return map;
		}


		public static byte[][,] ArrayMonoToRgb(byte[,] inMap) {
			int j, k;
			int width, height;
			width = inMap.GetLength(1);
			height = inMap.GetLength(0);
			byte[][,] map = new byte[4][,];
			for (int c = 0; c < 4; c++) {
				map[c] = new byte[height, width];
			}
			for (j = 0; j < height; j++) {
				for (k = 0; k < width; k++) {
					map[0][j, k] = inMap[j, k];
					map[1][j, k] = inMap[j, k];
					map[2][j, k] = inMap[j, k];
					map[3][j, k] = 255;
				}
			}
			return map;
		}

		public static float[] Average(Bitmap srcBitmap) {
			Rectangle area = new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height);
			return Average(srcBitmap, area);
		}

		public static float[] Average(Bitmap srcBitmap, Rectangle area) {
			float[] sum = new float[3];
			int i, j;
			int bpp = 4;
			int stride;

			Debug.Assert((Image.GetPixelFormatSize(srcBitmap.PixelFormat) == 32));
			BitmapData srcData = srcBitmap.LockBits(new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			stride = srcBitmap.Width * bpp;
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* lineOffset, pixelOffset;

				for (j = area.Y; j < area.Bottom; j++) {
					lineOffset = srcPtr + (j * stride);
					pixelOffset = lineOffset + (area.X * bpp);
					for (i = area.X; i < area.Right; i++) {
						sum[0] += pixelOffset[0];   // Blue
						sum[1] += pixelOffset[1];   // Green
						sum[2] += pixelOffset[2];   // Red
						pixelOffset += 4;
					}
				}
			}
			srcBitmap.UnlockBits(srcData);
			sum[0] = sum[0] / (area.Width * area.Height);
			sum[1] = sum[1] / (area.Width * area.Height);
			sum[2] = sum[2] / (area.Width * area.Height);
			return sum;
		}

		public static float AverageMono(byte[,]? map) {
			if (map == null) return 0.0f;
			int w = map.GetLength(1);
			int h = map.GetLength(0);
			Rectangle area = new Rectangle(new Point(0, 0), new Size(w, h));
			return AverageMono(map, area);
		}

		public static float AverageMono(byte[,]? map, Rectangle area) {
			int j, k;
			float sum = 0;

			Debug.Assert(map != null);
			for (j = area.Y; j < area.Bottom; j++) {
				for (k = area.X; k < area.Right; k++) {
					sum += map[j, k];
				}
			}
			sum = sum / (area.Width * area.Height);
			return sum;
		}

		public static bool AreBitmapsIdentical(Bitmap srcBitmap, Bitmap dstBitmap) {
			int i, j;
			int width, height;

			Debug.Assert((Image.GetPixelFormatSize(srcBitmap.PixelFormat) == 32));
			Debug.Assert((Image.GetPixelFormatSize(dstBitmap.PixelFormat) == 32));
			BitmapData srcData = srcBitmap.LockBits(new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			BitmapData dstData = dstBitmap.LockBits(new Rectangle(0, 0, dstBitmap.Width, dstBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			width = srcBitmap.Width;
			height = srcBitmap.Height;
			bool isDifferent = false;
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* dstPtr = (byte*)dstData.Scan0;
				byte* srcOffset;
				byte* dstOffset;

				srcOffset = srcPtr;
				dstOffset = dstPtr;
				for (j = 0; j < height; j++) {
					for (i = 0; i < width; i++) {
						if (dstOffset[0] != srcOffset[0]) {
							isDifferent = true;
							break;
						}
						if (dstOffset[1] != srcOffset[1]) {
							isDifferent = true;
							break;
						}
						if (dstOffset[2] != srcOffset[2]) {
							isDifferent = true;
							break;
						}
						if (dstOffset[3] != srcOffset[3]) {
							isDifferent = true;
							break;
						}
						srcOffset += 4;
						dstOffset += 4;
					}
				}
			}
			srcBitmap.UnlockBits(srcData);
			dstBitmap.UnlockBits(dstData);
			if (isDifferent) return false;
			else return true;
		}

		public static Bitmap QuickCopy(Bitmap srcBitmap) {
			Bitmap dstBitmap = new Bitmap(srcBitmap);
			if (srcBitmap.GetImageTag() != ImageTag.UNDEF) dstBitmap.SetImageTag(srcBitmap.GetImageTag());
			else dstBitmap.SetImageTag(ImageTag.RGB);
			return dstBitmap;
		}

		public static Bitmap DeepCopy(Bitmap srcBitmap) {
			int i, j;
			int width, height;

			Debug.Assert((Image.GetPixelFormatSize(srcBitmap.PixelFormat) == 32));
			Bitmap dstBitmap = new Bitmap(srcBitmap);
			BitmapData srcData = srcBitmap.LockBits(new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			BitmapData dstData = dstBitmap.LockBits(new Rectangle(0, 0, dstBitmap.Width, dstBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			width = srcBitmap.Width;
			height = srcBitmap.Height;
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* dstPtr = (byte*)dstData.Scan0;
				byte* srcOffset;
				byte* dstOffset;

				srcOffset = srcPtr;
				dstOffset = dstPtr;
				for (j = 0; j < height; j++) {
					for (i = 0; i < width; i++) {
						dstOffset[0] = srcOffset[0];
						dstOffset[1] = srcOffset[1];
						dstOffset[2] = srcOffset[2];
						dstOffset[3] = srcOffset[3];
						srcOffset += 4;
						dstOffset += 4;
					}
				}
			}
			srcBitmap.UnlockBits(srcData);
			dstBitmap.UnlockBits(dstData);
			if (srcBitmap.GetImageTag() != ImageTag.UNDEF) dstBitmap.SetImageTag(srcBitmap.GetImageTag());
			else dstBitmap.SetImageTag(ImageTag.RGB);
			return dstBitmap;
		}

		public static Bitmap ConvertTo32bppPargbGdi(Bitmap inBmp, bool shouldDispose) {
			Bitmap outBmp = inBmp.Clone(new Rectangle(0, 0, inBmp.Width, inBmp.Height), PixelFormat.Format32bppPArgb);
			if (shouldDispose) inBmp.Dispose();
			outBmp.SetImageTag(ImageTag.RGB);
			return outBmp;
		}

		public static Bitmap ConvertTo32bppArgbGdi(Bitmap inBmp, bool shouldDispose) {
			Bitmap outBmp = inBmp.Clone(new Rectangle(0, 0, inBmp.Width, inBmp.Height), PixelFormat.Format32bppArgb);
			if (shouldDispose) inBmp.Dispose();
			outBmp.SetImageTag(ImageTag.RGB);
			return outBmp;
		}

		public static Bitmap ConvertTo32bppArgb(Bitmap srcBitmap, bool shouldDispose) {
			Bitmap dstBitmap = new Bitmap(srcBitmap.Width, srcBitmap.Height, PixelFormat.Format32bppArgb);
			int i, j;
			int width, height;
			BitmapData srcData;
			ColorPalette pal;
			Color indexed;

			pal = srcBitmap.Palette;
			srcData = srcBitmap.LockBits(new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height), ImageLockMode.ReadOnly, srcBitmap.PixelFormat);
			int stride = srcData.Stride;
			BitmapData dstData = dstBitmap.LockBits(new Rectangle(0, 0, dstBitmap.Width, dstBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			width = srcBitmap.Width;
			height = srcBitmap.Height;
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* dstPtr = (byte*)dstData.Scan0;
				byte* srcOffset, srcLineOffset;
				byte* dstOffset;
				srcLineOffset = srcPtr;
				dstOffset = dstPtr;
				for (j = 0; j < height; j++) {
					srcOffset = srcLineOffset;
					switch (srcBitmap.PixelFormat) {
						case PixelFormat.Format32bppArgb:
						case PixelFormat.Format32bppPArgb:
							for (i = 0; i < width; i++) {
								dstOffset[0] = srcOffset[0];
								dstOffset[1] = srcOffset[1];
								dstOffset[2] = srcOffset[2];
								dstOffset[3] = srcOffset[3];
								srcOffset += 4;
								dstOffset += 4;
							}
							break;
						case PixelFormat.Format32bppRgb:
							for (i = 0; i < width; i++) {
								dstOffset[0] = srcOffset[0];
								dstOffset[1] = srcOffset[1];
								dstOffset[2] = srcOffset[2];
								dstOffset[3] = 255;
								srcOffset += 4;
								dstOffset += 4;
							}
							break;
						case PixelFormat.Format24bppRgb:
							for (i = 0; i < width; i++) {
								dstOffset[0] = srcOffset[0];
								dstOffset[1] = srcOffset[1];
								dstOffset[2] = srcOffset[2];
								dstOffset[3] = 255;
								srcOffset += 3;
								dstOffset += 4;
							}
							break;
						case PixelFormat.Format16bppRgb555:
							break;
						case PixelFormat.Format16bppRgb565:
							break;
						case PixelFormat.Format16bppArgb1555:
							break;
						case PixelFormat.Format8bppIndexed:
							for (i = 0; i < width; i++) {
								indexed = pal.Entries[srcOffset[0]];
								dstOffset[0] = indexed.B;
								dstOffset[1] = indexed.G;
								dstOffset[2] = indexed.R;
								dstOffset[3] = 255;
								srcOffset += 1;
								dstOffset += 4;
							}
							break;
					}
					srcLineOffset += stride;
				}
			}
			srcBitmap.UnlockBits(srcData);
			dstBitmap.UnlockBits(dstData);
			if (srcBitmap.GetImageTag() != ImageTag.UNDEF) dstBitmap.SetImageTag(srcBitmap.GetImageTag());
			else dstBitmap.SetImageTag(ImageTag.RGB);
			if (shouldDispose) srcBitmap.Dispose();
			return dstBitmap;
		}

		public static Bitmap ConvertTo32bppPargb(Bitmap srcBitmap, bool shouldDispose) {
			Bitmap dstBitmap = new Bitmap(srcBitmap.Width, srcBitmap.Height, PixelFormat.Format32bppPArgb);
			int i, j, b;
			int width, height;
			BitmapData srcData;
			ColorPalette pal;
			Color indexed;
			float c, m, y, k;

			pal = srcBitmap.Palette;
			srcData = srcBitmap.LockBits(new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height), ImageLockMode.ReadOnly, srcBitmap.PixelFormat);
			int stride = srcData.Stride;
			BitmapData dstData = dstBitmap.LockBits(new Rectangle(0, 0, dstBitmap.Width, dstBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
			width = srcBitmap.Width;
			height = srcBitmap.Height;
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* dstPtr = (byte*)dstData.Scan0;
				byte* srcOffset, srcLineOffset;
				byte* dstOffset;
				srcLineOffset = srcPtr;
				dstOffset = dstPtr;
				for (j = 0; j < height; j++) {
					srcOffset = srcLineOffset;
					switch (srcBitmap.PixelFormat) {
						case PixelFormat.Format32bppArgb:
						case PixelFormat.Format32bppPArgb:
							for (i = 0; i < width; i++) {
								dstOffset[0] = srcOffset[0];
								dstOffset[1] = srcOffset[1];
								dstOffset[2] = srcOffset[2];
								dstOffset[3] = srcOffset[3];
								srcOffset += 4;
								dstOffset += 4;
							}
							break;
						case PixelFormat.Format32bppRgb:
							for (i = 0; i < width; i++) {
								dstOffset[0] = srcOffset[0];
								dstOffset[1] = srcOffset[1];
								dstOffset[2] = srcOffset[2];
								dstOffset[3] = 255;
								srcOffset += 4;
								dstOffset += 4;
							}
							break;
						case PixelFormat.Format24bppRgb:
							for (i = 0; i < width; i++) {
								dstOffset[0] = srcOffset[0];
								dstOffset[1] = srcOffset[1];
								dstOffset[2] = srcOffset[2];
								dstOffset[3] = 255;
								srcOffset += 3;
								dstOffset += 4;
							}
							break;
						case PixelFormat.Format16bppRgb555:
							break;
						case PixelFormat.Format16bppRgb565:
							break;
						case PixelFormat.Format16bppArgb1555:
							break;
						case PixelFormat.Format8bppIndexed:
							for (i = 0; i < width; i++) {
								indexed = pal.Entries[srcOffset[0]];
								dstOffset[0] = indexed.B;
								dstOffset[1] = indexed.G;
								dstOffset[2] = indexed.R;
								dstOffset[3] = 255;
								srcOffset += 1;
								dstOffset += 4;
							}
							break;
						case PixelFormat.Format4bppIndexed:
							for (i = 0; i < width; i += 2) {
								indexed = pal.Entries[(srcOffset[0] >> 4) & 0x0F];
								dstOffset[0] = indexed.B;
								dstOffset[1] = indexed.G;
								dstOffset[2] = indexed.R;
								dstOffset[3] = 255;
								dstOffset += 4;
								indexed = pal.Entries[srcOffset[0] & 0x0F];
								dstOffset[0] = indexed.B;
								dstOffset[1] = indexed.G;
								dstOffset[2] = indexed.R;
								dstOffset[3] = 255;
								srcOffset += 1;
								dstOffset += 4;
							}
							if (width % 2 == 1) dstOffset -= 4;
							break;
						case PixelFormat.Format1bppIndexed:
							for (i = 0; i < width; i += 8) {
								for (b = 7; b >= 0; b--) {
									indexed = pal.Entries[(srcOffset[0] >> b) & 0x01];
									dstOffset[0] = indexed.B;
									dstOffset[1] = indexed.G;
									dstOffset[2] = indexed.R;
									dstOffset[3] = 255;
									dstOffset += 4;
								}
								srcOffset += 1;
							}
							srcOffset += 1;
							if ((width % 8) != 0) {
								dstOffset -= 4 * (8 - (width % 8));
							}
							break;
						default:
							if ((int)srcBitmap.PixelFormat == 8207) {
								for (i = 0; i < width; i++) {
									c = (float)srcOffset[0] / (float)255.0;
									m = (float)srcOffset[1] / (float)255.0;
									y = (float)srcOffset[2] / (float)255.0;
									k = (float)srcOffset[3] / (float)255.0;
									dstOffset[0] = (byte)(255 * (1.0 - y) * (1.0 - k));
									dstOffset[1] = (byte)(255 * (1.0 - m) * (1.0 - k));
									dstOffset[2] = (byte)(255 * (1.0 - c) * (1.0 - k));
									dstOffset[3] = 255;
									srcOffset += 4;
									dstOffset += 4;
								}
							}
							break;
					}
					srcLineOffset += stride;
				}
			}
			srcBitmap.UnlockBits(srcData);
			dstBitmap.UnlockBits(dstData);
			return dstBitmap;
		}

		public static Bitmap ConvertTo32bppPargbMT(Bitmap srcBitmap, bool shouldDispose) {
			Bitmap dstBitmap;
			int width, height;
			BitmapData srcData, dstData;
			ColorPalette pal;
			int stride;

			if ((int)srcBitmap.PixelFormat == 8207) {
				return ConvertTo32bppPargb(srcBitmap, shouldDispose);
			}
			width = srcBitmap.Width;
			height = srcBitmap.Height;
			dstBitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
			srcData = srcBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, srcBitmap.PixelFormat);
			dstData = dstBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
			pal = srcBitmap.Palette;
			stride = srcData.Stride;
			ParallelOptions options = new ParallelOptions();
			options.MaxDegreeOfParallelism = Threads;
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* dstPtr = (byte*)dstData.Scan0;
				Parallel.For(0, height - 1, options, j => {
					//for (j = 0; j < height; j++) {
					byte* srcOffset = srcPtr + j * stride;
					byte* dstOffset = dstPtr + j * width * 4;
					Color indexed;
					int i = 0;
					int b;
					float c, m, y, k;
					switch (srcData.PixelFormat) {
						case PixelFormat.Format32bppArgb:
						case PixelFormat.Format32bppPArgb:
							for (i = 0; i < width; i++) {
								dstOffset[0] = srcOffset[0];
								dstOffset[1] = srcOffset[1];
								dstOffset[2] = srcOffset[2];
								dstOffset[3] = srcOffset[3];
								srcOffset += 4;
								dstOffset += 4;
							}
							break;
						case PixelFormat.Format32bppRgb:
							for (i = 0; i < width; i++) {
								dstOffset[0] = srcOffset[0];
								dstOffset[1] = srcOffset[1];
								dstOffset[2] = srcOffset[2];
								dstOffset[3] = 255;
								srcOffset += 4;
								dstOffset += 4;
							}
							break;
						case PixelFormat.Format24bppRgb:
							for (i = 0; i < width; i++) {
								dstOffset[0] = srcOffset[0];
								dstOffset[1] = srcOffset[1];
								dstOffset[2] = srcOffset[2];
								dstOffset[3] = 255;
								srcOffset += 3;
								dstOffset += 4;
							}
							break;
						case PixelFormat.Format16bppRgb555:
							break;
						case PixelFormat.Format16bppRgb565:
							break;
						case PixelFormat.Format16bppArgb1555:
							break;
						case PixelFormat.Format8bppIndexed:
							for (i = 0; i < width; i++) {
								indexed = pal.Entries[srcOffset[0]];
								dstOffset[0] = indexed.B;
								dstOffset[1] = indexed.G;
								dstOffset[2] = indexed.R;
								dstOffset[3] = 255;
								srcOffset += 1;
								dstOffset += 4;
							}
							break;
						case PixelFormat.Format4bppIndexed:
							for (i = 0; i < width; i += 2) {
								indexed = pal.Entries[(srcOffset[0] >> 4) & 0x0F];
								dstOffset[0] = indexed.B;
								dstOffset[1] = indexed.G;
								dstOffset[2] = indexed.R;
								dstOffset[3] = 255;
								dstOffset += 4;
								indexed = pal.Entries[srcOffset[0] & 0x0F];
								dstOffset[0] = indexed.B;
								dstOffset[1] = indexed.G;
								dstOffset[2] = indexed.R;
								dstOffset[3] = 255;
								srcOffset += 1;
								dstOffset += 4;
							}
							if (width % 2 == 1) dstOffset -= 4;
							break;
						case PixelFormat.Format1bppIndexed:
							for (i = 0; i < width; i += 8) {
								for (b = 7; b >= 0; b--) {
									indexed = pal.Entries[(srcOffset[0] >> b) & 0x01];
									dstOffset[0] = indexed.B;
									dstOffset[1] = indexed.G;
									dstOffset[2] = indexed.R;
									dstOffset[3] = 255;
									dstOffset += 4;
								}
								srcOffset += 1;
							}
							srcOffset += 1;
							if ((width % 8) != 0) {
								dstOffset -= 4 * (8 - (width % 8));
							}
							break;
						default:
							if ((int)srcBitmap.PixelFormat == 8207) {
								for (i = 0; i < width; i++) {
									c = (float)srcOffset[0] / (float)255.0;
									m = (float)srcOffset[1] / (float)255.0;
									y = (float)srcOffset[2] / (float)255.0;
									k = (float)srcOffset[3] / (float)255.0;
									dstOffset[0] = 128;// (byte)(255 * (1.0 - y) * (1.0 - k));
									dstOffset[1] = 128;// (byte)(255 * (1.0 - m) * (1.0 - k));
									dstOffset[2] = 128;// (byte)(255 * (1.0 - c) * (1.0 - k));
									dstOffset[3] = 255;
									srcOffset += 4;
									dstOffset += 4;
								}
							}
							break;
					}
				});
			}
			srcBitmap.UnlockBits(srcData);
			dstBitmap.UnlockBits(dstData);
			return dstBitmap;
		}

		public static int[][] Histogram(Bitmap srcBitmap) {
			Rectangle area = new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height);
			return Histogram(srcBitmap, area);
		}

		public static int[][] Histogram(Bitmap srcBitmap, Rectangle area) {
			int[][] hist;
			int bpp = 4;
			int stride;

			Debug.Assert((Image.GetPixelFormatSize(srcBitmap.PixelFormat) == 32));
			hist = new int[3][];
			hist[0] = new int[256];
			hist[1] = new int[256];
			hist[2] = new int[256];
			int i, j;
			BitmapData srcData = srcBitmap.LockBits(new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			stride = srcBitmap.Width * bpp;

			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* lineOffset, pixelOffset;

				for (j = area.Y; j < area.Bottom; j++) {
					lineOffset = srcPtr + (j * stride);
					pixelOffset = lineOffset + (area.X * bpp);
					for (i = area.X; i < area.Right; i++) {
						hist[0][pixelOffset[0]]++;    // Blue
						hist[1][pixelOffset[1]]++;    // Green
						hist[2][pixelOffset[2]]++;    // Red
						pixelOffset += 4;
					}
				}
			}
			srcBitmap.UnlockBits(srcData);
			return hist;
		}

		public static int[] HistogramMono(Bitmap srcBitmap) {
			Rectangle area = new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height);
			return HistogramMono(srcBitmap, area);
		}

		// assumes image is already grayscale
		public static int[] HistogramMono(Bitmap srcBitmap, Rectangle area) {
			int[] hist;
			int bpp = 4;
			int stride;
			int i, j;

			Debug.Assert((Image.GetPixelFormatSize(srcBitmap.PixelFormat) == 32));
			Debug.Assert((ImageTag)srcBitmap.GetImageTag() == ImageTag.MONO);
			hist = new int[256];
			BitmapData srcData = srcBitmap.LockBits(new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			stride = srcBitmap.Width * bpp;

			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* lineOffset, pixelOffset;
				for (j = area.Y; j < area.Bottom; j++) {
					lineOffset = srcPtr + (j * stride);
					pixelOffset = lineOffset + (area.X * bpp);
					for (i = area.X; i < area.Right; i++) {
						//hist[0][pixelOffset[0]]++;    // Blue
						hist[pixelOffset[1]]++;    // Green
												   //hist[2][pixelOffset[2]]++;    // Red
						pixelOffset += 4;
					}
				}
			}
			srcBitmap.UnlockBits(srcData);
			return hist;
		}

		public static int[] HistogramMono(byte[,] inMap) {
			int width, height;
			width = inMap.GetLength(1);
			height = inMap.GetLength(0);
			Rectangle area = new Rectangle(0, 0, width, height);
			return HistogramMono(inMap, area);
		}

		public static int[] HistogramMono(byte[,] inMap, Rectangle area) {
			int[] hist;

			hist = new int[256];
			int j, k;

			for (j = area.Y; j < area.Bottom; j++) {
				for (k = area.X; k < area.Right; k++) {
					hist[inMap[j, k]]++;    // Green
				}
			}
			return hist;
		}

		public static byte[][,] BitmapToArray(Bitmap bmp) {
			int i, j, width, height;
			width = bmp.Width;
			height = bmp.Height;

			Debug.Assert((Image.GetPixelFormatSize(bmp.PixelFormat) == 32));
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			byte[][,] map = new byte[4][,];
			for (i = 0; i < 4; i++) {
				map[i] = new byte[height, width];
			}
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* line = srcPtr;
				for (j = 0; j < height; j++) {
					for (i = 0; i < width; i++) {
						map[0][j, i] = line[0];   // Blue
						map[1][j, i] = line[1];   // Green
						map[2][j, i] = line[2];   // Red
						map[3][j, i] = 255;   // Alpha
						line += 4;
					}
				}
			}
			bmp.UnlockBits(srcData);
			return map;
		}

		public static byte[][,] BitmapToArrayA(Bitmap bmp) {
			int i, j, width, height;
			width = bmp.Width;
			height = bmp.Height;

			Debug.Assert((Image.GetPixelFormatSize(bmp.PixelFormat) == 32));
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			byte[][,] map = new byte[4][,];
			for (i = 0; i < 4; i++) {
				map[i] = new byte[height, width];
			}
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* line = srcPtr;
				for (j = 0; j < height; j++) {
					for (i = 0; i < width; i++) {
						map[0][j, i] = line[0];   // Blue
						map[1][j, i] = line[1];   // Green
						map[2][j, i] = line[2];   // Red
						map[3][j, i] = line[3];   // Alpha
						line += 4;
					}
				}
			}
			bmp.UnlockBits(srcData);
			return map;
		}

		public static byte[][,] BitmapToArrayA(Bitmap bmp, int padding) {
			int i, j, width, height;
			width = bmp.Width;
			height = bmp.Height;

			Debug.Assert((Image.GetPixelFormatSize(bmp.PixelFormat) == 32));
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			byte[][,] map = new byte[4][,];
			for (i = 0; i < 4; i++) {
				map[i] = new byte[height + padding * 2, width + padding * 2];
			}
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* line = srcPtr;
				for (j = padding; j < height + padding; j++) {
					for (i = padding; i < width + padding; i++) {
						map[0][j, i] = line[0];   // Blue
						map[1][j, i] = line[1];   // Green
						map[2][j, i] = line[2];   // Red
						map[3][j, i] = line[3];   // Alpha
						line += 4;
					}
				}
			}
			bmp.UnlockBits(srcData);
			return map;
		}

		public static byte[][,] BitmapToArrayAMT(Bitmap bmp) {
			int i, width, height;
			width = bmp.Width;
			height = bmp.Height;

			Debug.Assert((Image.GetPixelFormatSize(bmp.PixelFormat) == 32));
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			byte[][,] map = new byte[4][,];
			for (i = 0; i < 4; i++) {
				map[i] = new byte[height, width];
			}
			ParallelOptions options = new ParallelOptions();
			options.MaxDegreeOfParallelism = Threads;
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				Parallel.For(0, height - 1, options, j => {
					//for (j = 0; j < height; j++) {
					byte* line = srcPtr + j * width * 4;
					for (int k = 0; k < width; k++) {
						map[0][j, k] = line[0];   // Blue
						map[1][j, k] = line[1];   // Green
						map[2][j, k] = line[2];   // Red
						map[3][j, k] = line[3];   // Alpha
						line += 4;
					}
				});
			}
			bmp.UnlockBits(srcData);
			return map;
		}

		public static byte[][,] BitmapToArrayAMT(Bitmap bmp, int padding) {
			int i, width, height;
			width = bmp.Width;
			height = bmp.Height;

			Debug.Assert((Image.GetPixelFormatSize(bmp.PixelFormat) == 32));
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			byte[][,] map = new byte[4][,];
			for (i = 0; i < 4; i++) {
				map[i] = new byte[height + padding * 2, width + padding * 2];
			}
			ParallelOptions options = new ParallelOptions();
			options.MaxDegreeOfParallelism = Threads;
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				Parallel.For(padding, height + padding - 1, options, j => {
					//for (j = 0; j < height; j++) {
					byte* line = srcPtr + j * width * 4;
					for (int k = padding; k < width + padding; k++) {
						map[0][j, k] = line[0];   // Blue
						map[1][j, k] = line[1];   // Green
						map[2][j, k] = line[2];   // Red
						map[3][j, k] = line[3];   // Alpha
						line += 4;
					}
				});
			}
			bmp.UnlockBits(srcData);
			return map;
		}


		public static byte[,] BitmapToArrayMono(Bitmap bmp) {
			int i, j, width, height;
			width = bmp.Width;
			height = bmp.Height;
			int r, g, b, y;

			Debug.Assert((Image.GetPixelFormatSize(bmp.PixelFormat) == 32));
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			byte[,] map = new byte[height, width];
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* line = srcPtr;
				if (bmp.GetImageTag() == ImageTag.MONO) {
					for (j = 0; j < height; j++) {
						for (i = 0; i < width; i++) {
							g = line[1];
							map[j, i] = (byte)g;
							line += 4;
						}
					}
				} else {
					for (j = 0; j < height; j++) {
						for (i = 0; i < width; i++) {
							b = line[0];
							g = line[1];
							r = line[2];
							//y = (299 * r + 587 * g + 114 * b) / 1000;
							y = (306 * r + 601 * g + 117 * b) >> 10;
							map[j, i] = (byte)y;
							line += 4;
						}
					}
				}
			}
			bmp.UnlockBits(srcData);
			return map;
		}

		public static byte[][][] BitmapToJaggedArray(Bitmap bmp) {
			int i, j, width, height;
			width = bmp.Width;
			height = bmp.Height;

			Debug.Assert((Image.GetPixelFormatSize(bmp.PixelFormat) == 32));
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			byte[][][] map = new byte[3][][];
			byte[] bRow, gRow, rRow;
			for (i = 0; i < 4; i++) {
				map[i] = new byte[height][];
				for (j = 0; j < height; j++) {
					map[i][j] = new byte[width];
				}
			}
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* line = srcPtr;
				for (j = 0; j < height; j++) {
					bRow = map[0][j];
					gRow = map[1][j];
					rRow = map[2][j];
					for (i = 0; i < width; i++) {
						bRow[i] = line[0];   // Blue
						gRow[i] = line[1];   // Green
						rRow[i] = line[2];   // Red
						rRow[i] = line[3];   // Alpha
						line += 4;
					}
				}
			}
			bmp.UnlockBits(srcData);
			return map;
		}

		public static byte[][] BitmapToJaggedArrayMono(Bitmap bmp) {
			int i, j, width, height;
			width = bmp.Width;
			height = bmp.Height;
			byte[][] map;
			byte[] gRow;
			int r, g, b, y;

			Debug.Assert((Image.GetPixelFormatSize(bmp.PixelFormat) == 32));
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			map = new byte[height][];
			for (j = 0; j < height; j++) {
				map[j] = new byte[width];
			}
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* line = srcPtr;
				if (bmp.GetImageTag() == ImageTag.MONO) {
					for (j = 0; j < height; j++) {
						gRow = map[j];
						for (i = 0; i < width; i++) {
							gRow[i] = line[1];   // Green
							line += 4;
						}
					}
				} else {
					for (j = 0; j < height; j++) {
						gRow = map[j];
						for (i = 0; i < width; i++) {
							b = line[0];
							g = line[1];
							r = line[2];
							//y = (299 * r + 587 * g + 114 * b) / 1000;
							y = (306 * r + 601 * g + 117 * b) >> 10;
							gRow[i] = (byte)y;   // Green
							line += 4;
						}
					}
				}
			}
			bmp.UnlockBits(srcData);
			return map;
		}

		public static byte[][,] CreateArray(int x, int y) {
			byte[][,] map = new byte[4][,];
			map[0] = new byte[y, x];
			map[1] = new byte[y, x];
			map[2] = new byte[y, x];
			map[3] = new byte[y, x];
			return map;
		}

		public static Bitmap? ArrayToBitmap(byte[][,]? map) {
			int i, j, width, height;
			if (map == null) return null;
			width = map[0].GetLength(1);
			height = map[0].GetLength(0);
			Bitmap bmp = new Bitmap(width, height);
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* line = srcPtr;
				for (j = 0; j < height; j++) {
					for (i = 0; i < width; i++) {
						line[0] = map[0][j, i];   // Blue
						line[1] = map[1][j, i];   // Green
						line[2] = map[2][j, i];   // Red
						line[3] = 255;            // Alpha
						line += 4;
					}
				}
			}
			bmp.UnlockBits(srcData);
			bmp.SetImageTag(ImageTag.RGB);
			return bmp;
		}

		public static Bitmap? ArrayToBitmap(byte[,]? map) {
			int i, j, width, height;
			if (map == null) return null;
			width = map.GetLength(1);
			height = map.GetLength(0);
			Bitmap bmp = new Bitmap(width, height);
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* line = srcPtr;
				for (j = 0; j < height; j++) {
					for (i = 0; i < width; i++) {
						line[0] = map[j, i];   // Blue
						line[1] = map[j, i];   // Green
						line[2] = map[j, i];   // Red
						line[3] = 255;            // Alpha
						line += 4;
					}
				}
			}
			bmp.UnlockBits(srcData);
			bmp.SetImageTag(ImageTag.MONO);
			return bmp;
		}

		public static Bitmap? ArrayToBitmapA(byte[][,]? map) {
			int width = map[0].GetLength(1);
			int height = map[0].GetLength(0);
			Size bmpSize = new Size(width, height);
			return ArrayToBitmapA(map, bmpSize, new Point(0, 0));
		}

		public static Bitmap? ArrayToBitmapA(byte[][,]? map, Size bmpSize, Point offset) {
			int i, j, width, height;
			if (map == null) return null;
			width = map[0].GetLength(1);
			height = map[0].GetLength(0);
			Bitmap bmp = new Bitmap(bmpSize.Width, bmpSize.Height);
			//BitmapData srcData = bmp.LockBits(new Rectangle(offset.X, offset.Y, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, bmpSize.Width, bmpSize.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				//byte* line = srcPtr;
				for (j = 0; j < height; j++) {
					byte* line = srcPtr + 4 * ((j + offset.Y) * bmpSize.Width + offset.X);
					for (i = 0; i < width; i++) {
						line[0] = map[0][j, i];   // Blue
						line[1] = map[1][j, i];   // Green
						line[2] = map[2][j, i];   // Red
						line[3] = map[3][j, i];   // Alpha
						line += 4;
					}
				}
			}
			bmp.UnlockBits(srcData);
			bmp.SetImageTag(ImageTag.RGB);
			return bmp;
		}

		public static Bitmap? JaggedArrayToBitmap(byte[][][]? map) {
			int i, j, width, height;
			if (map == null) return null;
			width = map[0][0].Length;
			height = map[0].Length;
			Bitmap bmp = new Bitmap(width, height);
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			byte[] bRow, gRow, rRow;
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* offset = srcPtr;
				for (j = 0; j < height; j++) {
					bRow = map[0][j];
					gRow = map[1][j];
					rRow = map[2][j];
					for (i = 0; i < width; i++) {
						offset[0] = bRow[i];    // Blue
						offset[1] = gRow[i];    // Green
						offset[2] = rRow[i];    // Red
						offset[3] = 255;        // Alpha
						offset += 4;
					}
				}
			}
			bmp.UnlockBits(srcData);
			bmp.SetImageTag(ImageTag.RGB);
			return bmp;
		}

		public static void SetImageAlpha(Bitmap bmp, byte alpha) {
			int i, j, width, height;
			width = bmp.Width;
			height = bmp.Height;

			Debug.Assert((Image.GetPixelFormatSize(bmp.PixelFormat) == 32));
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* offset = srcPtr;
				for (j = 0; j < height; j++) {
					for (i = 0; i < width; i++) {
						//map[0][j, i] = offset[0];   // Blue
						//map[1][j, i] = offset[1];   // Green
						//map[2][j, i] = offset[2];   // Red
						offset[3] = alpha;
						offset += 4;
					}
				}
			}
			bmp.UnlockBits(srcData);
		}

		public static void SetImageAlpha(Bitmap bmp, byte alpha, RectangleF area) {
			Rectangle intArea = new Rectangle();
			intArea.X = (int)Math.Floor(area.X);
			intArea.Y = (int)Math.Floor(area.Y);
			intArea.Width = (int)Math.Ceiling(area.Width);
			intArea.Height = (int)Math.Ceiling(area.Height);
			SetImageAlpha(bmp, alpha, intArea);
		}

		public static void SetImageAlpha(Bitmap bmp, byte alpha, Rectangle area) {
			int i, j, width, height;
			width = bmp.Width;
			height = bmp.Height;

			Debug.Assert((Image.GetPixelFormatSize(bmp.PixelFormat) == 32));
			BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* offset = srcPtr;
				for (j = area.Top; j <= area.Bottom; j++) {
					offset = srcPtr + ((j * width) + area.Left) * 4;
					for (i = area.Left; i <= area.Right; i++) {
						offset[3] = alpha;
						offset += 4;
					}
				}
			}
			bmp.UnlockBits(srcData);
		}


		public static byte[][,] Overlay(byte[][,] srcMapPrimary, byte[][,] srcMapSecondary) {
			int j, k;
			int width, height;
			width = srcMapPrimary[0].GetLength(1);
			height = srcMapPrimary[0].GetLength(0);
			byte[][,] dstMap = new byte[4][,];
			for (int c = 0; c < 4; c++) {
				dstMap[c] = new byte[width, height];
			}
			int alphaPri, alphaSec;
			for (j = 0; j < height; j++) {
				for (k = 0; k < width; k++) {
					alphaSec = srcMapSecondary[3][j, k];
					alphaPri = 255 - alphaSec;
					dstMap[0][j, k] = (byte)((srcMapPrimary[0][j,k] * alphaPri + srcMapSecondary[0][j, k] * alphaSec) / 255);
					dstMap[1][j, k] = (byte)((srcMapPrimary[1][j, k] * alphaPri + srcMapSecondary[1][j, k] * alphaSec) / 255);
					dstMap[2][j, k] = (byte)((srcMapPrimary[2][j, k] * alphaPri + srcMapSecondary[2][j, k] * alphaSec) / 255);
					dstMap[3][j, k] = 255;
				}
			}
			return dstMap;
		}

		public static Bitmap Overlay(Bitmap srcBitmapPrimary, Bitmap srcBitmapSecondary) {
			int i, j;
			int width, height;
			width = srcBitmapPrimary.Width;
			height = srcBitmapPrimary.Height;
			Bitmap dstBitmap = new Bitmap(srcBitmapPrimary);
			int alphaPri, alphaSec;

			Debug.Assert((Image.GetPixelFormatSize(srcBitmapPrimary.PixelFormat) == 32));
			Debug.Assert((Image.GetPixelFormatSize(srcBitmapSecondary.PixelFormat) == 32));
			BitmapData srcDataPri = srcBitmapPrimary.LockBits(new Rectangle(0, 0, srcBitmapPrimary.Width, srcBitmapPrimary.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			BitmapData srcDataSec = srcBitmapSecondary.LockBits(new Rectangle(0, 0, srcBitmapSecondary.Width, srcBitmapSecondary.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			BitmapData dstData = dstBitmap.LockBits(new Rectangle(0, 0, dstBitmap.Width, dstBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			unsafe {
				byte* srcPtrPri = (byte*)srcDataPri.Scan0;
				byte* srcPtrSec = (byte*)srcDataSec.Scan0;
				byte* dstPtr = (byte*)dstData.Scan0;
				byte* srcOffsetPri, srcOffsetSec;
				byte* dstOffset;

				srcOffsetPri = srcPtrPri;
				srcOffsetSec = srcPtrSec;
				dstOffset = dstPtr;
				for (j = 0; j < height; j++) {
					for (i = 0; i < width; i++) {
						alphaSec = srcOffsetSec[3];
						alphaPri = 255 - alphaSec;
						dstOffset[0] = (byte)((srcOffsetPri[0] * alphaPri + srcOffsetSec[0] * alphaSec) / 255);
						dstOffset[1] = (byte)((srcOffsetPri[1] * alphaPri + srcOffsetSec[1] * alphaSec) / 255);
						dstOffset[2] = (byte)((srcOffsetPri[2] * alphaPri + srcOffsetSec[2] * alphaSec) / 255);
						dstOffset[3] = 255;
						srcOffsetPri += 4;
						srcOffsetSec += 4;
						dstOffset += 4;
					}
				}
			}
			srcBitmapPrimary.UnlockBits(srcDataPri);
			srcBitmapSecondary.UnlockBits(srcDataSec);
			dstBitmap.UnlockBits(dstData);
			if (srcBitmapPrimary.GetImageTag() != ImageTag.UNDEF) dstBitmap.SetImageTag(srcBitmapPrimary.GetImageTag());
			else dstBitmap.SetImageTag(ImageTag.RGB);
			return dstBitmap;
		}

		public static byte[][,] NearestNeighbor(Rectangle destRect, RectangleF sourceRect, byte[][,] inMap) {
			byte[][,] outMap = new byte[4][,];
			int outW = destRect.Width;
			int outH = destRect.Height;
			float inW = sourceRect.Width;
			float inH = sourceRect.Height;
			float xRatio = (float)inW / (float)outW;
			float yRatio = (float)inH / (float)outH;
			int sX, sY;
			for (int c = 0; c < 4; c++) {
				outMap[c] = new byte[outH, outW];
			}
			for (int y = 0; y < outH; y++) {
				sY = (int)((float)y * yRatio + sourceRect.Y);
				for (int x = 0; x < outW; x++) {
					sX = (int)((float)x * xRatio + sourceRect.X);
					for (int c = 0; c < 4; c++) {
						outMap[c][y, x] = inMap[c][sY, sX];
					}
				}
			}
			return outMap;
		}

		public static byte[][,] NearestNeighborMT(Rectangle destRect, RectangleF sourceRect, byte[][,] inMap) {
			byte[][,] outMap = new byte[4][,];
			int outW = destRect.Width;
			int outH = destRect.Height;
			float inW = sourceRect.Width;
			float inH = sourceRect.Height;
			float xRatio = (float)inW / (float)outW;
			float yRatio = (float)inH / (float)outH;
			for (int c = 0; c < 4; c++) {
				outMap[c] = new byte[outH, outW];
			}
			ParallelOptions options = new ParallelOptions();
			options.MaxDegreeOfParallelism = Threads;
			Parallel.For(0, outH, options, y => {
				int sX, sY;
				sY = (int)((float)y * yRatio + sourceRect.Y);
				for (int x = 0; x < outW; x++) {
					sX = (int)((float)x * xRatio + sourceRect.X);
					for (int c = 0; c < 4; c++) {
						outMap[c][y, x] = inMap[c][sY, sX];
					}
				}
			});
			return outMap;
		}

		private class BilinearScaler {
			const int BPP = 4;
			private static float Lerp(float s, float e, float t) {
				return s + (e - s) * t;
			}

			private static float Blerp(float c00, float c10, float c01, float c11, float tx, float ty) {
				return Lerp(Lerp(c00, c10, tx), Lerp(c01, c11, tx), ty);
			}

			unsafe private static Color GetPixel(byte* source, int stride, int x, int y) {
				byte* offset = source + (y * stride) + x * BPP;
				Color rval = Color.FromArgb(offset[3], offset[2], offset[1], offset[0]);
				return rval;
			}

			unsafe private static void SetPixel(byte* destination, int stride, int x, int y, Color pxl) {
				byte* offset = destination + (y * stride) + x * BPP;
				offset[0] = pxl.B;
				offset[1] = pxl.G;
				offset[2] = pxl.R;
				offset[3] = pxl.A;
			}

			unsafe public static Bitmap BilinearResize(Bitmap srcBitmap, float scaleX, float scaleY) {
				int newWidth = (int)(srcBitmap.Width * scaleX);
				int newHeight = (int)(srcBitmap.Height * scaleY);
				Bitmap dstBitmap = new Bitmap(newWidth, newHeight);

				Debug.Assert((Image.GetPixelFormatSize(srcBitmap.PixelFormat) == 32));
				BitmapData srcData = srcBitmap.LockBits(new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
				BitmapData dstData = dstBitmap.LockBits(new Rectangle(0, 0, dstBitmap.Width, dstBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
				int srcStride = srcData.Stride;
				int dstStride = dstData.Stride;

				unsafe {
					byte* srcPtr = (byte*)srcData.Scan0;
					byte* dstPtr = (byte*)dstData.Scan0;
					for (int y = 0; y < newHeight; y++) {
						for (int x = 0; x < newWidth; x++) {
							float gx = ((float)x) / newWidth * (srcBitmap.Width - 1);
							float gy = ((float)y) / newHeight * (srcBitmap.Height - 1);
							int gxi = (int)gx;
							int gyi = (int)gy;
							Color c00 = BilinearScaler.GetPixel(srcPtr, srcStride, gxi, gyi);
							Color c10 = BilinearScaler.GetPixel(srcPtr, srcStride, gxi + 1, gyi);
							Color c01 = BilinearScaler.GetPixel(srcPtr, srcStride, gxi, gyi + 1);
							Color c11 = BilinearScaler.GetPixel(srcPtr, srcStride, gxi + 1, gyi + 1);

							int red = (int)Blerp(c00.R, c10.R, c01.R, c11.R, gx - gxi, gy - gyi);
							int green = (int)Blerp(c00.G, c10.G, c01.G, c11.G, gx - gxi, gy - gyi);
							int blue = (int)Blerp(c00.B, c10.B, c01.B, c11.B, gx - gxi, gy - gyi);
							Color rgb = Color.FromArgb(red, green, blue);
							BilinearScaler.SetPixel(dstPtr, dstStride, x, y, rgb);
						}
					}
				}
				srcBitmap.UnlockBits(srcData);
				dstBitmap.UnlockBits(dstData);
				if (srcBitmap.GetImageTag() != ImageTag.UNDEF) dstBitmap.SetImageTag(srcBitmap.GetImageTag());
				else dstBitmap.SetImageTag(ImageTag.RGB);
				return dstBitmap;
			}
		}

/*		public static float BiCubicKernel(float x) {
			float bicubicCoef = 0;
			if (x < 0) x = -x;
			if (x <= 1) bicubicCoef = (float)((1.5 * x - 2.5) * x * x + 1);
			else if (x < 2) bicubicCoef = (float)(((-0.5 * x + 2.5) * x - 4) * x + 2);
			return bicubicCoef;
		}

		const int BICUBIC_PADDING = 2;
		public static byte[][,] ScaleBiCubic(Rectangle destRect, RectangleF sourceRect, byte[][,] inMap) {
			byte[][,] outMap = new byte[4][,];
			int outW = destRect.Width;
			int outH = destRect.Height;
			float inW = sourceRect.Width;
			float inH = sourceRect.Height;
			float xRatio = (float)inW / (float)outW;
			float yRatio = (float)inH / (float)outH;
			float fX, fY;
			int iX, iY;
			float[] dX = new float[4];
			float[] dY = new float[4];
			float[] pxl = new float[16];
			float fpxl;
			for (int ch = 0; ch < 4; ch++) {
				outMap[ch] = new byte[outH, outW];
			}
			Matrix<float> left, right, middle, dot1, dot2;
			right = Matrix<float>.Build.Dense(4, 1, dY);
			left = Matrix<float>.Build.Dense(1, 4, dX);
			middle = Matrix<float>.Build.Dense(4, 4, pxl);
			for (int y = 0; y < outH; y++) {
				fY = ((float)y * yRatio + sourceRect.Y) + BICUBIC_PADDING;
				iY = (int)fY;
				dY[0] = BiCubicKernel(fY - iY + 1);
				dY[1] = BiCubicKernel(fY - iY);
				dY[2] = BiCubicKernel(iY - fY + 1);
				dY[3] = BiCubicKernel(iY - fY + 2);
				for (int x = 0; x < outW; x++) {
					fX = ((float)x * xRatio + sourceRect.X) + BICUBIC_PADDING;
					iX = (int)fX;
					dX[0] = BiCubicKernel(fX - iX + 1);
					dX[1] = BiCubicKernel(fX - iX);
					dX[2] = BiCubicKernel(iX - fX + 1);
					dX[3] = BiCubicKernel(iX - fX + 2);
					for (int ch = 0; ch < 4; ch++) {
						pxl[0] = inMap[ch][iY - 1, iX - 1];
						pxl[1] = inMap[ch][iY - 1, iX - 0];
						pxl[2] = inMap[ch][iY - 1, iX + 1];
						pxl[3] = inMap[ch][iY - 1, iX + 2];
						pxl[4] = inMap[ch][iY - 0, iX - 1];
						pxl[5] = inMap[ch][iY - 0, iX - 0];
						pxl[6] = inMap[ch][iY - 0, iX + 1];
						pxl[7] = inMap[ch][iY - 0, iX + 2];
						pxl[8] = inMap[ch][iY + 1, iX - 1];
						pxl[9] = inMap[ch][iY + 1, iX - 0];
						pxl[10] = inMap[ch][iY + 1, iX + 1];
						pxl[11] = inMap[ch][iY + 1, iX + 2];
						pxl[12] = inMap[ch][iY + 2, iX - 1];
						pxl[13] = inMap[ch][iY + 2, iX - 0];
						pxl[14] = inMap[ch][iY + 2, iX + 1];
						pxl[15] = inMap[ch][iY + 2, iX + 2];
						dot1 = left * middle;
						dot2 = dot1 * right;
						fpxl = dot2.At(0, 0);
						if (fpxl > 255) fpxl = 255;
						else if (fpxl < 0) fpxl = 0;
						outMap[ch][y, x] = (byte)fpxl;
					}
				}
			}
			return outMap;
		}

		public static byte[][,] ScaleBiCubicMT(Rectangle destRect, RectangleF sourceRect, byte[][,] inMap) {
			byte[][,] outMap = new byte[4][,];
			int outW = destRect.Width;
			int outH = destRect.Height;
			float inW = sourceRect.Width;
			float inH = sourceRect.Height;
			float xRatio = (float)inW / (float)outW;
			float yRatio = (float)inH / (float)outH;
			for (int ch = 0; ch < 4; ch++) {
				outMap[ch] = new byte[outH, outW];
			}
			ParallelOptions options = new ParallelOptions();
			options.MaxDegreeOfParallelism = Threads;
			Parallel.For(0, outH, options, y => {
				//for (int y = 0; y < outH; y++) {
				float fX, fY;
				int iX, iY;
				float[] dX = new float[4];
				float[] dY = new float[4];
				float[] pxl = new float[16];
				float fpxl;
				Matrix<float> left, right, middle, dot1, dot2;
				right = Matrix<float>.Build.Dense(4, 1, dY);
				left = Matrix<float>.Build.Dense(1, 4, dX);
				middle = Matrix<float>.Build.Dense(4, 4, pxl);
				fY = ((float)y * yRatio + sourceRect.Y) + BICUBIC_PADDING;
				iY = (int)fY;
				dY[0] = BiCubicKernel(fY - iY + 1);
				dY[1] = BiCubicKernel(fY - iY);
				dY[2] = BiCubicKernel(iY - fY + 1);
				dY[3] = BiCubicKernel(iY - fY + 2);
				for (int x = 0; x < outW; x++) {
					fX = ((float)x * xRatio + sourceRect.X) + BICUBIC_PADDING;
					iX = (int)fX;
					dX[0] = BiCubicKernel(fX - iX + 1);
					dX[1] = BiCubicKernel(fX - iX);
					dX[2] = BiCubicKernel(iX - fX + 1);
					dX[3] = BiCubicKernel(iX - fX + 2);
					for (int ch = 0; ch < 4; ch++) {
						pxl[0] = inMap[ch][iY - 1, iX - 1];
						pxl[1] = inMap[ch][iY - 1, iX - 0];
						pxl[2] = inMap[ch][iY - 1, iX + 1];
						pxl[3] = inMap[ch][iY - 1, iX + 2];
						pxl[4] = inMap[ch][iY - 0, iX - 1];
						pxl[5] = inMap[ch][iY - 0, iX - 0];
						pxl[6] = inMap[ch][iY - 0, iX + 1];
						pxl[7] = inMap[ch][iY - 0, iX + 2];
						pxl[8] = inMap[ch][iY + 1, iX - 1];
						pxl[9] = inMap[ch][iY + 1, iX - 0];
						pxl[10] = inMap[ch][iY + 1, iX + 1];
						pxl[11] = inMap[ch][iY + 1, iX + 2];
						pxl[12] = inMap[ch][iY + 2, iX - 1];
						pxl[13] = inMap[ch][iY + 2, iX - 0];
						pxl[14] = inMap[ch][iY + 2, iX + 1];
						pxl[15] = inMap[ch][iY + 2, iX + 2];
						dot1 = left * middle;
						dot2 = dot1 * right;
						fpxl = dot2.At(0, 0);
						if (fpxl > 255) fpxl = 255;
						else if (fpxl < 0) fpxl = 0;
						outMap[ch][y, x] = (byte)fpxl;
					}
				}
			});
			return outMap;
		}*/

		const int Prec = 20;    // Default = 20 ( >= 19 for no err)

		// Adds MemOpt support to EM/EM2
		public static byte[,]? EmulatedResizeMono(byte[,] inputArray, Size outputSize, bool round, bool memOpt) {
			if (memOpt) {
				byte[,] binnedArray = Bin2x2(inputArray);
				return EmulatedResizeMono(binnedArray, outputSize, round);
			} else {
				return EmulatedResizeMono(inputArray, outputSize, round);
			}
		}

		// Supports both EM (rounding) and EM2 (truncation)
		public static byte[,]? EmulatedResizeMono(byte[,] inputArray, Size outputSize, bool round) {
			int i, j, k;
			// calculate sizes and setup arrays
			int[] xExtend, yExtend;
			int xFactor, yFactor;
			// float division without F suffix may be a bit off, e.g 16.0 / 0.1 = 159.999999
			Size inputSize = new Size(inputArray.GetLength(1), inputArray.GetLength(0));
			float scaleX = (float)outputSize.Width / (float)inputSize.Width;
			float scaleY = (float)outputSize.Height / (float)inputSize.Height;
			xFactor = (int)(16.0F / scaleX);
			yFactor = (int)(16.0F / scaleY);
			if ((inputSize.Width == 0) || (inputSize.Height == 0) || (outputSize.Width == 0) || (outputSize.Height == 0)) {
				throw new ImageSizeException("EM Resize: Input/Output size is 0");
			} else if ((inputSize.Width < outputSize.Width) && (inputSize.Height < outputSize.Height)) {
				throw new ImageSizeException("EM Resize: Upscale not supported");
			} else if ((inputSize.Width == outputSize.Width) && (inputSize.Height == outputSize.Height)) {
				return inputArray;
			}

			int ptr;
			int iw, ih, ow, oh;
			iw = inputSize.Width;
			ih = inputSize.Height;
			ow = outputSize.Width;
			oh = outputSize.Height;
			xExtend = new int[iw * 16];
			yExtend = new int[ih * 16];
			int acc;
			Point[] xlims, ylims;
			int extIdx, begIdx, endIdx;
			int fyfactor = (1 << Prec) / yFactor;
			int fxfactor = (1 << Prec) / xFactor;

			for (i = 0; i < iw; i++) {
				for (j = 0; j < 16; j++) {
					xExtend[i * 16 + j] = i;
				}
			}
			for (i = 0; i < ih; i++) {
				for (j = 0; j < 16; j++) {
					yExtend[i * 16 + j] = i;
				}
			}
			byte[,] hbufArray = new byte[ih, ow];
			byte[,] outputArray = new byte[oh, ow];
			// pre-calculate coefficient weights using histogram method
			byte[][] xCoefArray = new byte[ow][];
			byte[][] yCoefArray = new byte[oh][];
			xlims = new Point[iw];
			ylims = new Point[ih];
			for (i = 0; i < oh; i++) {
				yCoefArray[i] = new byte[ih];
			}
			ptr = 0;
			for (i = 0; i < ow; i++) {
				begIdx = iw; endIdx = -1;
				xCoefArray[i] = new byte[iw];
				for (j = 0; j < xFactor; j++) {
					if (ptr >= xExtend.Length) break;
					extIdx = xExtend[ptr];
					if (begIdx > extIdx) begIdx = extIdx;
					if (extIdx > endIdx) endIdx = extIdx;
					xCoefArray[i][extIdx]++;
					ptr++;
				}
				xlims[i] = new Point(begIdx, endIdx);
			}
			ptr = 0;
			for (i = 0; i < oh; i++) {
				begIdx = iw; endIdx = -1;
				yCoefArray[i] = new byte[ih];
				for (j = 0; j < yFactor; j++) {
					if (ptr >= yExtend.Length) break;
					extIdx = yExtend[ptr];
					if (begIdx > extIdx) begIdx = extIdx;
					if (extIdx > endIdx) endIdx = extIdx;
					yCoefArray[i][extIdx]++;
					ptr++;
				}
				ylims[i] = new Point(begIdx, endIdx);
			}
			// scale the image horizontally
			for (j = 0; j < ih; j++) {
				for (k = 0; k < ow; k++) {
					acc = 0;
					//lim = xCoefArray[k].Length;
					for (i = xlims[k].X; i <= xlims[k].Y; i++) {
						acc += inputArray[j, i] * xCoefArray[k][i];
					}
					if (round) {
						hbufArray[j, k] = (byte)Math.Round((((float)acc / (float)xFactor) + 0.125), MidpointRounding.AwayFromZero);
					} else {
						//hbufArray[j, k] = (byte)Math.Round(((float)acc / (float)xFactor), MidpointRounding.AwayFromZero);
						hbufArray[j, k] = (byte)(((acc * fxfactor) + (1 << (Prec - 1))) >> Prec);
					}
				}
			}
			// scale the image vertically
			for (k = 0; k < ow; k++) {
				for (j = 0; j < oh; j++) {
					acc = 0;
					//lim = yCoefArray[j].Length;
					for (i = ylims[j].X; i <= ylims[j].Y; i++) {
						acc += hbufArray[i, k] * yCoefArray[j][i];
					}
					if (round) {
						outputArray[j, k] = (byte)Math.Round((((float)acc / (float)yFactor) + 0.125), MidpointRounding.AwayFromZero);
					} else {
						//outputArray[j, k] = (byte)Math.Round(((float)acc / (float)yFactor), MidpointRounding.AwayFromZero);
						outputArray[j, k] = (byte)(((acc * fyfactor) + (1 << (Prec - 1))) >> Prec);
					}
				}
			}
			return outputArray;
		}

		// Emulates EM2 only
		public static byte[,]? EmulatedResizeMonoRTL(byte[,] inputArray, Size outputSize, bool memOpt) {
			int inputWidth, inputHeight;
			float scaleX, scaleY;
			int xFactor, yFactor;

			if (memOpt) {
				byte[,] binnedArray = Bin2x2(inputArray);
				inputWidth = binnedArray.GetLength(1);
				inputHeight = binnedArray.GetLength(0);
				scaleX = (float)outputSize.Width / (float)inputWidth;
				scaleY = (float)outputSize.Height / (float)inputHeight;
				xFactor = (int)Math.Truncate(16.0F / scaleX);
				yFactor = (int)Math.Truncate(16.0F / scaleY);
				return EmulatedResizeMonoRTL(binnedArray, outputSize, xFactor, yFactor);
			} else {
				inputWidth = inputArray.GetLength(1);
				inputHeight = inputArray.GetLength(0);
				scaleX = (float)outputSize.Width / (float)inputWidth;
				scaleY = (float)outputSize.Height / (float)inputHeight;
				xFactor = (int)Math.Truncate(16.0F / scaleX);
				yFactor = (int)Math.Truncate(16.0F / scaleY);
				return EmulatedResizeMonoRTL(inputArray, outputSize, xFactor, yFactor);
			}
		}

		// Emulates EM2 only
		public static byte[,]? EmulatedResizeMonoRTL(byte[,] inputArray, Size outputSize, int xFactor, int yFactor) {
			int i, j, k;
			int[] xExtend, yExtend;
			Size inputSize = new Size(inputArray.GetLength(1), inputArray.GetLength(0));
			if ((inputSize.Width == 0) || (inputSize.Height == 0) || (outputSize.Width == 0) || (outputSize.Height == 0)) return null;
			int ptr;
			int iw, ih, ow, oh;
			iw = inputSize.Width;
			ih = inputSize.Height;
			ow = outputSize.Width;
			oh = outputSize.Height;
			xExtend = new int[iw * 16];
			yExtend = new int[ih * 16];
			int acc, lim;
			int fyfactor = (1 << Prec) / yFactor;
			int fxfactor = (1 << Prec) / xFactor;
			byte[,] hbufArray = new byte[ih, ow];
			byte[,] outputArray = new byte[oh, ow];
			byte[][] xCoefArray = new byte[ow][];
			byte[][] yCoefArray = new byte[oh][];

			for (i = 0; i < iw; i++) {
				for (j = 0; j < 16; j++) {
					xExtend[i * 16 + j] = i;
				}
			}
			for (i = 0; i < ih; i++) {
				for (j = 0; j < 16; j++) {
					yExtend[i * 16 + j] = i;
				}
			}
			for (i = 0; i < oh; i++) {
				yCoefArray[i] = new byte[ih];
			}
			ptr = 0;
			for (i = 0; i < ow; i++) {
				xCoefArray[i] = new byte[iw];
				for (j = 0; j < xFactor; j++) {
					if (ptr >= xExtend.Length) break;
					xCoefArray[i][xExtend[ptr]]++;
					ptr++;
				}
			}
			ptr = 0;
			for (i = 0; i < oh; i++) {
				yCoefArray[i] = new byte[ih];
				for (j = 0; j < yFactor; j++) {
					if (ptr >= yExtend.Length) break;
					yCoefArray[i][yExtend[ptr]]++;
					ptr++;
				}
			}
			// scale the image horizontally
			for (j = 0; j < ih; j++) {
				for (k = 0; k < ow; k++) {
					acc = 0;
					lim = xCoefArray[k].Length;
					for (i = 0; i < lim; i++) {
						acc += inputArray[j, i] * xCoefArray[k][i];
					}
					hbufArray[j, k] = (byte)(((acc * fxfactor) + (1 << (Prec - 1))) >> Prec);
				}
			}
			// scale the image vertically
			for (k = 0; k < ow; k++) {
				for (j = 0; j < oh; j++) {
					acc = 0;
					lim = yCoefArray[j].Length;
					for (i = 0; i < lim; i++) {
						acc += hbufArray[i, k] * yCoefArray[j][i];
					}
					outputArray[j, k] = (byte)(((acc * fyfactor) + (1 << (Prec - 1))) >> Prec);
				}
			}
			return outputArray;
		}

		public class EmulatedResizeBuffers {
			public byte[,]? hbufArray;
			public byte[,]? outputArray;
			public byte[,]? xCoefArray;
			public byte[,]? yCoefArray;
			public Point[]? xlims;
			public Point[]? ylims;
			public int xFactor;
			public int yFactor;
			public int IW;
			public int IH;
			public int OW;
			public int OH;
			public bool round;
		}

		public static EmulatedResizeBuffers? EmulatedResizeSetup(Size inputSize, float scaleX, float scaleY, bool round) {
			int i, j;
			EmulatedResizeBuffers setup = new EmulatedResizeBuffers();
			int[] xExtend, yExtend;
			int xFactor, yFactor;
			// float division without F suffix may be a bit off, e.g 16.0 / 0.1 = 159.999999
			xFactor = (int)Math.Truncate(16.0F / scaleX);
			yFactor = (int)Math.Truncate(16.0F / scaleY);
			Size outputSize = new Size((int)Math.Round(inputSize.Width * scaleX), (int)Math.Round(inputSize.Height * scaleY));
			if ((inputSize.Width == 0) || (inputSize.Height == 0) || (outputSize.Width == 0) || (outputSize.Height == 0)) {
				throw new ImageSizeException("EM Resize: Input/Output size is 0");
			} else if ((inputSize.Width < outputSize.Width) && (inputSize.Height < outputSize.Height)) {
				throw new ImageSizeException("EM Resize: Upscale not supported");
			} else if ((inputSize.Width < outputSize.Width) && (inputSize.Height < outputSize.Height)) {
				return null;
			}
			int ptr;
			int iw, ih, ow, oh;
			iw = inputSize.Width;
			ih = inputSize.Height;
			ow = outputSize.Width;
			oh = outputSize.Height;
			xExtend = new int[iw * 16];
			yExtend = new int[ih * 16];
			int extIdx, begIdx, endIdx;
			Point[] xlims, ylims;

			for (i = 0; i < iw; i++) {
				for (j = 0; j < 16; j++) {
					xExtend[i * 16 + j] = i;
				}
			}
			for (i = 0; i < ih; i++) {
				for (j = 0; j < 16; j++) {
					yExtend[i * 16 + j] = i;
				}
			}
			byte[,] hbufArray = new byte[ih, ow];
			byte[,] outputArray = new byte[oh, ow];
			byte[,] xCoefArray = new byte[ow, iw];
			byte[,] yCoefArray = new byte[oh, ih];
			xlims = new Point[iw];
			ylims = new Point[ih];
			ptr = 0;
			for (i = 0; i < ow; i++) {
				begIdx = iw; endIdx = -1;
				for (j = 0; j < xFactor; j++) {
					if (ptr >= xExtend.Length) break;
					extIdx = xExtend[ptr];
					if (begIdx > extIdx) begIdx = extIdx;
					if (extIdx > endIdx) endIdx = extIdx;
					xCoefArray[i, extIdx]++;
					ptr++;
				}
				xlims[i] = new Point(begIdx, endIdx);
			}
			ptr = 0;
			for (i = 0; i < oh; i++) {
				begIdx = iw; endIdx = -1;
				for (j = 0; j < yFactor; j++) {
					if (ptr >= yExtend.Length) break;
					extIdx = yExtend[ptr];
					if (begIdx > extIdx) begIdx = extIdx;
					if (extIdx > endIdx) endIdx = extIdx;
					yCoefArray[i, extIdx]++;
					ptr++;
				}
				ylims[i] = new Point(begIdx, endIdx);
			}
			setup.IW = iw; setup.IH = ih; setup.OW = ow; setup.OH = oh;
			setup.hbufArray = hbufArray;
			setup.outputArray = outputArray;
			setup.xCoefArray = xCoefArray;
			setup.yCoefArray = yCoefArray;
			setup.xlims = xlims;
			setup.ylims = ylims;
			setup.xFactor = xFactor;
			setup.yFactor = yFactor;
			setup.round = round;
			return setup;
		}

		public static byte[,]? EmulatedResizeMonoSharedBuf(byte[,] inputArray, EmulatedResizeBuffers buffers) {
			int i, j, k;
			int iw = buffers.IW;
			int ih = buffers.IH;
			int ow = buffers.OW;
			int oh = buffers.OH;
			int acc;
			Point[]? xlims = buffers.xlims;
			Point[]? ylims = buffers.ylims;
			int yFactor = buffers.yFactor;
			int xFactor = buffers.xFactor;
			int fyfactor = (1 << Prec) / yFactor;
			int fxfactor = (1 << Prec) / xFactor;
			// These bufs cannot be shared if multiple threads operate in same zlevel
			byte[,]? hbufArray = buffers.hbufArray;
			byte[,]? outputArray = buffers.outputArray;
			byte[,]? xCoefArray = buffers.xCoefArray;
			byte[,]? yCoefArray = buffers.yCoefArray;
			bool round = buffers.round;

			if ((iw == ow) && (ih == oh)) {
				return inputArray;
			}
			if (xlims == null || ylims == null || hbufArray == null || outputArray == null || xCoefArray == null || yCoefArray == null) return null;
			// scale the image horizontally
			for (j = 0; j < ih; j++) {
				for (k = 0; k < ow; k++) {
					acc = 0;
					for (i = xlims[k].X; i <= xlims[k].Y; i++) {
						acc += inputArray[j, i] * xCoefArray[k, i];
					}
					if (round) {
						hbufArray[j, k] = (byte)Math.Round((((float)acc / (float)xFactor) + 0.125), MidpointRounding.AwayFromZero);
						//hbufArray[j, k] = (byte)Math.Round(((float)acc / (float)xFactor), MidpointRounding.AwayFromZero);
					} else {
						//hbufArray[j, k] = (byte)Math.Round(((float)acc / (float)xFactor), MidpointRounding.AwayFromZero);
						hbufArray[j, k] = (byte)(((acc * fxfactor) + (1 << (Prec - 1))) >> Prec);
					}
				}
			}
			// scale the image vertically
			for (k = 0; k < ow; k++) {
				for (j = 0; j < oh; j++) {
					acc = 0;
					for (i = ylims[j].X; i <= ylims[j].Y; i++) {
						acc += hbufArray[i, k] * yCoefArray[j, i];
					}
					if (round) {
						outputArray[j, k] = (byte)Math.Round((((float)acc / (float)yFactor) + 0.125), MidpointRounding.AwayFromZero);
						//outputArray[j, k] = (byte)Math.Round(((float)acc / (float)yFactor), MidpointRounding.AwayFromZero);
					} else {
						//outputArray[j, k] = (byte)Math.Round(((float)acc / (float)yFactor), MidpointRounding.AwayFromZero);
						outputArray[j, k] = (byte)(((acc * fyfactor) + (1 << (Prec - 1))) >> Prec);
					}
				}
			}
			return outputArray;
		}

		public static Bitmap? ResizeImage(Bitmap image, int width, int height, ScalerType scaler) {
			Size sz = new Size(width, height);

			return ResizeImage(image, sz, scaler);
		}

		// LQ/MQ/HQ will produce differnt results depending on x86 or x64
		public static Bitmap? ResizeImage(Bitmap image, Size size, ScalerType scaler) {
			if ((size.Width == 0) || (size.Height == 0) || (image.Width == 0) || (image.Height == 0)) return null;
			var destImage = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);

			byte[,] inputArray;
			byte[,]? outputArray;
			Rectangle destRect = new Rectangle(0, 0, size.Width, size.Height);
			Rectangle sourceRect = new Rectangle(0, 0, image.Width, image.Height);

			switch (scaler) {
				case ScalerType.EM:
					inputArray = ImgLib.BitmapToArrayMono(image);
					outputArray = EmulatedResizeMono(inputArray, size, true, false);
					destImage = ImgLib.ArrayToBitmap(outputArray);
					break;
				case ScalerType.EM2:
					inputArray = ImgLib.BitmapToArrayMono(image);
					outputArray = EmulatedResizeMono(inputArray, size, false, false);
					destImage = ImgLib.ArrayToBitmap(outputArray);
					break;
				case ScalerType.RTL:
					inputArray = ImgLib.BitmapToArrayMono(image);
					outputArray = EmulatedResizeMonoRTL(inputArray, size, false);
					destImage = ImgLib.ArrayToBitmap(outputArray);
					break;
				case ScalerType.RTLM:   // memory-optimized version of RTL
					inputArray = ImgLib.BitmapToArrayMono(image);
					outputArray = EmulatedResizeMonoRTL(inputArray, size, true);
					destImage = ImgLib.ArrayToBitmap(outputArray);
					break;
				case ScalerType.LQ:
					using (var graphics = Graphics.FromImage(destImage)) {
						graphics.CompositingMode = CompositingMode.SourceCopy;
						graphics.CompositingQuality = CompositingQuality.HighSpeed;
						graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
						graphics.SmoothingMode = SmoothingMode.HighSpeed;
						graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
						//using (var wrapMode = new ImageAttributes()) {
						graphics.DrawImage(image, destRect, sourceRect, GraphicsUnit.Pixel);
						//}
						if (image.GetImageTag() != ImageTag.UNDEF) destImage.SetImageTag(image.GetImageTag());
						else destImage.SetImageTag(ImageTag.RGB);
					}
					break;
				case ScalerType.MQ:
					using (var graphics = Graphics.FromImage(destImage)) {
						graphics.CompositingMode = CompositingMode.SourceCopy;
						graphics.CompositingQuality = CompositingQuality.HighQuality;
						graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
						graphics.SmoothingMode = SmoothingMode.HighQuality;
						graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
						//using (var wrapMode = new ImageAttributes()) {
						graphics.DrawImage(image, destRect, sourceRect, GraphicsUnit.Pixel);
						//}
						if (image.GetImageTag() != ImageTag.UNDEF) destImage.SetImageTag(image.GetImageTag());
						else destImage.SetImageTag(ImageTag.RGB);
					}
					break;
				case ScalerType.HQ:
					using (var graphics = Graphics.FromImage(destImage)) {
						graphics.CompositingMode = CompositingMode.SourceCopy;
						graphics.CompositingQuality = CompositingQuality.HighQuality;
						graphics.InterpolationMode = InterpolationMode.Bicubic;
						graphics.SmoothingMode = SmoothingMode.HighQuality;
						graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
						//using (var wrapMode = new ImageAttributes()) {
						graphics.DrawImage(image, destRect, sourceRect, GraphicsUnit.Pixel);
						//}
						if (image.GetImageTag() != ImageTag.UNDEF) destImage.SetImageTag(image.GetImageTag());
						else destImage.SetImageTag(ImageTag.RGB);
					}
					break;
				default:
					destImage = new Bitmap(image);
					if (image.GetImageTag() != ImageTag.UNDEF) destImage.SetImageTag(image.GetImageTag());
					else destImage.SetImageTag(ImageTag.RGB);
					break;
			}

			return destImage;
		}

		/*	public static OpenCvSharp.Mat? ResizeImageCv(Bitmap image, Size size, ScalerType scaler) {
				if ((size.Width == 0) || (size.Height == 0) || (image.Width == 0) || (image.Height == 0)) return null;
				var destImage = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);

				// OpenCV
				OpenCvSharp.InterpolationFlags flag = OpenCvSharp.InterpolationFlags.Linear; ;
				if (scaler == ScalerType.LQ) {
					flag = OpenCvSharp.InterpolationFlags.Nearest;
				} else if (scaler == ScalerType.MQ) {
					flag = OpenCvSharp.InterpolationFlags.Linear;
				} else if (scaler == ScalerType.HQ) {
					flag = OpenCvSharp.InterpolationFlags.Cubic;
				}
				OpenCvSharp.Mat srcMat = OpenCvSharp.Extensions.BitmapConverter.ToMat(image);
				OpenCvSharp.Size dstSize = new OpenCvSharp.Size(size.Width, size.Height);
				OpenCvSharp.Mat dstMat = srcMat.Resize(dstSize, 0, 0, flag);
				return dstMat;
			}*/

		public static Bitmap CropImage(Bitmap image, Rectangle srcRect) {
			Bitmap nbmp = new Bitmap(srcRect.Width, srcRect.Height);
			Rectangle destRect = new Rectangle(0, 0, srcRect.Width, srcRect.Height);

			using (Graphics g = Graphics.FromImage(nbmp)) {
				g.DrawImage(image, destRect, srcRect, GraphicsUnit.Pixel);
			}
			if (image.GetImageTag() != ImageTag.UNDEF) nbmp.SetImageTag(image.GetImageTag());
			else nbmp.SetImageTag(ImageTag.RGB);
			return nbmp;
		}

		public static Bitmap CropImage(Bitmap image, float AR) {
			Bitmap nbmp;
			float bar;
			Rectangle destRect, srcRect;
			int dx, dy;

			// AR = width/height
			bar = (float)image.Width / (float)image.Height;
			if (bar > AR) { // image too wide
				destRect = new Rectangle(0, 0, (int)(image.Height * AR), image.Height);
			} else if (bar < AR) {  // image too narrow
				destRect = new Rectangle(0, 0, image.Width, (int)(image.Width / AR));
			} else {    // just right
				destRect = new Rectangle(0, 0, image.Width, image.Height);
			}
			nbmp = new Bitmap(destRect.Width, destRect.Height);
			dx = image.Width - destRect.Width;
			dy = image.Height - destRect.Height;
			srcRect = new Rectangle(dx / 2, dy / 2, destRect.Width, destRect.Height);
			using (Graphics g = Graphics.FromImage(nbmp)) {
				g.DrawImage(image, destRect, srcRect, GraphicsUnit.Pixel);
			}
			if (image.GetImageTag() != ImageTag.UNDEF) nbmp.SetImageTag(image.GetImageTag());
			else nbmp.SetImageTag(ImageTag.RGB);
			return nbmp;
		}

		public static Rectangle GetARCropRect(Size imageSize, Size scaleSize, float AR) {
			float bar;
			Rectangle cropRect;
			int dx, dy;

			// AR = width/height
			bar = (float)imageSize.Width / (float)imageSize.Height;
			if (bar > AR) { // image too wide
				scaleSize = new Size((int)(imageSize.Height * AR), imageSize.Height);
			} else if (bar < AR) {  // image too narrow
				scaleSize = new Size(imageSize.Width, (int)(imageSize.Width / AR));
			} else {    // just right
				scaleSize = new Size(imageSize.Width, imageSize.Height);
			}
			dx = imageSize.Width - scaleSize.Width;
			dy = imageSize.Height - scaleSize.Height;
			cropRect = new Rectangle(dx / 2, dy / 2, scaleSize.Width, scaleSize.Height);
			return cropRect;
		}

		public static byte[,] CropImage(byte[,] map, Rectangle srcRect) {
			byte[,] newMap;
			int y;
			int dstW, dstH;
			int srcW, srcL, srcT;
			int srcO, dstO;

			dstW = srcRect.Width;
			dstH = srcRect.Height;
			srcW = map.GetLength(1);
			srcL = srcRect.Left;
			srcT = srcRect.Top;
			newMap = new byte[dstH, dstW];

			for (y = 0; y < dstH; y++) {
				dstO = dstW * y;
				srcO = (y + srcT) * srcW + srcL;
				Buffer.BlockCopy(map, srcO, newMap, dstO, dstW);
			}
			return newMap;
		}

		public static byte[,] RotateFlip(byte[,] map, RotateFlipType rotateFlip) {
			int iw = map.GetLength(1);
			int ih = map.GetLength(0);
			int ow, oh;
			byte[,] newMap;
			int x, y;
			switch (rotateFlip) {
				case RotateFlipType.RotateNoneFlipNone:
					newMap = map;
					break;
				case RotateFlipType.Rotate90FlipNone:
					ow = ih; oh = iw;
					newMap = new byte[oh, ow];
					for (y = 0; y < oh; y++) {
						for (x = 0; x < ow; x++) {
							newMap[y, x] = map[ih - x - 1, y];
						}
					}
					break;
				case RotateFlipType.Rotate270FlipNone:
					ow = ih; oh = iw;
					newMap = new byte[oh, ow];
					for (y = 0; y < oh; y++) {
						for (x = 0; x < ow; x++) {
							newMap[y, x] = map[x, iw - y - 1];
						}
					}
					break;
				case RotateFlipType.Rotate180FlipNone:
					ow = iw; oh = ih;
					newMap = new byte[oh, ow];
					for (y = 0; y < oh; y++) {
						for (x = 0; x < ow; x++) {
							newMap[y, x] = map[ih - y - 1, iw - x - 1];
						}
					}
					break;
				case RotateFlipType.RotateNoneFlipX:
					ow = iw; oh = ih;
					newMap = new byte[oh, ow];
					for (y = 0; y < oh; y++) {
						for (x = 0; x < ow; x++) {
							newMap[y, x] = map[y, iw - x - 1];
						}
					}
					break;
				case RotateFlipType.RotateNoneFlipY:
					ow = iw; oh = ih;
					newMap = new byte[oh, ow];
					for (y = 0; y < oh; y++) {
						for (x = 0; x < ow; x++) {
							newMap[y, x] = map[ih - y - 1, x];
						}
					}
					break;
				default:
					newMap = map;
					break;
			}
			return newMap;
		}

		public static Bitmap Invert(Bitmap srcBitmap) {
			int i, j;
			int width, height;
			width = srcBitmap.Width;
			height = srcBitmap.Height;
			Bitmap dstBitmap = new Bitmap(srcBitmap);

			Debug.Assert((Image.GetPixelFormatSize(srcBitmap.PixelFormat) == 32));
			BitmapData srcData = srcBitmap.LockBits(new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			BitmapData dstData = dstBitmap.LockBits(new Rectangle(0, 0, dstBitmap.Width, dstBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			unsafe {
				byte* srcPtr = (byte*)srcData.Scan0;
				byte* dstPtr = (byte*)dstData.Scan0;
				byte* srcOffset;
				byte* dstOffset;

				srcOffset = srcPtr;
				dstOffset = dstPtr;
				for (j = 0; j < height; j++) {
					for (i = 0; i < width; i++) {
						dstOffset[0] = (byte)(255 - srcOffset[0]);
						dstOffset[1] = (byte)(255 - srcOffset[1]);
						dstOffset[2] = (byte)(255 - srcOffset[2]);
						dstOffset[3] = 255;
						srcOffset += 4;
						dstOffset += 4;
					}
				}
			}
			srcBitmap.UnlockBits(srcData);
			dstBitmap.UnlockBits(dstData);
			if (srcBitmap.GetImageTag() != ImageTag.UNDEF) dstBitmap.SetImageTag(srcBitmap.GetImageTag());
			else dstBitmap.SetImageTag(ImageTag.RGB);
			return dstBitmap;
		}

		public static byte[,] Bin2x2(byte[,] map) {
			int k, j;
			int iw, ih, ow, oh;
			iw = map.GetLength(1);
			ih = map.GetLength(0);
			ow = iw / 2;
			oh = ih / 2;
			byte[,] binned = new byte[oh, ow];
			byte avg;

			for (j = 0; j < oh; j++) {
				for (k = 0; k < ow; k++) {
					avg = (byte)((map[j * 2, k * 2] + map[j * 2 + 1, k * 2] + map[j * 2, k * 2 + 1] + map[j * 2 + 1, k * 2 + 1]) / 4);
					binned[j, k] = avg;
				}
			}

			return binned;
		}

		// ratio is how much of primary bitmap to keep from 0.0 to 1.0
		public static Bitmap Mix(Bitmap srcBitmapPrimary, Bitmap srcBitmapSecondary, float ratio) {
			int i, j;
			int width, height;
			width = srcBitmapPrimary.Width;
			height = srcBitmapPrimary.Height;
			Bitmap dstBitmap = new Bitmap(srcBitmapPrimary);

			Debug.Assert((Image.GetPixelFormatSize(srcBitmapPrimary.PixelFormat) == 32));
			Debug.Assert((Image.GetPixelFormatSize(srcBitmapSecondary.PixelFormat) == 32));
			BitmapData srcDataPri = srcBitmapPrimary.LockBits(new Rectangle(0, 0, srcBitmapPrimary.Width, srcBitmapPrimary.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			BitmapData srcDataSec = srcBitmapSecondary.LockBits(new Rectangle(0, 0, srcBitmapSecondary.Width, srcBitmapSecondary.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			BitmapData dstData = dstBitmap.LockBits(new Rectangle(0, 0, dstBitmap.Width, dstBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			unsafe {
				byte* srcPtrPri = (byte*)srcDataPri.Scan0;
				byte* srcPtrSec = (byte*)srcDataSec.Scan0;
				byte* dstPtr = (byte*)dstData.Scan0;
				byte* srcOffsetPri, srcOffsetSec;
				byte* dstOffset;
				//UInt32* srcPriPxl, srcSecPxl;

				srcOffsetPri = srcPtrPri;
				srcOffsetSec = srcPtrSec;
				dstOffset = dstPtr;
				//srcPriPxl = (UInt32*)srcOffsetPri;
				//srcSecPxl = (UInt32*)srcOffsetSec;
				for (j = 0; j < height; j++) {
					for (i = 0; i < width; i++) {
						//if (srcPriPxl[0] == srcSecPxl[0]) {
						//	dstOffset[0] = srcOffsetPri[0];
						//	dstOffset[1] = srcOffsetPri[1];
						//	dstOffset[2] = srcOffsetPri[2];
						//} else {
							dstOffset[0] = (byte)((srcOffsetPri[0] * ratio) + srcOffsetSec[0] * (1.0 - ratio));
							dstOffset[1] = (byte)((srcOffsetPri[1] * ratio) + srcOffsetSec[1] * (1.0 - ratio));
							dstOffset[2] = (byte)((srcOffsetPri[2] * ratio) + srcOffsetSec[2] * (1.0 - ratio));
						//}
						dstOffset[3] = 255;
						srcOffsetPri += 4;
						srcOffsetSec += 4;
						dstOffset += 4;
					//	srcPriPxl++;
					//	srcSecPxl++;
					}
				}
			}
			srcBitmapPrimary.UnlockBits(srcDataPri);
			srcBitmapSecondary.UnlockBits(srcDataSec);
			dstBitmap.UnlockBits(dstData);
			if (srcBitmapPrimary.GetImageTag() != ImageTag.UNDEF) dstBitmap.SetImageTag(srcBitmapPrimary.GetImageTag());
			else dstBitmap.SetImageTag(ImageTag.RGB);
			return dstBitmap;
		}

		public static Bitmap MixMT(Bitmap srcBitmapPrimary, Bitmap srcBitmapSecondary, float ratio) {
			int width, height;
			if ((srcBitmapPrimary is null) || (srcBitmapSecondary is null)) return null;
			width = srcBitmapPrimary.Width;
			height = srcBitmapPrimary.Height;
			Bitmap dstBitmap = new Bitmap(srcBitmapPrimary);

			Debug.Assert((Image.GetPixelFormatSize(srcBitmapPrimary.PixelFormat) == 32));
			Debug.Assert((Image.GetPixelFormatSize(srcBitmapSecondary.PixelFormat) == 32));
			BitmapData srcDataPri = srcBitmapPrimary.LockBits(new Rectangle(0, 0, srcBitmapPrimary.Width, srcBitmapPrimary.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			BitmapData srcDataSec = srcBitmapSecondary.LockBits(new Rectangle(0, 0, srcBitmapSecondary.Width, srcBitmapSecondary.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			BitmapData dstData = dstBitmap.LockBits(new Rectangle(0, 0, dstBitmap.Width, dstBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			ParallelOptions options = new ParallelOptions();
			options.MaxDegreeOfParallelism = Threads;
			unsafe {
				byte* srcPtrPri = (byte*)srcDataPri.Scan0;
				byte* srcPtrSec = (byte*)srcDataSec.Scan0;
				byte* dstPtr = (byte*)dstData.Scan0;
				Parallel.For(0, height - 1, j => {
					//for (j = 0; j < height; j++) {
					byte* srcOffsetPri, srcOffsetSec;
					byte* dstOffset;
					srcOffsetPri = srcPtrPri + j * width * 4;
					srcOffsetSec = srcPtrSec + j * width * 4; ;
					dstOffset = dstPtr + j * width * 4;
					for (int i = 0; i < width; i++) {
						dstOffset[0] = (byte)((srcOffsetPri[0] * ratio) + srcOffsetSec[0] * (1.0 - ratio));
						dstOffset[1] = (byte)((srcOffsetPri[1] * ratio) + srcOffsetSec[1] * (1.0 - ratio));
						dstOffset[2] = (byte)((srcOffsetPri[2] * ratio) + srcOffsetSec[2] * (1.0 - ratio));
						dstOffset[3] = 255;
						srcOffsetPri += 4;
						srcOffsetSec += 4;
						dstOffset += 4;
					}
				});
			}
			srcBitmapPrimary.UnlockBits(srcDataPri);
			srcBitmapSecondary.UnlockBits(srcDataSec);
			dstBitmap.UnlockBits(dstData);
			if (srcBitmapPrimary.GetImageTag() != ImageTag.UNDEF) dstBitmap.SetImageTag(srcBitmapPrimary.GetImageTag());
			else dstBitmap.SetImageTag(ImageTag.RGB);
			return dstBitmap;
		}


		public static Bitmap Mix(Bitmap srcBitmapPrimary, Bitmap srcBitmapSecondary, float ratio, Rectangle clip) {
			int i, j;
			int width, height;
			width = srcBitmapPrimary.Width;
			height = srcBitmapPrimary.Height;
			Bitmap dstBitmap = new Bitmap(srcBitmapPrimary);

			Debug.Assert((Image.GetPixelFormatSize(srcBitmapPrimary.PixelFormat) == 32));
			Debug.Assert((Image.GetPixelFormatSize(srcBitmapSecondary.PixelFormat) == 32));
			BitmapData srcDataPri = srcBitmapPrimary.LockBits(new Rectangle(0, 0, srcBitmapPrimary.Width, srcBitmapPrimary.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			BitmapData srcDataSec = srcBitmapSecondary.LockBits(new Rectangle(0, 0, srcBitmapSecondary.Width, srcBitmapSecondary.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			BitmapData dstData = dstBitmap.LockBits(new Rectangle(0, 0, dstBitmap.Width, dstBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			unsafe {
				byte* srcPtrPri = (byte*)srcDataPri.Scan0;
				byte* srcPtrSec = (byte*)srcDataSec.Scan0;
				byte* dstPtr = (byte*)dstData.Scan0;
				byte* srcOffsetPri, srcOffsetSec;
				byte* dstOffset;
				int clipOffset;
				//UInt32* srcPriPxl, srcSecPxl;

				srcOffsetPri = srcPtrPri;
				srcOffsetSec = srcPtrSec;
				dstOffset = dstPtr;
				//srcPriPxl = (UInt32*)srcOffsetPri;
				//srcSecPxl = (UInt32*)srcOffsetSec;
				for (j = clip.Top; j < clip.Bottom; j++) {
					clipOffset = ((j * width) + clip.Left) * 4;
					srcOffsetPri = srcPtrPri + clipOffset;
					srcOffsetSec = srcPtrSec + clipOffset;
					dstOffset = dstPtr + clipOffset;
					for (i = clip.Left; i < clip.Right; i++) {
						//if (srcPriPxl[0] == srcSecPxl[0]) {
						//	dstOffset[0] = srcOffsetPri[0];
						//	dstOffset[1] = srcOffsetPri[1];
						//	dstOffset[2] = srcOffsetPri[2];
						//} else {
						dstOffset[0] = (byte)((srcOffsetPri[0] * ratio) + srcOffsetSec[0] * (1.0 - ratio));
						dstOffset[1] = (byte)((srcOffsetPri[1] * ratio) + srcOffsetSec[1] * (1.0 - ratio));
						dstOffset[2] = (byte)((srcOffsetPri[2] * ratio) + srcOffsetSec[2] * (1.0 - ratio));
						//}
						dstOffset[3] = 255;
						srcOffsetPri += 4;
						srcOffsetSec += 4;
						dstOffset += 4;
						//	srcPriPxl++;
						//	srcSecPxl++;
					}
				}
			}
			srcBitmapPrimary.UnlockBits(srcDataPri);
			srcBitmapSecondary.UnlockBits(srcDataSec);
			dstBitmap.UnlockBits(dstData);
			if (srcBitmapPrimary.GetImageTag() != ImageTag.UNDEF) dstBitmap.SetImageTag(srcBitmapPrimary.GetImageTag());
			else dstBitmap.SetImageTag(ImageTag.RGB);
			return dstBitmap;
		}

		public static byte[][,] Mix(byte[][,] srcMapPrimary, byte[][,] srcMapSecondary, float ratio) {
			int i, j;
			int width, height, bpp;
			bpp = srcMapPrimary.Length;
			width = srcMapPrimary[0].GetLength(1);
			height = srcMapPrimary[0].GetLength(0);
			byte[][,] dstMap = new byte[bpp][,];

			Debug.Assert(bpp == srcMapSecondary.Length);
			Debug.Assert(width == srcMapSecondary[0].GetLength(1));
			Debug.Assert(height == srcMapSecondary[0].GetLength(0));
			for (int c = 0; c < bpp; c++) {
				dstMap[c] = new byte[height, width];
				for (j = 0; j < height; j++) {
					for (i = 0; i < width; i++) {
						dstMap[c][j, i] = (byte)((srcMapPrimary[c][j, i] * ratio) + (srcMapSecondary[c][j, i] * (1.0 - ratio)));
					}
				}
			}
			return dstMap;
		}

		public static T[,] PadEdge<T>(T[,] map) {
			int i, j;
			int width, height;

			height = map.GetLength(0);
			width = map.GetLength(1);

			T[,] pad = new T[height + 2, width + 2];

			for (j = 0; j < height; j++) {
				for (i = 0; i < width; i++) {
					pad[j + 1, i + 1] = map[j, i];
				}
			}

			return pad;
		}

		public static T[,] UnPadEdge<T>(T[,] map) {
			int i, j;
			int width, height;

			height = map.GetLength(0);
			width = map.GetLength(1);

			T[,] unpad = new T[height - 2, width - 2];

			for (j = 0; j < height - 2; j++) {
				for (i = 0; i < width - 2; i++) {
					unpad[j, i] = map[j + 1, i + 1];
				}
			}

			return unpad;
		}

		public static byte[,] CopyMap(byte[,] map) {
			int i, j;
			int width, height;

			height = map.GetLength(0);
			width = map.GetLength(1);

			byte[,] copy = new byte[height, width];

			for (j = 0; j < height; j++) {
				for (i = 0; i < width; i++) {
					copy[j, i] = map[j, i];
				}
			}

			return copy;
		}

		public static void ReflectEdge(byte[,] map) {
			int i, j;
			int width, height;

			height = map.GetLength(0);
			width = map.GetLength(1);
			// use reflection padding
			for (i = 1; i < width - 1; i++) {
				map[0, i] = map[2, i];
				map[height - 1, i] = map[height - 3, i];
			}
			for (j = 1; j < height - 1; j++) {
				map[j, 0] = map[j, 2];
				map[j, width - 1] = map[j, width - 3];
			}
			map[0, 0] = map[2, 2];
			map[0, width - 1] = map[2, width - 3];
			map[height - 1, 0] = map[height - 3, 2];
			map[height - 1, width - 1] = map[height - 3, width - 3];
		}

		public static byte convolve3x3(byte[,] map, int[,] kernel, int x, int y) {
			int sum = 0;
			int i, j;
			for (j = 0; j < 3; j++) {
				for (i = 0; i < 3; i++) {
					sum = sum + map[j + y - 1, i + x - 1] * kernel[j, i];
				}
			}
			if (sum < 0) sum = 0;
			if (sum > 255) sum = 255;
			return (byte)sum;
		}

		public static byte[,] Filter(byte[,] map, int[,] kernel) {
			int i, j;
			int width, height;

			height = map.GetLength(0);
			width = map.GetLength(1);

			byte[,] filtered = new byte[height, width];

			for (j = 1; j < height - 1; j++) {
				for (i = 1; i < width - 1; i++) {
					filtered[j, i] = convolve3x3(map, kernel, i, j);
				}
			}

			return filtered;
		}

		public static byte[] GetGammaTable(float gamma) {
			int i;
			float g, p;
			byte[] table = new byte[256];

			for (i = 0; i < 256; i++) {
				g = (float)i / 255.0f;
				p = 1.0f / (float)gamma;
				g = (float)Math.Pow(g, p) * 255;
				g = (float)Math.Round(g);
				table[i] = (byte)g;
			}
			return table;
		}

		public static byte[] GetPieceWise32(byte[] points) {
			int pixel, addr, offset;
			short lo, hi;
			byte[] lut = new byte[256];

			for (pixel = 0; pixel < 255; pixel++) {
				addr = pixel >> 3;
				offset = pixel & 0x07;
				lo = points[addr];
				hi = points[addr + 1];
				switch (offset) {
					case 0: lut[pixel] = (byte)lo; break;
					case 1: lut[pixel] = (byte)(((lo * 7) + hi) / 8); break;
					case 2: lut[pixel] = (byte)(((lo * 3) + hi) / 4); break;
					case 3: lut[pixel] = (byte)(((lo * 5) + (hi * 3)) / 8); break;
					case 4: lut[pixel] = (byte)((lo + hi) / 2); break;
					case 5: lut[pixel] = (byte)(((hi * 5) + (lo * 3)) / 8); break;
					case 6: lut[pixel] = (byte)(((hi * 3) + lo) / 4); break;
					case 7: lut[pixel] = (byte)(((hi * 7) + lo) / 8); break;
				}
			}
			return lut;
		}

		public static byte[] GetPieceWise64(byte[] points) {
			int pixel, addr, offset;
			short lo, hi;
			byte[] lut = new byte[256];

			for (pixel = 0; pixel < 255; pixel++) {
				addr = pixel >> 2;
				offset = pixel & 0x03;
				lo = points[addr];
				hi = points[addr + 1];
				switch (offset) {
					case 0: lut[pixel] = (byte)lo; break;
					case 1: lut[pixel] = (byte)(((lo * 3) + hi) / 4); break;
					case 2: lut[pixel] = (byte)((lo + hi) / 2); break;
					case 3: lut[pixel] = (byte)(((hi * 3) + lo) / 4); break;
				}
			}
			return lut;
		}

		public static void ApplyLUT(byte[,] map, byte[] lut) {
			int j, k;
			int h = map.GetLength(0);
			int w = map.GetLength(1);

			for (j = 0; j < h; j++) {
				for (k = 0; k < w; k++) {
					map[j, k] = lut[map[j, k]];
				}
			}
		}

		public static byte[,] Transpose(byte[,] map) {
			int j, k;
			int h = map.GetLength(0);
			int w = map.GetLength(1);
			byte[,] trans = new byte[w, h];

			for (j = 0; j < h; j++) {
				for (k = 0; k < w; k++) {
					trans[k, j] = map[j, k];
				}
			}
			return trans;
		}

		public static float CosineSimilarity(byte[,] map1, byte[,] map2) {
			int dot, magA, magB;
			int w1, h1, w2, h2;
			int x, y;
			w1 = map1.GetLength(1);
			h1 = map1.GetLength(0);
			w2 = map2.GetLength(1);
			h2 = map2.GetLength(0);
			Debug.Assert((w1 == w2) && (h1 != h2));

			dot = 0; magA = 0; magB = 0;
			for (y = 0; y < h1; y++) {
				for (x = 0; x < w1; x++) {
					dot += map1[y, x] * map2[y, x];
					magA += map1[y, x] * map1[y, x];
					magB += map2[y, x] * map2[y, x];
				}
			}

			return (float)dot / (float)Math.Sqrt(magA * magB);
		}

		public static float StructuralSimilarity(byte[,] map1, byte[,] map2) {
			float K1 = 0.01f;
			float K2 = 0.03f;
			float L = 255;
			float alpha = 1;
			float beta = 1;
			float gamma = 1;

			float C1 = (K1 * L) * (K1 * L);
			float C2 = (K2 * L) * (K2 * L);
			float C3 = C2 / 2;

			float covariance(byte[,] map1, byte[,] map2) {
				int w1, h1, w2, h2;
				int x, y;
				w1 = map1.GetLength(1);
				h1 = map1.GetLength(0);
				w2 = map2.GetLength(1);
				h2 = map2.GetLength(0);
				Debug.Assert((w1 == w2) && (h1 != h2));

				float sum = 0;
				float meanA = AverageMono(map1);
				float meanB = AverageMono(map2);

				for (y = 0; y < h1; y++) {
					for (x = 0; x < w1; x++) {
						sum += ((float)map1[y, x] - meanA) * ((float)map2[y, x] - meanB);
					}
				}
				return sum;
			}

			float variance(byte[,] map) {
				return covariance(map, map);
			}


			float luminance(byte[,] map1, byte[,] map2) {
				float ux = AverageMono(map1);
				float uy = AverageMono(map2);

				return (2 * ux * uy + C1) / ((ux * ux) + (uy + uy) + C1);
			}

			float contrast(byte[,] map1, byte[,] map2) {
				float sx2 = variance(map1);
				float sy2 = variance(map2);
				float sx = (float)Math.Sqrt(sx2);
				float sy = (float)Math.Sqrt(sy2);
				return (2 * sx * sy + C2) / (sx2 + sy2 + C2);
			}

			float structure(byte[,] map1, byte[,] map2) {
				float sx = (float)Math.Sqrt(variance(map1));
				float sy = (float)Math.Sqrt(variance(map2));
				float sxy = covariance(map1, map2);

				return (sxy + C3) / (sx * sy + C3);

			}

			float l = (float)Math.Pow(luminance(map1, map2), alpha);
			float c = (float)Math.Pow(contrast(map1, map2), beta);
			float s = (float)Math.Pow(structure(map1, map2), gamma);

			return l * c * s;
		}

		public static float[] DCT1D(byte[] vector) {
			int bin, k;
			float arg;
			int len = vector.Length;
			float[] transform = new float[len];
			float PI = (float)Math.PI;

			for (bin = 0; bin < len; bin++) {
				transform[bin] = 0.0F;
				for (k = 0; k < len; k++) {
					arg = (float)bin * PI * (float)k / (float)len;
					transform[bin] += (float)(vector[k] * (float)Math.Cos(arg));
				}
			}

			return transform;
		}

		public static float[,] DCT(byte[,] map) {
			int iw = map.GetLength(1);
			int ih = map.GetLength(0);
			int i, j, u, v;
			float[,] matrix = new float[ih, iw];

			for (v = 0; v < ih; ++v) {
				for (u = 0; u < iw; ++u) {
					matrix[v, u] = 0;
					for (j = 0; j < ih; j++) {
						for (i = 0; i < iw; i++) {
							matrix[v, u] += (float)(map[j, i] * Math.Cos(Math.PI / ((float)iw) * (i + 1.0 / 2.0) * u) * Math.Cos(Math.PI / ((float)ih) * (j + 1.0 / 2.0) * v));
						}
					}
				}
			}

			return matrix;
		}

		public static float[,] IDCT(float[,] matrix) {
			int iw = matrix.GetLength(1);
			int ih = matrix.GetLength(0);
			int i, j, u, v;
			float[,] map = new float[ih, iw];
			float PI = (float)Math.PI;

			for (v = 0; v < ih; ++v) {
				for (u = 0; u < iw; ++u) {
					map[v, u] = (float)(1.0 / 4.0 * matrix[0, 0]);
					for (i = 1; i < iw; i++) {
						map[v, u] += (float)(1.0 / 2.0 * matrix[0, i]);
					}
					for (j = 1; j < ih; j++) {
						map[v, u] += (float)(1.0 / 2.0 * matrix[j, 0]);
					}
					for (j = 1; j < ih; j++) {
						for (i = 1; i < iw; i++) {
							map[v, u] += (float)(matrix[j, i] * Math.Cos(PI / ((float)iw) * (u + 1.0 / 2.0) * i) * Math.Cos(PI / ((float)ih) * (v + 1.0 / 2.0) * j));
						}
					}
					map[v, u] *= (float)(2.0 / ((float)iw) * 2.0 / ((float)ih));
				}
			}
			return map;
		}

		public static byte[] PHash(byte[,] map) {
			int iw = map.GetLength(1);
			int ih = map.GetLength(0);

			byte[,] matrix = new byte[ih, iw];

			/*void dct(float** DCTMatrix, float** Matrix, int N, int M) {

				int i, j, u, v;
				for (u = 0; u < N; ++u) {
					for (v = 0; v < M; ++v) {
						DCTMatrix[u][v] = 0;
						for (i = 0; i < N; i++) {
							for (j = 0; j < M; j++) {
								DCTMatrix[u][v] += Matrix[i][j] * cos(M_PI / ((float)N) * (i + 1./ 2.) * u) * cos(M_PI / ((float)M) * (j + 1./ 2.) * v);
							}
						}
					}
				}
			}

			void idct(float** Matrix, float** DCTMatrix, int N, int M) {
				int i, j, u, v;

				for (u = 0; u < N; ++u) {
					for (v = 0; v < M; ++v) {
						Matrix[u][v] = 1 / 4.* DCTMatrix[0][0];
						for (i = 1; i < N; i++) {
							Matrix[u][v] += 1 / 2.* DCTMatrix[i][0];
						}
						for (j = 1; j < M; j++) {
							Matrix[u][v] += 1 / 2.* DCTMatrix[0][j];
						}

						for (i = 1; i < N; i++) {
							for (j = 1; j < M; j++) {
								Matrix[u][v] += DCTMatrix[i][j] * cos(M_PI / ((float)N) * (u + 1./ 2.) * i) * cos(M_PI / ((float)M) * (v + 1./ 2.) * j);
							}
						}
						Matrix[u][v] *= 2./ ((float)N) * 2./ ((float)M);
					}
				}
			}*/

			return null;
		}
	}

	/*public class GpuLib {
		public Context MyContext;
		public Accelerator MyAccelerator;
		public KernelConfig MyKernelConfig;

		public Accelerator AcquireGPU() {
			MyContext = Context.CreateDefault();
			MyAccelerator = MyContext.CreateCPUAccelerator(0);
			return MyAccelerator;
		}

		public void ReleaseGPU() {
			MyAccelerator.Dispose();
			MyContext.Dispose();
		}

		public MemoryBuffer2D<byte, Stride2D.DenseX> BitmapToGPU(byte[,] map) {
			int mapW, mapH;
			mapW = map.GetLength(0);
			mapH = map.GetLength(1);

			MemoryBuffer2D<byte, Stride2D.DenseX> mapOnDevice = MyAccelerator.Allocate2DDenseX<byte>(new Index2D(mapW, mapH));
			mapOnDevice.CopyFromCPU(map);
			return mapOnDevice;
		}

		public MemoryBuffer2D<short, Stride2D.DenseX> BitmapToGPU(short[,] map) {
			int mapW, mapH;
			mapW = map.GetLength(0);
			mapH = map.GetLength(1);

			MemoryBuffer2D<short, Stride2D.DenseX> mapOnDevice = MyAccelerator.Allocate2DDenseX<short>(new Index2D(mapW, mapH));
			mapOnDevice.CopyFromCPU(map);
			return mapOnDevice;
		}

		public byte[,] BitmapFromGPU(MemoryBuffer2D<byte, Stride2D.DenseX> mapOnDevice) {
			return mapOnDevice.GetAsArray2D();
		}

		public short[,] BitmapFromGPU(MemoryBuffer2D<short, Stride2D.DenseX> mapOnDevice) {
			return mapOnDevice.GetAsArray2D();
		}

		public static void EmulatedResizeMonoKernel(Point offset, ArrayView2D<byte, Stride2D.DenseX> mapView, ArrayView2D<byte, Stride2D.DenseX> outView, Size inputSize, Size outputSize, bool round) {
			int i, j, k;
			float scaleX = (float)outputSize.Width / (float)inputSize.Width;
			float scaleY = (float)outputSize.Height / (float)inputSize.Height;
			// calculate sizes and setup arrays
			int[] xExtend, yExtend;
			int xFactor, yFactor;
			xFactor = (int)Math.Truncate(16.0F / scaleX);
			yFactor = (int)Math.Truncate(16.0F / scaleY);
			if ((inputSize.Width == 0) || (inputSize.Height == 0) || (outputSize.Width == 0) || (outputSize.Height == 0)) return;
			int ptr;
			int iw, ih, ow, oh;
			iw = inputSize.Width;
			ih = inputSize.Height;
			ow = outputSize.Width;
			oh = outputSize.Height;
			xExtend = new int[iw * 16];
			yExtend = new int[ih * 16];
			int acc;
			int lim;

			for (i = 0; i < iw; i++) {
				for (j = 0; j < 16; j++) {
					xExtend[i * 16 + j] = i;
				}
			}
			for (i = 0; i < ih; i++) {
				for (j = 0; j < 16; j++) {
					yExtend[i * 16 + j] = i;
				}
			}
			byte[,] hbufArray = new byte[ih, ow];
			//byte[,] outputArray = new byte[oh, ow];
			// pre-calculate coefficient weights using histogram method
			byte[][] xCoefArray = new byte[ow][];
			byte[][] yCoefArray = new byte[oh][];
			for (i = 0; i < oh; i++) {
				yCoefArray[i] = new byte[ih];
			}
			ptr = 0;
			for (i = 0; i < ow; i++) {
				xCoefArray[i] = new byte[iw];
				for (j = 0; j < xFactor; j++) {
					if (ptr >= xExtend.Length) continue;
					xCoefArray[i][xExtend[ptr]]++;
					ptr++;
				}
			}
			ptr = 0;
			for (i = 0; i < oh; i++) {
				yCoefArray[i] = new byte[ih];
				for (j = 0; j < yFactor; j++) {
					if (ptr >= yExtend.Length) continue;
					yCoefArray[i][yExtend[ptr]]++;
					ptr++;
				}
			}
			// scale the image horizontally
			for (j = 0; j < ih; j++) {
				for (k = 0; k < ow; k++) {
					acc = 0;
					lim = xCoefArray[k].Length;
					for (i = 0; i < lim; i++) {
						if (xCoefArray[k][i] == 0) continue;
						acc += mapView[j + offset.Y, i + offset.X] * xCoefArray[k][i];
					}
					if (round) {
						hbufArray[j, k] = (byte)Math.Round((((double)acc / (double)xFactor) + 0.125), MidpointRounding.AwayFromZero);
					} else {
						hbufArray[j, k] = (byte)(acc / xFactor);
					}
				}
			}
			// scale the image vertically
			for (k = 0; k < ow; k++) {
				for (j = 0; j < oh; j++) {
					acc = 0;
					lim = yCoefArray[j].Length;
					for (i = 0; i < lim; i++) {
						if (yCoefArray[j][i] == 0) continue;
						acc += hbufArray[i, k] * yCoefArray[j][i];
					}
					if (round) {
						outView[j, k] = (byte)Math.Round((((double)acc / (double)yFactor) + 0.125), MidpointRounding.AwayFromZero);
					} else {
						outView[j, k] = (byte)(acc / yFactor);
					}
				}
			}
			//return outputArray;
		}

		public byte[,] EmulatedResizeMono(byte[,] inputArray, Rectangle sourceRect, float scaleX, float scaleY, bool round) {
			Size inputSize = new Size(sourceRect.Size.Height, sourceRect.Size.Width);
			Size outputSize = new Size((int)Math.Round(inputSize.Width * scaleX), (int)Math.Round(inputSize.Height * scaleY));
			byte[,] outputArray = new byte[outputSize.Height, outputSize.Width];
			var iA = BitmapToGPU(inputArray);
			var iB = BitmapToGPU(outputArray);
			Action<Point, ArrayView2D<byte, Stride2D.DenseX>, ArrayView2D<byte, Stride2D.DenseX>, Size, Size, bool> loadedKernel =
			MyAccelerator.LoadStreamKernel<Point, ArrayView2D<byte, Stride2D.DenseX>, ArrayView2D<byte, Stride2D.DenseX>, Size, Size, bool>(EmulatedResizeMonoKernel);
			MyAccelerator.Synchronize();
			loadedKernel(sourceRect.Location, iA.View, iB.View, sourceRect.Size, outputSize, round);
			outputArray = BitmapFromGPU(iB);
			return outputArray;
		}
	}*/
}