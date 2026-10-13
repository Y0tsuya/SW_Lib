using System;
using System.Drawing;
using System.Drawing.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using HeyRed.ImageSharp.Heif.Formats.Avif;
using HeyRed.ImageSharp.Heif.Formats.Heif;
using SixLabors.ImageSharp.Formats.Png;

// SimplePixViewer v4.4.0	Initial version

namespace SixLabors.ImageSharp.Avif {

	public class AvifLib {

		public static MemoryStream LoadAvifToStream(string filename) {
			// Configure the custom decoder modules
			var decoderOptions = new DecoderOptions() {
				Configuration = new Configuration(
					new AvifConfigurationModule(),
					new HeifConfigurationModule()
				)
			};

			// Decode the AVIF file
			SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(decoderOptions, filename);
			MemoryStream outStream = new MemoryStream();
			image.Save(outStream, new PngEncoder());
			outStream.Seek(0, SeekOrigin.Begin);
			return outStream;
		}

		public static System.Drawing.Image LoadAvifToImage(string filename) {
			return new Bitmap(LoadAvifToStream((string)filename));
		}

		public static MemoryStream LoadAvifToStream(MemoryStream inStream) {
			// Configure the custom decoder modules
			var decoderOptions = new DecoderOptions() {
				Configuration = new Configuration(
					new AvifConfigurationModule(),
					new HeifConfigurationModule()
				)
			};

			// Decode the AVIF file
			SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(decoderOptions, inStream);
			MemoryStream outStream = new MemoryStream();
			image.Save(outStream, new PngEncoder());
			outStream.Seek(0, SeekOrigin.Begin);
			return outStream;
		}

		public static System.Drawing.Image LoadAvifToImage(MemoryStream inStream) {
			return new Bitmap(LoadAvifToStream(inStream));
		}

	}
}