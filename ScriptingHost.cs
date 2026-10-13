using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CustomLibs;
using CustomExtensions;
//using static System.Net.Mime.MediaTypeNames;

// UnifiedProxyControlBase is shared by both the parent application
// and the ScriptingHost

namespace Scripting {
	public class UnifiedProxyControlBase {
		public string[]? ScriptArgs;
		public Func<int, string, int, int, int> LogCallBack;
		public Action<int, int> ProgressCallBack;
		public Action? AbortOnError; // used by script to indicate error
		public bool ShouldAbortAndCleanuping;
		public int LogParentIndex;

		/////////////////////////////////////////////////
		///////////    General Text and UI    ///////////
		/////////////////////////////////////////////////

		// Get script arguments from the UI
		public string[]? Args {
			get {
				return ScriptArgs;
			}
		}

		// Add a message to the application logger
		public void AddLogInfo(string text) {
			if (LogCallBack != null) LogCallBack(CallBacks.LOG_SUB, text, CallBacks.LOG_INFO, LogParentIndex);
		}

		public void AddLogWarning(string text) {
			if (LogCallBack != null) LogCallBack(CallBacks.LOG_SUB, text, CallBacks.LOG_WARNING, LogParentIndex);
		}

		public void AddLogError(string text) {
			if (LogCallBack != null) LogCallBack(CallBacks.LOG_SUB, text, CallBacks.LOG_ERROR, LogParentIndex);
		}

		// Manipulate the progress bar
		public void SetProgressIndicator(int value, int max) {
			if (ProgressCallBack != null) ProgressCallBack(value, max);
		}

	}


	// ScriptingHostBase needs to be inherited by a class to fill out the
	// CsScriptWorker abstract method
	public abstract class ScriptingHostBase {
		public bool HasScriptingError;
		private Thread? ScriptThread;
		public bool IsScriptBusy = false;
		protected string? ScriptText;
		//public string[]? ScriptFile;
		protected string[]? ScriptArgs;
		protected string[]? ScriptSearchPaths;
		protected string? ScriptBaseDirectory;
		protected UnifiedProxyControlBase? ProxyControl;
		protected EventHandler? ClickHandler;
		public Func<int, string, int, int, int> LogCallBack;
		public int LogParentIndex;
		protected System.Timers.Timer ThreadMonitorTimer;
		public EventHandler? ScriptExitHandler;

		public ScriptingHostBase() {
			ThreadMonitorTimer = new System.Timers.Timer();
			ThreadMonitorTimer.AutoReset = true;
			ThreadMonitorTimer.Enabled = true;
			ThreadMonitorTimer.Elapsed += ThreadMonitorTimer_Tick;
			ThreadMonitorTimer.Interval = 1000;
		}

		//private void ThreadMonitorTimer_Tick(object sender, EventArgs e) {
		private void ThreadMonitorTimer_Tick(object sender, System.Timers.ElapsedEventArgs e) {
			if (ScriptThread == null) return;
			if (!ScriptThread.IsAlive) {
				if (HasScriptingError) {
					if (LogCallBack != null) LogCallBack(CallBacks.LOG_SUB, "Script ended with errors", CallBacks.LOG_WARNING, LogParentIndex); 
				} else {
					if (LogCallBack != null) LogCallBack(CallBacks.LOG_SUB, "Script completed", CallBacks.LOG_INFO, LogParentIndex);
				}
				ThreadMonitorTimer.Stop();
				IsScriptBusy = false;
				ScriptExitHandler?.Invoke(this, new EventArgs());
			}
		}

		public bool RunScriptByNameBlocking(string? fileName, string args) {
			if (RunScriptByName(fileName, args)) {
				WaitForWorker();
				return true;
			} else return false;
		}

		public bool RunScriptByName(string? fileName, string args) {
			char[] argd = new char[] { ' ' };
			Debug.Assert(ProxyControl != null);
			Debug.Assert(fileName != null);
			IsScriptBusy = true;
			HasScriptingError = false;
			ProxyControl.ShouldAbortAndCleanuping = false;
			ScriptArgs = args.Split(argd);
			ProxyControl.ScriptArgs = ScriptArgs;
			ProxyControl.AbortOnError = AbortScriptWithError;
			// start script here
			ScriptSearchPaths = new string[] { };
			ScriptBaseDirectory = Path.GetDirectoryName(fileName);
			try {
				ScriptText = File.ReadAllText(fileName);
			} catch (IOException ex) {
				if (LogCallBack != null) LogCallBack(CallBacks.LOG_SUB, ex.Message, CallBacks.LOG_ERROR, LogParentIndex);
				HasScriptingError = true;
				return false;
			}
			ScriptThread = new Thread(CsScriptWorker);
			ScriptThread.Start();
			ThreadMonitorTimer.Start();
			return true;
		}

		public bool RunScriptByTextBlocking(string scriptText, string args) {
			if (RunScriptByText(scriptText, args)) {
				WaitForWorker();
				return true;
			}
			return false;
		}

		public bool RunScriptByText(string scriptText, string args) {
			char[] argd = new char[] { ' ' };
			IsScriptBusy = true;
			HasScriptingError = false;
			ProxyControl.ShouldAbortAndCleanuping = false;
			ScriptArgs = args.Split(argd);
			ProxyControl.ScriptArgs = ScriptArgs;
			ProxyControl.AbortOnError = AbortScriptWithError;
			ScriptText = scriptText;
			ScriptThread = new Thread(CsScriptWorker);
			ScriptThread.Start();
			return true;
		}

		public void WaitForWorker() {
			if (ScriptThread != null) {
				if (ScriptThread.IsAlive) {
					while (ScriptThread.IsAlive) {
						Thread.Sleep(100);
						Application.DoEvents();
					}

				}
			}
		}

		public void AbortScript() {
			Debug.Assert(ProxyControl != null);
			ProxyControl.ShouldAbortAndCleanuping = true;
			if (ScriptThread != null) {
				if (ScriptThread.IsAlive) {
					try {
						ScriptThread.Abort();
					} catch (PlatformNotSupportedException ex) {
						// nothing doing
						string buf = ex.Message;
					}
					while (ScriptThread.IsAlive) {
						Thread.Sleep(100);
						Application.DoEvents();
					}

				}
			}
			IsScriptBusy = false;
		}

		public void AbortScriptWithError() {
			HasScriptingError = true;
		}

		protected string FindCodeFragmentFromError(string errorMsg) {
			string linenumtxt;
			int c, linenum;

			Debug.Assert(ScriptText != null);
			if (errorMsg[0] != '(') return "";
			for (c = 1; c < errorMsg.Length; c++) {
				if (errorMsg[c] == ',') break;
			}
			if (c == errorMsg.Length) return "";
			linenumtxt = errorMsg.Substring(1, c - 1);
			linenum = Convert.ToInt32(linenumtxt);
			string[] scriptlines = ScriptText.Split(new char[] { '\n' });
			return "Line " + linenum + ": " + scriptlines[linenum - 1].Trim();
		}

		protected string ParseStackTrace(string trace) {
			Debug.Assert(ScriptText != null);
			string ns = GetString(@"in :line ([0-9]{1,5})\r\n", ref trace);
			if (ns != "") {
				int linenum = Int32.Parse(ns);
				string[] scriptlines = ScriptText.Split(new char[] { '\n' });
				return "Line " + linenum + ": " + scriptlines[linenum - 1].Trim();
			} else {
				return "";
			}
		}

		public static string GetString(string searchstring, ref string blob) {
			string value;

			Regex rgx = new Regex(searchstring);
			Match match = rgx.Match(blob);
			if (match.Success) {
				value = rgx.Replace(match.Value, "$1");
				return value;
			} else {
				return "";
			}
		}

		public abstract void CsScriptWorker();

	}
}
