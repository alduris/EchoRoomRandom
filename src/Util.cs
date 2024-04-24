using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EchoRooms
{
    internal static class Util
    {
        public static List<string> SlugcatAccessibleRooms(SlugcatStats.Name slugcat)
        {
            // Thanks Vigaro for world loading code
            var rooms = new List<string>();
            var regions = Region.LoadAllRegions(slugcat);

            foreach (var region in regions)
            {
                var worldLoader = new WorldLoader(null, slugcat, false, region.name, region, RainWorld.LoadSetupValues(true));
                worldLoader.NextActivity();
                while (!worldLoader.Finished)
                {
                    worldLoader.Update();
                }
                
                foreach (var room in worldLoader.abstractRooms)
                {
                    rooms.Add(room.name);
                }
            }

            return rooms;
        }

        public static int DegreesOfSeparation(AbstractRoom A, AbstractRoom B, int limit = 3)
        {
            if (A == null || B == null) return -1;

            // todo:

            return -1;
        }

        private static int GetRooms(AbstractRoom room, int curr, int limit)
        {
            // todo: do I need a helper?
        }
    }
}
