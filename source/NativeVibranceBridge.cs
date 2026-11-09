using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ResolutionSwitcher;

namespace Lucent
{
    internal sealed class NativeVibranceBridge:IDisposable
    {
        readonly Queue<Action> queue=new Queue<Action>();readonly AutoResetEvent wake=new AutoResetEvent(false);readonly Thread thread;
        bool finished,owns;int generation;NvidiaVibranceDriver driver;DesktopVibranceSession session;Mutex owner;string device;
        DesktopVibranceStore store=new DesktopVibranceStore(System.IO.Path.Combine(StateStore.Folder,"native-vibrance"));
        public NativeVibranceBridge(){thread=new Thread(Run){IsBackground=true,Name="lucent native color"};thread.Start();}
        void Run()
        {
            while(true)
            {
                Action action=null;
                lock(queue)
                {
                    if(queue.Count>0)action=queue.Dequeue();
                    else if(finished)break;
                }
                if(action!=null)action();
                else wake.WaitOne();
            }
            if(driver!=null)driver.Dispose();
            if(owner!=null)
            {
                if(owns)owner.ReleaseMutex();
                owner.Dispose();
            }
        }
        Task Submit(Action action)
        {
            var completion=new TaskCompletionSource<bool>();
            lock(queue)
            {
                if(finished)throw new ObjectDisposedException("native color");
                queue.Enqueue(()=>
                {
                    try
                    {
                        action();
                        completion.SetResult(true);
                    }
                    catch(Exception e)
                    {
                        completion.SetException(e);
                    }
                });
            }
            wake.Set();
            return completion.Task;
        }
        void Initialize()
        {
            if(driver!=null)return;
            if(owner==null)owner=new Mutex(false,LegacyNames.GameVibranceMutex);
            if(!owns)try
            {
                owns=owner.WaitOne(0);
            }
            catch(AbandonedMutexException)
            {
                owns=true;
            }
            if(!owns)throw new InvalidOperationException("Another lucent instance owns native desktop colors. Turn its vibrance off first.");
            driver=new NvidiaVibranceDriver();
            session=new DesktopVibranceSession(driver,store);
        }
        public Task Set(string target,int percent)
        {
            int stamp=Interlocked.Increment(ref generation);
            return Submit(()=>
            {
                if(stamp!=generation)return;
                Initialize();
                if(store.LoadRecovery().Count>0&&!session.Enabled)throw new InvalidOperationException("Native recovery record found. Use Recover native colors in Settings before enabling.");
                if(session.Enabled&&device!=target)
                {
                    session.Restore();
                    device=null;
                }
                if(session.Enabled)session.Adjust(percent);
                else
                {
                    session.Enable(new[]{target},percent);
                    device=target;
                }
            });
        }
        public Task Off()
        {
            Interlocked.Increment(ref generation);
            return Submit(()=>
            {
                if(session!=null&&session.HasPendingRestore)session.Restore();
            });
        }
        public Task Recover(){return Submit(()=>{Initialize();session.Recover(store.LoadRecovery());});}
        public void Dispose(){lock(queue)finished=true;wake.Set();thread.Join(2500);wake.Dispose();}
    }
}
