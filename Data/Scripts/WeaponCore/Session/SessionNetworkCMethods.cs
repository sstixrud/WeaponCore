﻿using Sandbox.Game.Entities;
using WeaponCore.Control;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Session;
using static WeaponCore.Support.GridAi;
using static WeaponCore.Support.WeaponDefinition;

namespace WeaponCore
{
    public partial class Session
    {
        private bool CompStateUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            var statePacket = (StatePacket)packet;
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));
            if (statePacket.Data == null) return Error(data,  Msg("Data"));

            comp.MIds[(int)packet.PType] = statePacket.MId;
            comp.State.Value.Sync(statePacket.Data);
            data.Report.PacketValid = true;

            return true;
        }

        private bool CompSettingsUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            var setPacket = (SettingPacket)packet;
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));
            if (setPacket.Data == null) return Error(data, Msg("Data"));

            comp.MIds[(int)packet.PType] = setPacket.MId;
            comp.Set.Value.Sync(comp, setPacket.Data);

            data.Report.PacketValid = true;
            return true;
        }

        private bool WeaponSyncUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var targetPacket = (GridWeaponPacket)packet;
            if (targetPacket.Data == null) return Error(data, Msg("Data"));

            for (int j = 0; j < targetPacket.Data.Count; j++) {

                var weaponData = targetPacket.Data[j];
                var block = MyEntities.GetEntityByIdOrDefault(weaponData.CompEntityId) as MyCubeBlock;
                var comp = block?.Components.Get<WeaponComponent>();

                if (comp?.Ai == null) return Error(data, Msg("Comp", comp != null), Msg("Ai"));

                if (comp.Platform.State != MyWeaponPlatform.PlatformState.Ready)
                    continue;

                Weapon weapon;

                if (weaponData.Timmings != null && weaponData.SyncData != null && weaponData.WeaponRng != null) {
                    weapon = comp.Platform.Weapons[weaponData.SyncData.WeaponId];
                    var timings = weaponData.Timmings.SyncOffsetClient(Tick);
                    SyncWeapon(weapon, timings, ref weaponData.SyncData);

                    weapon.Comp.WeaponValues.WeaponRandom[weapon.WeaponId].Sync(weaponData.WeaponRng);
                }

                if (weaponData.TargetData != null) {

                    weapon = comp.Platform.Weapons[weaponData.TargetData.WeaponId];
                    weaponData.TargetData.SyncTarget(weapon.Target);

                    if (weapon.Target.HasTarget) {

                        if (!weapon.Target.IsProjectile && !weapon.Target.IsFakeTarget && weapon.Target.Entity == null) {
                            var oldChange = weapon.Target.TargetChanged;
                            weapon.Target.StateChange(true, Target.States.Invalid);
                            weapon.Target.TargetChanged = !weapon.FirstSync && oldChange;
                            weapon.FirstSync = false;
                        }
                        else if (weapon.Target.IsProjectile) {

                            TargetType targetType;
                            AcquireProjectile(weapon, out targetType);

                            if (targetType == TargetType.None) {
                                if (weapon.NewTarget.CurrentState != Target.States.NoTargetsSeen)
                                    weapon.NewTarget.Reset(weapon.Comp.Session.Tick, Target.States.NoTargetsSeen);
                                if (weapon.Target.CurrentState != Target.States.NoTargetsSeen) weapon.Target.Reset(weapon.Comp.Session.Tick, Target.States.NoTargetsSeen, !weapon.Comp.TrackReticle);
                            }
                        }
                    }
                }

                data.Report.PacketValid = true;
            }

            return true;
        }

        private bool FakeTargetUpdate(PacketObj data)
        {
            var packet = data.Packet;
            data.ErrorPacket.NoReprocess = true; 
            var targetPacket = (FakeTargetPacket)packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

            GridAi ai;
            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai)) {
                ai.DummyTarget.Update(targetPacket.Data, ai, null, true);
                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg("Grid", myGrid != null), Msg("Ai"));

            return true;
        }

        private bool PlayerIdUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var updatePacket = (BoolUpdatePacket)packet;

            if (updatePacket.Data)
                PlayerConnected(updatePacket.EntityId);
            else //remove
                PlayerDisconnected(updatePacket.EntityId);

            data.Report.PacketValid = true;
            return true;
        }

        private bool ClientMouseEvent(PacketObj data)
        {
            var packet = data.Packet;
            var mousePacket = (InputPacket)packet;
            if (mousePacket.Data == null) return Error(data, Msg("Data"));

            long playerId;
            if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId)) {
                PlayerMouseStates[playerId] = mousePacket.Data;

                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg("No Player Mouse State Found"));


            return true;
        }

        private bool ActiveControlUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var dPacket = (BoolUpdatePacket)packet;
            var cube = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeBlock;
            if (cube == null) return Error(data, Msg("Cube"));

            long playerId;
            SteamToPlayer.TryGetValue(packet.SenderId, out playerId);

            UpdateActiveControlDictionary(cube, playerId, dPacket.Data);

            data.Report.PacketValid = true;

            return true;
        }

        private bool ActiveControlFullUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var csPacket = (CurrentGridPlayersPacket)packet;

            for (int i = 0; i < csPacket.Data.PlayersToControlledBlock.Length; i++) {
                var playerBlock = csPacket.Data.PlayersToControlledBlock[i];

                var cube = MyEntities.GetEntityByIdOrDefault(playerBlock.EntityId) as MyCubeBlock;
                if (cube?.CubeGrid == null) return Error(data, Msg("Cube", cube != null), Msg("Grid"));

                UpdateActiveControlDictionary(cube, playerBlock.PlayerId, true);
            }

            data.Report.PacketValid = true;

            return true;

        }

        private bool ReticleUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var reticlePacket = (BoolUpdatePacket)packet;

            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));


            comp.State.Value.OtherPlayerTrackingReticle = reticlePacket.Data;

            data.Report.PacketValid = true;
            return true;

        }

        private bool OverRidesUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var overRidesPacket = (OverRidesPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            var myGrid = ent as MyCubeGrid;

            if (comp?.Ai == null && myGrid == null) return Error(data, Msg("Comp", comp != null), Msg("Ai+Grid"));

            if (overRidesPacket.Data == null) return Error(data, Msg("Data"));

            if (comp?.Ai != null && comp.MIds[(int)packet.PType] < overRidesPacket.MId) {

                comp.Set.Value.Overrides.Sync(overRidesPacket.Data);
                comp.MIds[(int)packet.PType] = overRidesPacket.MId;

                GroupInfo group;
                if (!string.IsNullOrEmpty(comp.State.Value.CurrentBlockGroup) && comp.Ai.BlockGroups.TryGetValue(comp.State.Value.CurrentBlockGroup, out group)) {
                    comp.Ai.ScanBlockGroupSettings = true;
                    comp.Ai.GroupsToCheck.Add(group);
                }
                data.Report.PacketValid = true;
            }
            else if (myGrid != null)
            {
                GridAi ai;
                if (GridTargetingAIs.TryGetValue(myGrid, out ai) && ai.UiMId < overRidesPacket.MId) {
                    var o = overRidesPacket.Data;
                    ai.UiMId = overRidesPacket.MId;

                    ai.ReScanBlockGroups();

                    SyncGridOverrides(ai, overRidesPacket.GroupName, o);

                    GroupInfo groups;
                    if (ai.BlockGroups.TryGetValue(overRidesPacket.GroupName, out groups)) {

                        foreach (var component in groups.Comps) {
                            component.State.Value.CurrentBlockGroup = overRidesPacket.GroupName;
                            component.Set.Value.Overrides.Sync(o);
                        }

                        data.Report.PacketValid = true;
                    }
                    else
                        return Error(data, Msg("Block group not found"));
                }
                else
                    return Error(data, Msg("GridAi not found"));
            }

            return true;
        }

        private bool PlayerControlUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var cPlayerPacket = (ControllingPlayerPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            comp.State.Value.CurrentPlayerControl.Sync(cPlayerPacket.Data);
            comp.MIds[(int)packet.PType] = cPlayerPacket.MId;
            data.Report.PacketValid = true;

            return true;
        }

        private bool TargetExpireUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var idPacket = (WeaponIdPacket)packet;
            data.ErrorPacket.NoReprocess = true;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            //saving on extra field with new packet type
            comp.Platform.Weapons[idPacket.WeaponId].Target.Reset(Tick, Target.States.ServerReset);

            data.Report.PacketValid = true;

            return true;

        }

        private bool FullMouseUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var mouseUpdatePacket = (MouseInputSyncPacket)packet;

            if (mouseUpdatePacket.Data == null) return Error(data, Msg("Data"));

            for (int i = 0; i < mouseUpdatePacket.Data.Length; i++) {
                var playerMousePackets = mouseUpdatePacket.Data[i];
                if (playerMousePackets.PlayerId != PlayerId)
                    PlayerMouseStates[playerMousePackets.PlayerId] = playerMousePackets.MouseStateData;
            }

            data.Report.PacketValid = true;
            return true;

        }

        private bool CompToolbarShootState(PacketObj data)
        {
            var packet = data.Packet;
            var shootStatePacket = (ShootStatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();

            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            comp.MIds[(int)packet.PType] = shootStatePacket.MId;

            switch (shootStatePacket.Data)
            {
                case ManualShootActionState.ShootClick:
                    TerminalHelpers.WcShootClickAction(comp, true, comp.HasTurret, true);
                    break;
                case ManualShootActionState.ShootOff:
                    TerminalHelpers.WcShootOffAction(comp, true);
                    break;
                case ManualShootActionState.ShootOn:
                    TerminalHelpers.WcShootOnAction(comp, true);
                    break;
                case ManualShootActionState.ShootOnce:
                    TerminalHelpers.WcShootOnceAction(comp, true);
                    break;
            }

            data.Report.PacketValid = true;
            return true;
        }

        private bool RangeUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var rangePacket = (RangePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            comp.MIds[(int)packet.PType] = rangePacket.MId;
            comp.Set.Value.Range = rangePacket.Data;

            data.Report.PacketValid = true;
            return true;
        }

        private bool GridAiUiMidUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            var midPacket = (MIdPacket)packet;

            if (myGrid == null) return Error(data, Msg("Grid"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {
                ai.UiMId = midPacket.MId;
                data.Report.PacketValid = true;
            }
            return true;
        }

        private bool CycleAmmo(PacketObj data)
        {
            var packet = data.Packet;
            var cyclePacket = (CycleAmmoPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            comp.MIds[(int)packet.PType] = cyclePacket.MId;
            var weapon = comp.Platform.Weapons[cyclePacket.WeaponId];
            weapon.Set.AmmoTypeId = cyclePacket.AmmoId;

            if (weapon.State.Sync.CurrentAmmo == 0)
                weapon.StartReload();

            return true;

        }

        private bool GridOverRidesSync(PacketObj data)
        {
            var packet = data.Packet;
            var gridOverRidePacket = (GridOverRidesSyncPacket)packet;

            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            if (myGrid == null) return Error(data, Msg("Grid"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
            {
                ai.ReScanBlockGroups();

                for (int i = 0; i < gridOverRidePacket.Data.Length; i++) {

                    var groupName = gridOverRidePacket.Data[i].GroupName;
                    var overRides = gridOverRidePacket.Data[i].Overrides;

                    if (ai.BlockGroups.ContainsKey(groupName)) {
                        SyncGridOverrides(ai, groupName, overRides);
                        data.Report.PacketValid = true;
                    }
                    else
                        return Error(data, Msg("group did not exist"));
                }
            }
            return true;

        }

        private bool RescanGroupRequest(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            if (myGrid == null) return Error(data, Msg("Grid"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
                ai.ReScanBlockGroups(true);

            data.Report.PacketValid = true;
            return true;

        }

        private bool GridFocusListSync(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            var focusPacket = (GridFocusListPacket)packet;
            if (myGrid == null) return Error(data, Msg("Grid"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                for (int i = 0; i < focusPacket.EntityIds.Length; i++) {

                    var focusTarget = MyEntities.GetEntityByIdOrDefault(focusPacket.EntityIds[i]);
                    if (focusTarget == null) return Error(data, Msg("focusTarget"));

                    ai.Focus.Target[i] = focusTarget;
                }
                data.Report.PacketValid = true;
            }

            return true;
        }

        private bool ClientMidUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var midPacket = (ClientMIdUpdatePacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            var myGrid = ent as MyCubeGrid;
            if (comp?.Ai == null && myGrid == null) return Error(data, Msg("Comp", comp != null), Msg("Ai+Grid"));

            if (comp != null) {
                comp.MIds[(int)midPacket.MidType] = midPacket.MId;
                if (comp.GetSyncHash() != midPacket.HashCheck)
                    RequestCompSync(comp);

                data.Report.PacketValid = true;
            }
            else  {
                GridAi ai;
                if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {
                    ai.UiMId = midPacket.MId;
                    data.Report.PacketValid = true;
                }
                else
                    return Error(data, Msg("GridAi not found"));
            }

            return true;

        }
        private bool FocusStates(PacketObj data)
        {
            var packet = data.Packet;
            var focusPacket = (FocusPacket)packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            if (myGrid == null) return Error(data, Msg("Grid"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                var targetGrid = MyEntities.GetEntityByIdOrDefault(focusPacket.TargetId) as MyCubeGrid;
                switch (packet.PType) {

                    case PacketType.FocusUpdate:
                        if (targetGrid != null)
                            ai.Focus.AddFocus(targetGrid, ai, true);
                        break;
                    case PacketType.ReassignTargetUpdate:
                        if (targetGrid != null)
                            ai.Focus.ReassignTarget(targetGrid, focusPacket.FocusId, ai, true);
                        break;
                    case PacketType.NextActiveUpdate:
                        ai.Focus.NextActive(focusPacket.AddSecondary, ai, true);
                        break;
                    case PacketType.ReleaseActiveUpdate:
                        ai.Focus.ReleaseActive(ai, true);
                        break;
                }
                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg("GridAi not found"));

            return true;

        }
    }
}