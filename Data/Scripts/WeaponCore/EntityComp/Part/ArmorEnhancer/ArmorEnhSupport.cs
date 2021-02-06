﻿using VRage.Game.ModAPI;

namespace WeaponCore.Platform
{
    public partial class ArmorSupport : Part
    {
        internal void RefreshBlocks()
        {
            Session.GetCubesInRange(Comp.Cube.CubeGrid, Comp.Cube, 3, EnhancedArmorBlocks, out Min, out Max, Session.CubeTypes.Slims);
            
            LastBlockRefreshTick = System.Session.Tick;
        }

        internal void ToggleAreaEffectDisplay()
        {
            var grid = Comp.Cube.CubeGrid;
            if (!ShowAffectedBlocks) {

                ShowAffectedBlocks = true;
                foreach (var myCube in EnhancedArmorBlocks.Keys)
                    _blockColorBackup.Add(myCube, ((IMySlimBlock)myCube.CubeBlock).ColorMaskHSV);

                System.Session.DisplayAffectedArmor.Add(this);
            }
            else {

                foreach (var pair in _blockColorBackup)
                    grid.ChangeColorAndSkin(pair.Key.CubeBlock, pair.Value);

                _blockColorBackup.Clear();
                System.Session.DisplayAffectedArmor.Remove(this);
                ShowAffectedBlocks = false;
            }
        }
    }
}