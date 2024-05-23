using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using BepInEx;
using RWCustom;
using UnityEngine;
using GhostID = GhostWorldPresence.GhostID;
using Random = UnityEngine.Random;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace EchoRooms;

[BepInPlugin("alduris.echorooms", "Echo Room Randomizer", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    bool init;

    public void OnEnable()
    {
        On.RainWorld.PostModsInit += PostModsInit;
    }

    private void PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
    {
        orig(self);

        if (init) return;
        init = true;

        // Hooks
        try
        {
            On.OverWorld.ctor += OverWorld_ctor;
            On.GhostWorldPresence.GetGhostID += GhostWorldPresence_GetGhostID;
            On.GhostWorldPresence.GhostMode_AbstractRoom_Vector2 += GhostWorldPresence_GhostMode_AbstractRoom_Vector2;
            On.GhostWorldPresence.ctor += GhostWorldPresence_ctor;
            On.Room.Loaded += Room_Loaded;
            On.RainWorldGame.ShutDownProcess += RainWorldGame_ShutDownProcess;
            Logger.LogInfo("Ready!");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    private void RainWorldGame_ShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
    {
        orig(self);
        echoRooms.Clear();
        echoRegions.Clear();
    }

    private void Room_Loaded(On.Room.orig_Loaded orig, Room self)
    {
        if (self.game != null && echoRooms.ContainsKey(self.abstractRoom.name))
        {
            Logger.LogDebug("Placing ghost spot");
            var pos = self.MiddleOfTile(self.Tiles.GetLength(0) / 2, self.Tiles.GetLength(1) / 2);
            for (int i = 0; i < (int)Math.Sqrt(self.Tiles.Length * 2); i++)
            {
                int x = Random.Range(1, self.Tiles.GetLength(0) - 1);
                int y = Random.Range(1, self.Tiles.GetLength(1) - 1);
                if (!self.Tiles[x, y].Solid)
                {
                    pos = self.MiddleOfTile(x, y);
                    break;
                }
            }

            self.roomSettings.placedObjects.Add(new PlacedObject(PlacedObject.Type.GhostSpot, null) { pos = pos });
            Logger.LogDebug($"Placed at ({pos.x}, {pos.y})");
        }
        orig(self);
    }

    private void GhostWorldPresence_ctor(On.GhostWorldPresence.orig_ctor orig, GhostWorldPresence self, World world, GhostID ghostID)
    {
        orig(self, world, ghostID);
        if (echoRooms.Count > 0)
        {
            foreach (var kv in echoRooms)
            {
                if (kv.Value == ghostID)
                {
                    Logger.LogDebug("RANDOM ECHO ROOM (" + ghostID.value + ", " + kv.Key + ")");
                    self.ghostRoom = world.GetAbstractRoom(kv.Key);
                    break;
                }
            }
        }
    }

    private float GhostWorldPresence_GhostMode_AbstractRoom_Vector2(On.GhostWorldPresence.orig_GhostMode_AbstractRoom_Vector2 orig, GhostWorldPresence self, AbstractRoom testRoom, Vector2 worldPos)
    {
        // int degreesSep = Util.DegreesOfSeparation(self.ghostRoom, testRoom);
        if (self.ghostRoom == null) return 0f;
        int degreesSep = self.DegreesOfSeparation(testRoom);
        if (degreesSep == -1) return 0f;

        var dist = Custom.RestrictInRect(worldPos, FloatRect.MakeFromVector2(self.world.RoomToWorldPos(default, self.ghostRoom.index), self.world.RoomToWorldPos(self.ghostRoom.size.ToVector2() * 20f, self.ghostRoom.index)));
        return Mathf.Pow(Mathf.InverseLerp(4000f, 500f, Vector2.Distance(worldPos, dist)), 2f) * Custom.LerpMap(degreesSep, 1f, 3f, 0.6f, 0.15f) * ((testRoom.layer == self.ghostRoom.layer) ? 1f : 0.6f);
    }

    private GhostID GhostWorldPresence_GetGhostID(On.GhostWorldPresence.orig_GetGhostID orig, string regionName)
    {
        var result = orig(regionName);
        if (echoRooms.Count == 0)
        {
            Logger.LogDebug("No echoes at all");
            return result;
        }

        if (echoRegions.ContainsKey(regionName))
        {
            var room = echoRegions[regionName];
            Logger.LogDebug("GHOST IN REGION: " + room);
            return echoRooms[room];
        }
        else
        {
            return GhostID.NoGhost;
        }
    }

    private static readonly Dictionary<string, string> echoRegions = [];
    private static readonly Dictionary<string, GhostID> echoRooms = [];
    private void OverWorld_ctor(On.OverWorld.orig_ctor orig, OverWorld self, RainWorldGame game)
    {
        if (game.IsStorySession)
        {
            // Get regions and rooms
            var regionList = SlugcatStats.SlugcatStoryRegions(game.StoryCharacter).Concat(SlugcatStats.SlugcatOptionalRegions(game.StoryCharacter)).ToList();
            if (regionList.Count == 0)
            {
                regionList = Region.GetFullRegionOrder();
            }
            regionList = [.. regionList]; // copy because idk why not
            var roomList = RainWorld.roomNameToIndex.Keys.ToArray();

            // Generate echo rooms
            echoRooms.Clear();
            echoRegions.Clear();

            foreach (var ghost in GhostID.values.entries)
            {
                if (ghost == "NoGhost") continue;

                string room;
                int i = 0;
                do
                {
                    room = roomList[Random.Range(0, roomList.Length)];
                }
                while ((room.ToUpperInvariant().Contains("OFFSCREEN") || i++ < roomList.Length / 4) && !regionList.Any(x => room.ToUpperInvariant().StartsWith(x.ToUpperInvariant())));

                var region = room.Split(['_'], 2)[0];
                echoRooms[room] = new GhostID(ghost, false);
                echoRegions[region] = room;
                regionList.Remove(region);

                // Debug info
                if (!game.GetStorySession.saveState.deathPersistentSaveData.ghostsTalkedTo.TryGetValue(echoRooms[room], out int visited))
                {
                    visited = game.GetStorySession.saveState.deathPersistentSaveData.ghostsTalkedToUnrecognized.Any(x => x == ghost) ? 2 : 0;
                }
                Logger.LogDebug($"{ghost} echo: {room} (visited: {visited})");
            }
        }

        // We have to run before orig because orig calls a method we hooked
        orig(self, game);
    }

}
