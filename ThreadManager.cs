using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using static System.Resources.ResXFileRef;


namespace CustomLibs {
	public abstract class ThreadManager {

		protected EventWaitHandle WaitWorkerLaunch;
		protected Thread[]? WorkerThreads;
		public bool ShouldCooperativeExit;
		public ThreadPriority Priority;
		public Action Handoff;
		protected object ListLock;

		public ThreadManager() {
			ShouldCooperativeExit = false;
			ListLock = new object();
			Priority = ThreadPriority.Normal;
			Handoff = System.Windows.Forms.Application.DoEvents;
			WaitWorkerLaunch = new AutoResetEvent(false);
		}

		/*
		AddEvents();	// optional
		CreateWorkers(Cores);
		ExitWorkers();
		*/

		virtual public void CreateWorkers(int num) {
			CreateWorkers(Worker, ref WorkerThreads, num);
		}

		virtual public void CreateWorkers(Action<int> worker, ref Thread[]? threads, int num) {
			int i;

			ShouldCooperativeExit = false;
			threads = new Thread[num];
			lock (ListLock) {
				for (i = 0; i < num; i++) {
					threads[i] = new Thread(() => worker(i));
					threads[i].Name = i.ToString();
					threads[i].Priority = Priority;
					threads[i].Start();
					WaitWorkerLaunch.WaitOne();
				}
			}
		}

		virtual public void ExitWorkers() {
			Debug.Assert(WorkerThreads != null);
			ExitWorkers(WorkerThreads);
		}

		// Wait for workers to exit
		virtual public void ExitWorkers(Thread[]? threads) {
			int i;
			bool alive;

			if (threads == null) return;
			do {
				Thread.Sleep(100);
				Handoff();
				alive = false;
				for (i = 0; i < threads.Length; i++) {
					alive = alive | threads[i].IsAlive;
				}
			} while (alive);
			threads = null;
		}

		virtual public void AbortWorkers() {
			AbortWorkers(WorkerThreads, null);
		}

		virtual public void AbortWorkers(EventWaitHandle[]? extraKick) {
			AbortWorkers(WorkerThreads, extraKick);
		}

		// Signal for workers to exit
		virtual public void AbortWorkers(Thread[]? threads, EventWaitHandle[]? extraKick) {
			int i;
			bool alive;

			ShouldCooperativeExit = true;
			if (extraKick != null) {
				for (i = 0; i < extraKick.Length; i++) {
					extraKick[i].Set();
				}
			}
			if (threads == null) return;
			do {
				Thread.Sleep(100);
				Handoff();
				alive = false;
				for (i = 0; i < threads.Length; i++) {
					alive = alive | threads[i].IsAlive;
				}
			} while (alive);
			threads = null;
		}

		virtual public void AddEvents(ref EventWaitHandle[]? handles) {
			Debug.Assert(WorkerThreads != null);
			AddEvents(ref handles, WorkerThreads.Length);
		}

		virtual public void AddEvents(ref EventWaitHandle[]? handles, int num) {
			int i;
			handles = new AutoResetEvent[num];
			for (i = 0; i < num; i++) {
				handles[i] = new AutoResetEvent(false);
			}
		}

		virtual public void SendEvent(EventWaitHandle[]? handles) {
			int i;
			Debug.Assert(handles != null);
			int c = handles.Length;
			for (i = 0; i < c; i++) {
				handles[i].Set();
			}
		}

		virtual public void WaitForEvent(EventWaitHandle[]? handles) {
			int i;
			Debug.Assert(handles != null);
			int c = handles.Length;
			for (i = 0; i < c; i++) {
				handles[i].WaitOne();
			}
		}

		abstract public void Worker(int threadID);

		virtual public double GetProgress() {
			return 0.0;
		}

	}

	public abstract class ThreadManagerSharedList : ThreadManager {

		protected bool[]? HandledList;
		public int HandledIndex;

		public ThreadManagerSharedList() : base() {
			HandledIndex = -1;
		}

		public int NumItems {
			set {
				HandledList = new bool[value];  // should all be false
			}
		}

		protected int LockIndex() {
			int myIndex;
			Thread.Sleep(10);
			if (HandledList == null) return -1;
			lock (ListLock) {
				for (myIndex = 0; myIndex < HandledList.Length; myIndex++) {
					if (HandledList[myIndex] == false) {
						HandledList[myIndex] = true;
						HandledIndex = myIndex;
						return myIndex;
					}
				}
				HandledIndex = -1;
				return -1;
			}
		}

		override public double GetProgress() {
			if (HandledList == null) return 0.0;
			double percent = 100.0 * (double)HandledIndex / (double)HandledList.Length;
			return percent;
		}

	}
}