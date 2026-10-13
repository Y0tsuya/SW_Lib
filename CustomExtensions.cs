using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace CustomExtensions {
	public static class LinqExtension {
		public static bool AddNoRepeat<T>(this List<T> list, T item) {
			int n = list.Count();
			for (int i = 0; i < n; i++) {
				if (list[i].ToString() == item.ToString()) return false;
			}
			list.Add(item);
			return true;
		}
	}

	public static class DataGridViewExtension {
		public static void DoubleBuffered(this DataGridView dgv, bool setting) {
			Type dgvType = dgv.GetType();
			PropertyInfo pi = dgvType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
			pi.SetValue(dgv, setting, null);
		}
	}

	public static class RectangleExtension {
		public static Rectangle Expand(this Rectangle r, int border) {
			int top, left, right, bottom;
			top = r.Top - border;
			left = r.Left - border;
			right = r.Right + border;
			bottom = r.Bottom + border;
			return new Rectangle(left, top, right - left, bottom - top);
		}

		public static RectangleF Expand(this RectangleF r, float border) {
			float top, left, right, bottom;
			top = r.Top - border;
			left = r.Left - border;
			right = r.Right + border;
			bottom = r.Bottom + border;
			return new RectangleF(left, top, right - left, bottom - top);
		}
	}

	public static class StringExtension {
		public static float ToFloat(this string text) {
			return float.Parse(text);
		}

		public static double ToDouble(this string text) {
			return double.Parse(text);
		}

		public static int ToInt(this string text) {
			return Convert.ToInt32(text);
		}

		public static int HexStringToInt(this string text) {
			return Convert.ToInt32(text, 16);
		}

	}

	public static class ByteExtension {
		public static bool HasBit(this byte b, byte mask) {
			return ((b & mask) == mask);
		}

		public static byte SetBit(this byte b, byte mask) {
			return (byte)(b | mask);
		}

		public static byte ClrBit(this byte b, byte mask) {
			return (byte)(b & ~mask);
		}
	}
}
