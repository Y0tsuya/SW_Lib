using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace CustomLibs {
	public class NativeFunctions {
		[DllImport("NativeFunctions.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern float FastInvSqrt(float number);
	}

}