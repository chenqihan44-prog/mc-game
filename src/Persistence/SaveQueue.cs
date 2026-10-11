using System;
using System.Diagnostics;
using System.Threading.Tasks;
using EmberLighthouse.World;
namespace EmberLighthouse.Persistence;

public sealed record SaveReceipt(string WorldId,string Generation,DateTime SavedUtc,double CaptureMs,double WriteMs);
public sealed class SaveQueue(Func<WorldSnapshot,string> write)
{
    private sealed class Work(WorldSnapshot snapshot,double captureMs)
    {
        public WorldSnapshot Snapshot=snapshot;
        public double CaptureMs=captureMs;
        public readonly TaskCompletionSource<SaveReceipt> Completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private readonly object gate=new();
    private Work? active,pending;
    public int BufferedSnapshots {get{lock(gate)return(active==null?0:1)+(pending==null?0:1);}}
    // Called on the simulation thread. Only a complete immutable copy crosses into the worker.
    public Task<SaveReceipt> Enqueue(WorldSnapshot snapshot)
    {
        var timer=Stopwatch.StartNew();var copy=snapshot.Copy();double captureMs=timer.Elapsed.TotalMilliseconds;
        lock(gate){
            if(active==null){active=new(copy,captureMs);var task=active.Completion.Task;_ = Task.Run(Process);return task;}
            if(active.Snapshot.WorldId!=copy.WorldId)throw new InvalidOperationException("切换世界前必须完成当前世界的保存");
            if(pending==null)pending=new(copy,captureMs);else{pending.Snapshot=copy;pending.CaptureMs=captureMs;}
            return pending.Completion.Task;
        }
    }
    private void Process()
    {
        while(true){
            Work work;lock(gate)work=active!;
            SaveReceipt? receipt=null;Exception? failure=null;
            try{var timer=Stopwatch.StartNew();string generation=write(work.Snapshot);receipt=new(work.Snapshot.WorldId,generation,DateTime.UtcNow,work.CaptureMs,timer.Elapsed.TotalMilliseconds);}
            catch(Exception error){failure=error;}
            bool next;lock(gate){active=pending;pending=null;next=active!=null;}
            if(failure!=null)work.Completion.SetException(failure);else work.Completion.SetResult(receipt!);
            if(!next)return;
        }
    }
}
