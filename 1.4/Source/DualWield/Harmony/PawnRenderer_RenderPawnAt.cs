using HarmonyLib;
using RimWorld;
using System;
using System.Linq;
using System.Text;
using Verse;

namespace DualWield.HarmonyInstance
{
    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
    class PawnRenderer_RenderPawnAt
    {
        static void Postfix(PawnRenderer __instance, ref Pawn ___pawn)
        {
            if (___pawn.Spawned && !___pawn.Dead)
            {
                ___pawn.GetStancesOffHand().StanceTrackerDraw();
            }
        }

    }
}
