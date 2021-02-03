﻿using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;
using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Utils;
using VRageRender;
using static WeaponCore.Support.PartDefinition.AnimationDef.PartAnimationSetDef;
using static WeaponCore.Support.CoreComponent;
namespace WeaponCore.Platform
{
    public partial class Part
    {
        internal void Trigger() // Inlined due to keens mod profiler
        {
            try
            {
                var s = Comp.Session;
                var tick = s.Tick;
                #region Prefire
                if (_ticksUntilShoot++ < System.DelayToFire) {

                    if (AvCapable && System.PreFireSound && !PreFiringEmitter.IsPlaying)
                        StartPreFiringSound();

                    if (ActiveAmmoDef.AmmoDef.Const.MustCharge || System.AlwaysFireFullBurst)
                        FinishBurst = true;

                    if (!PreFired)
                        SetPreFire();
                    return;
                } 

                if (PreFired)
                    UnSetPreFire();
                #endregion

                #region Weapon timing
                if (System.HasBarrelRotation && !SpinBarrel() || ShootTick > tick)
                    return;

                if (LockOnFireState && (Target.TargetEntity?.EntityId != Comp.Ai.Construct.Data.Repo.FocusData.Target[0] || Target.TargetEntity?.EntityId != Comp.Ai.Construct.Data.Repo.FocusData.Target[1])) {
                    
                    MyEntity focusTarget;
                    int focusId;
                    if (!Comp.Ai.Construct.Focus.GetPriorityTarget(Comp.Ai, out focusTarget, out focusId) || Comp.Ai.Construct.Data.Repo.FocusData.Locked[focusId] == FocusData.LockModes.None)
                        return;
                    
                    Target.LockTarget(this, focusTarget);
                }

                ShootTick = tick + TicksPerShot;
                LastShootTick = tick;

                if (!IsTriggering) StartShooting();

                var burstDelay = (uint)System.Values.HardPoint.Loading.DelayAfterBurst;

                if (ActiveAmmoDef.AmmoDef.Const.BurstMode && ++ShotsFired > System.ShotsPerBurst) {
                    ShotsFired = 1;
                    EventTriggerStateChanged(EventTriggers.BurstReload, false);
                }
                else if (ActiveAmmoDef.AmmoDef.Const.HasShotReloadDelay && System.ShotsPerBurst > 0 && ++ShotsFired == System.ShotsPerBurst) {
                    ShotsFired = 0;
                    ShootTick = burstDelay > TicksPerShot ? tick + burstDelay : tick + TicksPerShot;
                }

                if (Comp.Ai.VelocityUpdateTick != tick) {
                    Comp.Ai.GridVel = Comp.Ai.TopEntity.Physics?.LinearVelocity ?? Vector3D.Zero;
                    Comp.Ai.IsStatic = Comp.Ai.TopEntity.Physics?.IsStatic ?? false;
                    Comp.Ai.VelocityUpdateTick = tick;
                }

                #endregion

                #region Projectile Creation
                var rnd = Comp.Data.Repo.Base.Targets[WeaponId].WeaponRandom;
                var pattern = ActiveAmmoDef.AmmoDef.Pattern;

                FireCounter++;
                List<NewVirtual> vProList = null;
                var selfDamage = 0f;
                for (int i = 0; i < System.Values.HardPoint.Loading.BarrelsPerShot; i++) {

                    #region Update Ammo state
                    var skipMuzzle = s.IsClient && Ammo.CurrentAmmo == 0 && ClientMakeUpShots == 0 && ShootOnce;
                    if (ActiveAmmoDef.AmmoDef.Const.Reloadable) {

                        if (Ammo.CurrentAmmo == 0) {

                            if (ShootOnce) 
                                ShootOnce = false;

                            if (ClientMakeUpShots == 0) {
                                if (s.MpActive && s.IsServer)
                                    s.SendWeaponReload(this);
                                if (!skipMuzzle) break;
                            }
                        }
                        
                        if (Ammo.CurrentAmmo > 0) {
                            --Ammo.CurrentAmmo;
                            if (ShootOnce)
                                DequeueShot();
                        }
                        else if (ClientMakeUpShots > 0)
                            --ClientMakeUpShots;
                        if (System.HasEjector && ActiveAmmoDef.AmmoDef.Const.HasEjectEffect)  {
                            if (ActiveAmmoDef.AmmoDef.Ejection.SpawnChance >= 1 || rnd.TurretRandom.Next(0, 1) >= ActiveAmmoDef.AmmoDef.Ejection.SpawnChance)
                            {
                                SpawnEjection();
                            }
                        }
                    }
                    #endregion

                    #region Next muzzle
                    var current = !skipMuzzle ? NextMuzzle : LastMuzzle;
                    var muzzle = Muzzles[current];
                    if (muzzle.LastUpdateTick != tick) {
                        var dummy = Dummies[current];
                        var newInfo = dummy.Info;
                        muzzle.Direction = newInfo.Direction;
                        muzzle.Position = newInfo.Position;
                        muzzle.LastUpdateTick = tick;
                    }
                    #endregion

                    if (ActiveAmmoDef.AmmoDef.Const.HasBackKickForce && !Comp.Ai.IsStatic && s.IsServer)
                        Comp.Ai.TopEntity.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, -muzzle.Direction * ActiveAmmoDef.AmmoDef.BackKickForce, muzzle.Position, Vector3D.Zero);

                    if (PlayTurretAv) {
                        if (System.BarrelEffect1 && muzzle.LastAv1Tick == 0 && !muzzle.Av1Looping) {

                            muzzle.LastAv1Tick = tick;
                            var avBarrel = s.Av.AvBarrelPool.Get();
                            avBarrel.Part = this;
                            avBarrel.Muzzle = muzzle;
                            avBarrel.StartTick = tick;
                            s.Av.AvBarrels1.Add(avBarrel);
                        }
                        if (System.BarrelEffect2 && muzzle.LastAv2Tick == 0 && !muzzle.Av2Looping) {

                            muzzle.LastAv2Tick = tick;
                            var avBarrel = s.Av.AvBarrelPool.Get();
                            avBarrel.Part = this;
                            avBarrel.Muzzle = muzzle;
                            avBarrel.StartTick = tick;
                            s.Av.AvBarrels2.Add(avBarrel);
                        }
                    }

                    for (int j = 0; j < System.Values.HardPoint.Loading.TrajectilesPerBarrel; j++) {

                        #region Pick projectile direction
                        if (System.Values.HardPoint.DeviateShotAngle > 0) {
                            var dirMatrix = Matrix.CreateFromDir(muzzle.Direction);
                            var rnd1 = rnd.TurretRandom.NextDouble();
                            var rnd2 = rnd.TurretRandom.NextDouble();
                            var randomFloat1 = (float)(rnd1 * (System.Values.HardPoint.DeviateShotAngle + System.Values.HardPoint.DeviateShotAngle) - System.Values.HardPoint.DeviateShotAngle);
                            var randomFloat2 = (float)(rnd2 * MathHelper.TwoPi);
                            rnd.TurretCurrentCounter += 2;
                            muzzle.DeviatedDir = Vector3.TransformNormal(-new Vector3D(MyMath.FastSin(randomFloat1) * MyMath.FastCos(randomFloat2), MyMath.FastSin(randomFloat1) * MyMath.FastSin(randomFloat2), MyMath.FastCos(randomFloat1)), dirMatrix);
                        }
                        else muzzle.DeviatedDir = muzzle.Direction;
                        #endregion

                        #region Pick Ammo Pattern
                        var patternIndex = ActiveAmmoDef.AmmoDef.Const.PatternIndexCnt;

                        if (pattern.Enable) {

                            if (pattern.Random) {

                                if (pattern.TriggerChance >= rnd.TurretRandom.NextDouble() || pattern.TriggerChance >= 1) {
                                    patternIndex = rnd.TurretRandom.Next(pattern.RandomMin, pattern.RandomMax);
                                    rnd.TurretCurrentCounter += 2;
                                }
                                else
                                    rnd.TurretCurrentCounter++;

                                for (int w = 0; w < ActiveAmmoDef.AmmoDef.Const.PatternIndexCnt; w++) {
                                    var y = rnd.TurretRandom.Next(w + 1);
                                    AmmoShufflePattern[w] = AmmoShufflePattern[y];
                                    AmmoShufflePattern[y] = w;
                                }
                            }
                            else if (pattern.PatternSteps > 0 && pattern.PatternSteps <= ActiveAmmoDef.AmmoDef.Const.PatternIndexCnt) {

                                patternIndex = pattern.PatternSteps;
                                for (int p = 0; p < ActiveAmmoDef.AmmoDef.Const.PatternIndexCnt; ++p)
                                    AmmoShufflePattern[p] = (AmmoShufflePattern[p] + patternIndex) % ActiveAmmoDef.AmmoDef.Const.PatternIndexCnt;
                            }
                        }
                        #endregion

                        #region Generate Projectiles
                        for (int k = 0; k < patternIndex; k++) {

                            var ammoPattern = ActiveAmmoDef.AmmoDef.Const.AmmoPattern[AmmoShufflePattern[k]];

                            selfDamage += ammoPattern.DecayPerShot;

                            long patternCycle = FireCounter;
                            if (ammoPattern.AmmoGraphics.Lines.Tracer.VisualFadeStart > 0 && ammoPattern.AmmoGraphics.Lines.Tracer.VisualFadeEnd > 0)
                                patternCycle = ((FireCounter - 1) % ammoPattern.AmmoGraphics.Lines.Tracer.VisualFadeEnd) + 1;

                            if (ammoPattern.Const.VirtualBeams && j == 0) {

                                if (i == 0) {
                                    vProList = s.Projectiles.VirtInfoPools.Get();
                                    s.Projectiles.NewProjectiles.Add(new NewProjectile { NewVirts = vProList, AmmoDef = ammoPattern, Muzzle = muzzle, PatternCycle = patternCycle, Direction = muzzle.DeviatedDir, Type = NewProjectile.Kind.Virtual });
                                }

                                MyEntity primeE = null;
                                MyEntity triggerE = null;

                                if (ammoPattern.Const.PrimeModel)
                                    primeE = ammoPattern.Const.PrimeEntityPool.Get();

                                if (ammoPattern.Const.TriggerModel)
                                    triggerE = s.TriggerEntityPool.Get();

                                float shotFade;
                                if (ammoPattern.Const.HasShotFade) {
                                    if (patternCycle > ammoPattern.AmmoGraphics.Lines.Tracer.VisualFadeStart)
                                        shotFade = MathHelper.Clamp(((patternCycle - ammoPattern.AmmoGraphics.Lines.Tracer.VisualFadeStart)) * ammoPattern.Const.ShotFadeStep, 0, 1);
                                    else if (System.DelayCeaseFire && CeaseFireDelayTick != tick)
                                        shotFade = MathHelper.Clamp(((tick - CeaseFireDelayTick) - ammoPattern.AmmoGraphics.Lines.Tracer.VisualFadeStart) * ammoPattern.Const.ShotFadeStep, 0, 1);
                                    else shotFade = 0;
                                }
                                else shotFade = 0;

                                var maxTrajectory = ammoPattern.Const.MaxTrajectoryGrows && FireCounter < ammoPattern.Trajectory.MaxTrajectoryTime ? ammoPattern.Const.TrajectoryStep * FireCounter : ammoPattern.Const.MaxTrajectory;
                                var info = s.Projectiles.VirtInfoPool.Get();
                                
                                info.AvShot = s.Av.AvShotPool.Get();
                                info.InitVirtual(this, ammoPattern, primeE, triggerE, muzzle, maxTrajectory, shotFade);
                                vProList.Add(new NewVirtual { Info = info, Rotate = !ammoPattern.Const.RotateRealBeam && i == _nextVirtual, Muzzle = muzzle, VirtualId = _nextVirtual });
                            }
                            else
                                s.Projectiles.NewProjectiles.Add(new NewProjectile {AmmoDef = ammoPattern, Muzzle = muzzle, PatternCycle = patternCycle, Direction = muzzle.DeviatedDir, Type = NewProjectile.Kind.Normal});
                        }
                        #endregion
                    }
                    _muzzlesToFire.Add(MuzzleIdToName[current]);

                    if (HeatPShot > 0) {

                        if (!HeatLoopRunning) {
                            s.FutureEvents.Schedule(UpdateWeaponHeat, null, 20);
                            HeatLoopRunning = true;
                        }

                        State.Heat += HeatPShot;
                        Comp.CurrentHeat += HeatPShot;
                        if (State.Heat >= System.MaxHeat) {
                            OverHeat();
                            break;
                        }
                    }
                    
                    LastMuzzle = NextMuzzle;
                    if (i == System.Values.HardPoint.Loading.BarrelsPerShot) NextMuzzle++;

                    NextMuzzle = (NextMuzzle + (System.Values.HardPoint.Loading.SkipBarrels + 1)) % _numOfBarrels;
                }
                #endregion

                #region Reload and Animation
                if (IsTriggering)
                    EventTriggerStateChanged(state: EventTriggers.Firing, active: true, muzzles: _muzzlesToFire);

                if (ActiveAmmoDef.AmmoDef.Const.BurstMode && (s.IsServer && !ComputeServerStorage() || s.IsClient && !ClientReload()))
                    BurstMode();

                _muzzlesToFire.Clear();

                if (s.IsServer && selfDamage > 0)
                {
                    if (Comp.IsBlock)
                        ((IMyDestroyableObject)Comp.Cube.SlimBlock).DoDamage(selfDamage, MyDamageType.Grind, true, null, Comp.CoreEntity.EntityId);
                    else
                        ((IMyDestroyableObject)Comp.TopEntity as IMyCharacter).DoDamage(selfDamage, MyDamageType.Grind, true, null, Comp.CoreEntity.EntityId);
                }

                #endregion
                _nextVirtual = _nextVirtual + 1 < System.Values.HardPoint.Loading.BarrelsPerShot ? _nextVirtual + 1 : 0;
            }
            catch (Exception e) { Log.Line($"Error in shoot: {e}"); }
        }

        private void OverHeat()
        {
            if (!System.Session.IsClient && Comp.Data.Repo.Base.Set.Overload > 1) {
                var dmg = .02f * Comp.MaxIntegrity;
                Comp.Slim.DoDamage(dmg, MyDamageType.Environment, true, null, Comp.Ai.TopEntity.EntityId);
            }
            EventTriggerStateChanged(EventTriggers.Overheated, true);

            var wasOver = State.Overheated;
            State.Overheated = true;
            if (System.Session.MpActive && System.Session.IsServer && !wasOver)
                System.Session.SendCompState(Comp);
        }

        private void BurstMode()
        {
            if (ShotsFired == System.ShotsPerBurst) {

                uint delay = 0;
                FinishBurst = false;
                var burstDelay = (uint)System.Values.HardPoint.Loading.DelayAfterBurst;
                if (System.WeaponAnimationLengths.TryGetValue(EventTriggers.Firing, out delay)) {

                    System.Session.FutureEvents.Schedule(o => {
                        EventTriggerStateChanged(EventTriggers.BurstReload, true);
                        ShootTick = burstDelay > TicksPerShot ? System.Session.Tick + burstDelay + delay : System.Session.Tick + TicksPerShot + delay;
                        StopShooting();

                    }, null, delay);
                }
                else
                    EventTriggerStateChanged(EventTriggers.BurstReload, true);

                if (IsTriggering) {
                    ShootTick = burstDelay > TicksPerShot ? System.Session.Tick + burstDelay + delay : System.Session.Tick + TicksPerShot + delay;
                    StopShooting();
                }

                if (System.Values.HardPoint.Loading.GiveUpAfterBurst)
                {
                    Target.Reset(System.Session.Tick, Target.States.FiredBurst);
                    FastTargetResetTick = System.Session.Tick + 1;
                }
            }
            else if (System.AlwaysFireFullBurst && ShotsFired < System.ShotsPerBurst)
                FinishBurst = true;
        }

        private void UnSetPreFire()
        {
            EventTriggerStateChanged(EventTriggers.PreFire, false);
            _muzzlesToFire.Clear();
            PreFired = false;

            if (AvCapable && System.PreFireSound && PreFiringEmitter.IsPlaying)
                StopPreTriggerSound();
        }

        private void SetPreFire()
        {
            var nxtMuzzle = NextMuzzle;
            for (int i = 0; i < System.Values.HardPoint.Loading.BarrelsPerShot; i++)
            {
                _muzzlesToFire.Clear();
                _muzzlesToFire.Add(MuzzleIdToName[NextMuzzle]);
                if (i == System.Values.HardPoint.Loading.BarrelsPerShot) NextMuzzle++;
                nxtMuzzle = (nxtMuzzle + (System.Values.HardPoint.Loading.SkipBarrels + 1)) % _numOfBarrels;
            }

            EventTriggerStateChanged(EventTriggers.PreFire, true, _muzzlesToFire);

            PreFired = true;
        }

        private void DequeueShot()
        {
            ShootOnce = false;
            if (System.Session.IsServer)
            {
                if (System.Session.MpActive) System.Session.SendQueuedShot(this);
                if (State.Action == TriggerActions.TriggerOnce && ShotQueueEmpty())
                    Comp.Data.Repo.Base.State.TerminalActionSetter(Comp, TriggerActions.TriggerOff, true, false);
            }
        }

        private bool ShotQueueEmpty()
        {
            State.Action = TriggerActions.TriggerOff;
            var hasShot = false;
            for (int i = 0; i < Comp.Platform.Weapons.Length; i++) {
                if (Comp.Platform.Weapons[i].ShootOnce)
                    hasShot = true;
            }
            return !hasShot;
        }

        private void SpawnEjection()
        {
            var eInfo = Ejector.Info;
            var ejectDef = ActiveAmmoDef.AmmoDef.Ejection;
            if (ejectDef.Type == PartDefinition.AmmoDef.EjectionDef.SpawnType.Item)
            {
                var delay = (uint)ejectDef.CompDef.Delay;
                if (delay <= 0)
                    MyFloatingObjects.Spawn(ActiveAmmoDef.AmmoDef.Const.EjectItem, eInfo.Position, eInfo.Direction, MyPivotUp, null, EjectionSpawnCallback);
                else 
                    System.Session.FutureEvents.Schedule(EjectionDelayed, null, delay);
            }
            else if (System.Session.HandlesInput) {
                
                var particle = ActiveAmmoDef.AmmoDef.AmmoGraphics.Particles.Eject;
                MyParticleEffect ejectEffect;
                var matrix = MatrixD.CreateTranslation(eInfo.Position);
                
                if (MyParticlesManager.TryCreateParticleEffect(particle.Name, ref matrix, ref eInfo.Position, uint.MaxValue, out ejectEffect)) {
                    ejectEffect.UserColorMultiplier = particle.Color;
                    var scaler = 1;
                    ejectEffect.UserRadiusMultiplier = particle.Extras.Scale * scaler;
                    var scale = particle.ShrinkByDistance ? MathHelper.Clamp(MathHelper.Lerp(1, 0, Vector3D.Distance(System.Session.CameraPos, eInfo.Position) / particle.Extras.MaxDistance), 0.05f, 1) : 1;
                    ejectEffect.UserScale = (float)scale * scaler;
                    ejectEffect.Velocity = eInfo.Direction * ActiveAmmoDef.AmmoDef.Ejection.Speed;
                }
            }
        }

        private void EjectionDelayed(object o)
        {
            if (ActiveAmmoDef?.AmmoDef != null && !Ejector.NullEntity) 
                MyFloatingObjects.Spawn(ActiveAmmoDef.AmmoDef.Const.EjectItem, Ejector.Info.Position, Ejector.Info.Direction, MyPivotUp, null, EjectionSpawnCallback);
        }

        private void EjectionSpawnCallback(MyEntity entity)
        {
            if (ActiveAmmoDef?.AmmoDef != null) {
                
                var ejectDef = ActiveAmmoDef.AmmoDef.Ejection;
                var itemTtl = ejectDef.CompDef.ItemLifeTime;

                if (ejectDef.Speed > 0) 
                    SetSpeed(entity);

                if (itemTtl > 0)
                    System.Session.FutureEvents.Schedule(RemoveEjection, entity, (uint)itemTtl);
            }
        }

        private void SetSpeed(object o)
        {
            var entity = (MyEntity)o;

            if (entity?.Physics != null && ActiveAmmoDef?.AmmoDef != null && !entity.MarkedForClose) {
                
                var ejectDef = ActiveAmmoDef.AmmoDef.Ejection;
                entity.Physics.SetSpeeds(Ejector.CachedDir * (ejectDef.Speed), Vector3.Zero);
            }
        }

        private static void RemoveEjection(object o)
        {
            var entity = (MyEntity) o;
            
            if (entity?.Physics != null) {
                using (entity.Pin())  {
                    if (!entity.MarkedForClose && !entity.Closed)
                        entity.Close();
                }
            }
        }
    }
}