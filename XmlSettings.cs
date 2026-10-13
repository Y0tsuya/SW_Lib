using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Windows.Forms.VisualStyles;
using System.Drawing;
using System.Runtime.InteropServices;

// v1.0	Release
// v1.1 Add Clear() function
//		Preserve white space when loading
// FolderBot v1.8.5:	Add generic Read<T>

namespace CustomLibs {
	public class XmlSettings {

		XDocument Doc;
		XElement Root;
		string FileName;
		string Product;

		public XmlSettings() {
			string startup = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			startup = startup.Replace("file:\\", "");
			FileName = startup + "\\settings.xml";
			Product = System.Windows.Forms.Application.ProductName;
		}

		public void Load() {
			try {
				Doc = XDocument.Load(FileName, LoadOptions.PreserveWhitespace);
				Root = Doc.Root;
			} catch (IOException ex) {
				Root = new XElement(Product);
			}
		}

		public void Clear() {
			Root.RemoveAll();
		}

		public void Save() {
			try {
				Root.Save(FileName);
			} catch (IOException ex) {
			}
		}

		public void Backup() {
			if (File.Exists(FileName)) {
				File.Copy(FileName, FileName + ".bak", true);
			}
		}

		private string GetXmlString(XElement element) {
			string rstring;

			try {
				rstring = element.Value;
				return rstring;
			} catch (Exception ex) {
				return "";
			}
		}

		public bool Exists(string key) {
			XElement element = Root.Element(key);
			if (element == null) return false;
			else return true;
		}

		private void AddSubElement(string key, string text) {
			if (!Exists(key)) {
				XElement element;
				element = new XElement(key, text);
				Root.Add(element);
			}
		}

		private void RemoveSubElement(string key) {
			if (Exists(key)) {
				XElement element;
				element = Root.Element(key);
				element.Remove();
			}
		}

		public void Remove(string key) {
			RemoveSubElement(key);
		}

		private string MergeStringArray(string[] arr) {
			if (arr == null) return "";
			if (arr.Length == 0) return "";
			StringBuilder sb = new StringBuilder();
			foreach (string filter in arr) {
				sb.Append(filter);
				sb.Append(" ");
			}
			return sb.ToString().Trim();
		}

		/////////////////////////////////////////////////////////////////
		///	Read overloads

		public bool Read(string key, bool def) {
			string valstring;
			bool val;

			if (Exists(key)) {
				valstring = GetXmlString(Root.Element(key));
				if (bool.TryParse(valstring, out val)) {
					return val;
				} else {
					return def;
				}
			} else {
				AddSubElement(key, def.ToString());
				return def;
			}
		}

		public int Read(string key, int def) {
			string valstring;
			int val;

			if (Exists(key)) {
				valstring = GetXmlString(Root.Element(key));
				if (Int32.TryParse(valstring, out val)) {
					return val;
				} else {
					return def;
				}
			} else {
				AddSubElement(key, def.ToString());
				return def;
			}
		}

		public double Read(string key, double def) {
			string valstring;
			double val;

			if (Exists(key)) {
				valstring = GetXmlString(Root.Element(key));
				if (Double.TryParse(valstring, out val)) {
					return val;
				} else {
					return def;
				}
			} else {
				AddSubElement(key, def.ToString());
				return def;
			}
		}

		public string Read(string key, string def) {
			string valstring;

			if (Exists(key)) {
				valstring = GetXmlString(Root.Element(key));
				return valstring;
			} else {
				AddSubElement(key, def);
				return def;
			}
		}

		public T Read<T>(string key, T def) {
			string valstring;
			T tv;

			if (Exists(key)) {
				valstring = GetXmlString(Root.Element(key));
				foreach (string en in Enum.GetNames(typeof(T))) {
					if (en == valstring) {
						return (T)Enum.Parse(typeof(T), valstring);
					}
				}
				return def;
			} else {
				AddSubElement(key, def.ToString());
				return def;
			}
		}

		public string[] Read(string key, string[] def) {
			string valstring;
			string[] arr;

			if (Exists(key)) {
				valstring = GetXmlString(Root.Element(key));
				arr = valstring.Split(new char[] { ' ', ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries);
				return arr;
			} else {
				AddSubElement(key, MergeStringArray(def));
				return def;
			}
		}

		public Size Read(string key, Size def) {
			string valstring;

			if (Exists(key)) {
				valstring = GetXmlString(Root.Element(key));
				string[] sz = valstring.Split('x');
				return new Size(Int32.Parse(sz[0]), Int32.Parse(sz[1]));
			} else {
				AddSubElement(key, def.Width.ToString() + "x" + def.Height.ToString());
				return def;
			}
		}

		public Color Read(string key, Color def) {
			string valstring;
			int a, r, g, b;

			if (Exists(key)) {
				valstring = GetXmlString(Root.Element(key));
				try {
					a = Convert.ToInt32(valstring.Substring(0, 2), 16);
					r = Convert.ToInt32(valstring.Substring(2, 2), 16);
					g = Convert.ToInt32(valstring.Substring(4, 2), 16);
					b = Convert.ToInt32(valstring.Substring(6, 2), 16);
					return Color.FromArgb(a, r, g, b);
				} catch (Exception ex) {
					return def;
				}
			} else {
				AddSubElement(key, String.Format("{0:X02}{1:X02}{2:X02}{3:X02}", def.A, def.R, def.G, def.B));
				return def;
			}
		}


		public Font Read(string key, Font def) {
			string valstring;
			string[] delims = new string[] { ",", "px" };
			string[] split;
			string fontName;
			float fontSize;
			FontStyle fontStyle;

			if (Exists(key)) {
				valstring = GetXmlString(Root.Element(key));
				split = valstring.Split(delims, StringSplitOptions.RemoveEmptyEntries);
				if (split.Length < 3) return def;
				fontName = split[0].Trim();
				try {
					fontSize = (float)Convert.ToDouble(split[1]);
				} catch (Exception ex) {
					return def;
				}
				switch (split[2].Trim()) {
					case "Bold": fontStyle = FontStyle.Bold; break;
					case "Italic": fontStyle = FontStyle.Italic; break;
					case "Regular": fontStyle = FontStyle.Regular; break;
					case "Underline": fontStyle = FontStyle.Underline; break;
					case "Strikeout": fontStyle = FontStyle.Strikeout; break;
					default: fontStyle = FontStyle.Regular; break;
				}
				return new Font(fontName, fontSize, fontStyle);
			} else {
				AddSubElement(key, String.Format("{0}, {1:F1}px, {2}", def.Name, def.Size, def.Style));
				return def;
			}
		}

		/////////////////////////////////////////////////////////////////
		///	Write overloads

		public void Write(string key, bool val) {
			if (Exists(key)) {
				Root.Element(key).Value = val.ToString();
			} else {
				AddSubElement(key, val.ToString());
			}
		}

		public void Write(string key, int val) {
			if (Exists(key)) {
				Root.Element(key).Value = val.ToString();
			} else {
				AddSubElement(key, val.ToString());
			}
		}

		public void Write(string key, double val) {
			if (Exists(key)) {
				Root.Element(key).Value = val.ToString();
			} else {
				AddSubElement(key, val.ToString());
			}
		}

		public void Write(string key, string val) {
			if (Exists(key)) {
				Root.Element(key).Value = val;
			} else {
				AddSubElement(key, val);
			}
		}

		public void Write(string key, string[] arr) {
			string val = MergeStringArray(arr);
			if (Exists(key)) {
				Root.Element(key).Value = val;
			} else {
				AddSubElement(key, val);
			}
		}

		public void Write(string key, Size size) {
			string val = size.Width.ToString() + "x" + size.Height.ToString();
			if (Exists(key)) {
				Root.Element(key).Value = val;
			} else {
				AddSubElement(key, val);
			}
		}

		public void Write(string key, Color clr) {
			string val = String.Format("{0:X02}{1:X02}{2:X02}{3:X02}", clr.A, clr.R, clr.G, clr.B);
			if (Exists(key)) {
				Root.Element(key).Value = val;
			} else {
				AddSubElement(key, val);
			}
		}

		public void Write(string key, Font font) {
			string val = String.Format("{0}, {1:F1}px, {2}", font.Name, font.Size, font.Style);
			if (Exists(key)) {
				Root.Element(key).Value = val;
			} else {
				AddSubElement(key, val);
			}
		}

	}

}