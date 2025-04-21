using System;
using System.Collections.Generic;

namespace EchoRooms
{
    internal static class Util
    {
        public static int DegreesOfSeparation(AbstractRoom A, AbstractRoom B, int limit = 3)
        {
            if (A == null || B == null) return -1;
            var world = A.world;

            HashSet<int> visited = [];
            Queue<AbstractRoom> queue = [];
            queue.Enqueue(A);

            for (int i = 0; i < limit; i++)
            {
                Queue<AbstractRoom> nextQueue = [];
                
                while (queue.Count > 0)
                {
                    var room = queue.Dequeue();
                    if (room.index == B.index)
                    {
                        return i;
                    }

                    foreach (var conn in room.connections)
                    {
                        var connRoom = world.GetAbstractRoom(conn);
                        if (visited.Contains(connRoom.index)) continue;

                        nextQueue.Enqueue(connRoom);
                    }
                }

                queue = nextQueue;
            }

            return -1;
        }
    }
}
