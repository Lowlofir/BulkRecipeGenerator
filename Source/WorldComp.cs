using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace Ad2mod
{
    class Ad2WorldComp : WorldComponent
    {
        public static Ad2WorldComp instance;

        public int threshold;

        public Ad2WorldComp(World world) : base(world)
        {
            instance = this;
            threshold = Ad2Mod.settings.defaultThreshold;
            Log.Message("WorldComp.ctr():  " + world.info.name + "  " + world.info.seedString);
        }


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref threshold, "threshold");
            Log.Message("WorldComp.ExposeData()  threshold = " + threshold);
        }
    }
}
