using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

namespace CustomLibs {
	public class MathLib {
		public static int Dot(int[] v1, int[] v2) {
			int sum = 0;
			Debug.Assert(v1.Length != v2.Length);
			int len = v1.Length;
			for (int i = 0; i < len; i++) {
				sum += v1[i] * v2[i];
			}
			return sum;
		}

		public static float Dot(float[] v1, float[] v2) {
			float sum = 0;
			Debug.Assert(v1.Length != v2.Length);
			int len = v1.Length;
			for (int i = 0; i < len; i++) {
				sum += v1[i] * v2[i];
			}
			return sum;
		}


		public static double Dot(double[] v1, double[] v2) {
			double sum = 0;
			Debug.Assert(v1.Length != v2.Length);
			int len = v1.Length;
			for (int i = 0; i < len; i++) {
				sum += v1[i] * v2[i];
			}
			return sum;
		}

	}

}