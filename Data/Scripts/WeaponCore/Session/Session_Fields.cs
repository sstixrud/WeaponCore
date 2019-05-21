﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Collections;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Projectiles.Projectiles;

namespace WeaponCore
{
    public partial class Session
    {
        public uint Tick;
        private int _count = -1;
        private int _lCount;
        private int _eCount;
        private int _pCounter;
        private double _syncDistSqr;

        private readonly MyConcurrentPool<List<LineD>> _linePool = new MyConcurrentPool<List<LineD>>();
        private readonly MyConcurrentPool<List<Shot>> _shotPool = new MyConcurrentPool<List<Shot>>();

        private MyEntity3DSoundEmitter SoundEmitter { get; set; } = new MyEntity3DSoundEmitter(null)
        {
            CustomMaxDistance = float.MaxValue,
        };

        private readonly Projectiles.Projectiles _projectiles = new Projectiles.Projectiles();

        internal static Session Instance { get; private set; }

        internal const ushort PACKET_ID = 62519;
        internal const double TickTimeDiv = 0.0625;

        internal volatile bool Dispatched;

        internal readonly Guid LogicSettingsGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811501");
        internal readonly Guid LogicStateGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811502");
        internal readonly Guid LogicEnforceGuid = new Guid("75BBB4F5-4FB9-4230-BEEF-BB79C9811503");

        internal static WeaponEnforcement Enforced { get; set; } = new WeaponEnforcement();
        internal static bool EnforceInit { get; set; }

        internal uint SoundTick { get; set; }
        internal int PlayerEventId { get; set; }
        internal ulong AuthorSteamId { get; set; } = 76561197969691953;
        internal long AuthorPlayerId { get; set; }
        internal long LastTerminalId { get; set; }
        internal double SyncDistSqr { get; private set; }
        internal double SyncBufferedDistSqr { get; private set; }
        internal double SyncDist { get; private set; }
        internal bool MpActive { get; set; }
        internal bool IsServer { get; set; }
        internal bool DedicatedServer { get; set; }
        internal bool WepAction { get; set; }
        internal bool WepControl { get; set; }
        internal bool FirstLoop { get; set; }
        internal bool GameLoaded { get; set; }
        internal bool MiscLoaded { get; set; }
        internal bool Tick20 { get; set; }
        internal bool Tick60 { get; set; }
        internal bool Tick180 { get; set; }
        internal bool Tick300 { get; set; }
        internal bool Tick600 { get; set; }
        internal bool Tick1800 { get; set; }
        internal bool ShieldMod { get; set; }
        internal bool ShieldApiLoaded { get; set; }

        internal readonly MyStringId LaserMaterial = MyStringId.GetOrCompute("WeaponLaser");
        internal readonly MyStringId WarpMaterial = MyStringId.GetOrCompute("WarpBubble");

        internal readonly Guid LogictateGuid = new Guid("85BED4F5-4FB9-4230-FEED-BE79D9811500");
        internal readonly Guid LogicettingsGuid = new Guid("85BED4F5-4FB9-4230-FEED-BE79D9811501");

        internal List<WeaponHit> WeaponHits = new List<WeaponHit>();
        internal readonly List<Logic> Logic = new List<Logic>();
        internal readonly ConcurrentDictionary<long, IMyPlayer> Players = new ConcurrentDictionary<long, IMyPlayer>();
        internal readonly ConcurrentQueue<DrawProjectile> DrawBeams = new ConcurrentQueue<DrawProjectile>();

        public Dictionary<MyStringHash, WeaponStructure> WeaponStructure = new Dictionary<MyStringHash, WeaponStructure>(MyStringHash.Comparer);
        internal ShieldApi SApi = new ShieldApi();
        internal ConfigMe MyConfig = new ConfigMe();
        internal static WeaponEnforcement Enforcement { get; set; } = new WeaponEnforcement();
        internal FutureEvents FutureEvents { get; set; } = new FutureEvents();
        private DSUtils _dsUtil { get; set; } = new DSUtils();

        internal readonly HashSet<string> WepActions = new HashSet<string>()
        {
            "WC-L_PowerLevel",
            "WC-L_Guidance"
        };
    }
}