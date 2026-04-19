using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Linq;
using CSTimer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace CS2_Extra_Spawns
{
    [MinimumApiVersion(80)]
    public class CS2_Extra_Spawns : BasePlugin
    {
        public override string ModuleName => "CS2-Extra-Spawns";
        public override string ModuleVersion => "1.1.0";
        public override string ModuleAuthor => "|ZAPS| BONE";

        private const int MaxSpawnsPerTeam = 32;
        private static readonly Vector[] Offsets =
        {
            new Vector(5, 0, 5),
            new Vector(-5, 0, 5),
            new Vector(0, 5, 5),
            new Vector(0, -5, 5)
        };

        private CSTimer? _solidTeammatesTimer;

        public override void Load(bool hotReload)
        {
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);

            AddCommand("css_spawns", "Generate extra spawns for T and CT", (player, info) =>
            {
                AddExtraSpawns("info_player_terrorist");
                AddExtraSpawns("info_player_counterterrorist");
                info.ReplyToCommand("[ExtraSpawns] Extra spawns generated.");
            });
        }

        public override void Unload(bool hotReload)
        {
            _solidTeammatesTimer?.Kill();
            _solidTeammatesTimer = null;
        }

        private void OnMapStart(string map)
        {
            // Delay to let map entities load before first round
            AddTimer(1.0f, () =>
            {
                AddExtraSpawns("info_player_terrorist");
                AddExtraSpawns("info_player_counterterrorist");
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }

        private HookResult OnRoundStart(EventRoundStart ev, GameEventInfo info)
        {
            _solidTeammatesTimer?.Kill();
            _solidTeammatesTimer = null;

            // Recreate spawns every round -- CS2 can clean up dynamic entities between rounds
            AddExtraSpawns("info_player_terrorist");
            AddExtraSpawns("info_player_counterterrorist");

            // Force collisions OFF so overlapping players can separate
            Server.ExecuteCommand("mp_solid_teammates 0");
            Server.PrintToConsole("[ExtraSpawns] mp_solid_teammates set to 0 at round start.");

            // Restore collisions after 3 seconds
            _solidTeammatesTimer = AddTimer(3.0f, () =>
            {
                Server.ExecuteCommand("mp_solid_teammates 2");
                Server.PrintToConsole("[ExtraSpawns] mp_solid_teammates restored to 2.");
                _solidTeammatesTimer = null;
            }, TimerFlags.STOP_ON_MAPCHANGE);

            return HookResult.Continue;
        }

        private static bool AddExtraSpawns(string className)
        {
            var spawns = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>(className).ToList();
            if (spawns.Count == 0)
                return false;

            int needed = MaxSpawnsPerTeam - spawns.Count;
            if (needed <= 0)
                return false;

            int created = 0;

            foreach (var spawn in spawns)
            {
                if (created >= needed)
                    break;

                var origin = spawn.AbsOrigin;
                var angles = spawn.AbsRotation;

                if (origin == null || angles == null)
                    continue;

                foreach (var offset in Offsets)
                {
                    if (created >= needed)
                        break;

                    var newPos = origin + offset;

                    var newSpawn = Utilities.CreateEntityByName<CBaseEntity>(className);
                    if (newSpawn != null)
                    {
                        newSpawn.Teleport(newPos, angles, null);
                        newSpawn.DispatchSpawn();
                        created++;
                    }
                }
            }

            if (created > 0)
                Server.PrintToConsole($"[ExtraSpawns] Added {created} spawns for {className}.");

            return created > 0;
        }
    }
}
