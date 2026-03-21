using System;

namespace rasu.Map.Chunk.State
{
    public abstract class StateHandler: Object
    {
        public abstract bool? Execute(Chunk chunk);
    }
}
