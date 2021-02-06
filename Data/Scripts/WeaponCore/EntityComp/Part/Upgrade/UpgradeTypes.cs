﻿using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Upgrade : Part
    {
        internal class UpgradeComponent : CoreComponent
        {

            internal UpgradeComponent(Session session, MyEntity coreEntity, MyDefinitionId id)
            {
                base.Init(session, coreEntity, true, ((MyCubeBlock)coreEntity).CubeGrid, id);
            }
        }
    }
}
