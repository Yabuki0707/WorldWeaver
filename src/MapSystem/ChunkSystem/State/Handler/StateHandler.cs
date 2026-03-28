using System;

namespace WorldWeaver.MapSystem.ChunkSystem.State.Handler
{
    
    public enum StateExecutionResult
    {
        Success = 0,
        RetryLater = 1,
        PermanentFailure = 2
    }
    
    public abstract class StateHandler: Object
    {
        public abstract StateExecutionResult Execute(Chunk chunk);
    }
}
