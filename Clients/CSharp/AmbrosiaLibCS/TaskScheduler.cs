using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// From: https://codereview.stackexchange.com/questions/43000/a-taskscheduler-that-always-run-tasks-in-a-specific-thread
/// </summary>
public class SameThreadTaskScheduler : System.Threading.Tasks.TaskScheduler, IDisposable
{
    #region publics
    public SameThreadTaskScheduler(string name)
    {
        scheduledTasks = new Queue<Task>();
        threadName = name;
    }
    public override int MaximumConcurrencyLevel { get { return 1; } }
    public void Dispose()
    {
        lock (scheduledTasks)
        {
            quit = true;
            Monitor.PulseAll(scheduledTasks);
        }
    }
    #endregion

    #region protected overrides
    protected override IEnumerable<Task> GetScheduledTasks()
    {
        lock (scheduledTasks)
        {
            return scheduledTasks.ToList();
        }
    }
    public int NumberOfScheduledTasks()
    {
        lock (scheduledTasks)
        {
            return scheduledTasks.Count;
        }
    }
    protected override void QueueTask(Task task)
    {
        if (myThread == null)
            myThread = StartThread(threadName);
        if (!myThread.IsAlive)
            throw new ObjectDisposedException("My thread is not alive, so this object has been disposed!");
        lock (scheduledTasks)
        {
            scheduledTasks.Enqueue(task);
            Monitor.PulseAll(scheduledTasks);
        }
    }
    protected override bool TryExecuteTaskInline(Task task, bool task_was_previously_queued)
    {
        return false;
    }
    #endregion

    private readonly Queue<Task> scheduledTasks;
    private Thread myThread;
    private readonly string threadName;
    private bool quit;

    private Thread StartThread(string name)
    {
        var t = new Thread(MyThread) { Name = name, IsBackground = true};
        using (var start = new Barrier(2))
        {
            t.Start(start);
            ReachBarrier(start);
        }
        return t;
    }
    private void MyThread(object o)
    {
        Task tsk;
        lock (scheduledTasks)
        {
            //When reaches the barrier, we know it holds the lock.
            //
            //So there is no Pulse call can trigger until
            //this thread starts to wait for signals.
            //
            //It is important not to call StartThread within a lock.
            //Otherwise, deadlock!
            ReachBarrier(o as Barrier);
            tsk = WaitAndDequeueTask();
        }
        for (; ; )
        {
            if (tsk == null)
                break;
            TryExecuteTask(tsk);
            lock (scheduledTasks)
            {
                tsk = WaitAndDequeueTask();
            }
        }
    }
    private Task WaitAndDequeueTask()
    {
        while (!scheduledTasks.Any() && !quit)
            Monitor.Wait(scheduledTasks);
        return quit ? null : scheduledTasks.Dequeue();
    }

    private static void ReachBarrier(Barrier b)
    {
        if (b != null)
            b.SignalAndWait();
    }
}
