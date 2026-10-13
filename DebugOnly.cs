using System.Diagnostics;

namespace CustomLibs {
	public class DebugStopwatch {
		private Stopwatch DbgStopWatch;

		public DebugStopwatch() {
#if DEBUG
			DbgStopWatch = new Stopwatch();
#endif
		}

		public void Start() {
#if DEBUG
			DbgStopWatch.Start();
#endif
		}

		public void Stop() {
#if DEBUG
			DbgStopWatch.Stop();
#endif
		}

		public void Restart() {
#if DEBUG
			DbgStopWatch.Restart();
#endif
		}

		public void Reset() {
#if DEBUG
			DbgStopWatch.Reset();
#endif
		}

		public TimeSpan Elapsed {
			get {
#if DEBUG
				return DbgStopWatch.Elapsed;
#else
				return new TimeSpan();
#endif
			}
		}

		public long ElapsedTicks {
			get {
#if DEBUG
				return DbgStopWatch.ElapsedTicks;
#else
				return 0;
#endif
			}
		}

		public long ElapsedMilliseconds {
			get {
#if DEBUG
				return DbgStopWatch.ElapsedMilliseconds;
#else
				return 0;
#endif
			}
		}

		public bool IsRunning {
			get {
#if DEBUG
				return DbgStopWatch.IsRunning;
#else
				return false;
#endif
			}
		}

		public void StopAndPrintElapsed(string annotation) {
#if DEBUG
			DbgStopWatch.Stop();
			Debug.WriteLine(String.Format("{0} {1}ms", annotation, DbgStopWatch.ElapsedMilliseconds));
#endif
		}

	}
}