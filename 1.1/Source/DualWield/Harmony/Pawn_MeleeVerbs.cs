using DualWield.Stances;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace DualWield.Harmony
{
    [HarmonyPatch(typeof(Pawn_MeleeVerbs), "GetUpdatedAvailableVerbsList")]
    class Pawn_MeleeVerbs_GetUpdatedAvailableVerbsList
    {
        static void Postfix(ref List<VerbEntry> __result)
        {
            //remove all offhand verbs so they're not used by for mainhand melee attacks.
            List<VerbEntry> shouldRemove = new List<VerbEntry>();
            foreach (VerbEntry ve in __result)
            {
                if (ve.verb.EquipmentSource != null && ve.verb.EquipmentSource.IsOffHand())
                {
                    shouldRemove.Add(ve);
                }
            }
            foreach (VerbEntry ve in shouldRemove)
            {
                __result.Remove(ve);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_MeleeVerbs),"TryMeleeAttack")]
    class Pawn_MeleeVerbs_TryMeleeAttack
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = new List<CodeInstruction>(instructions);
            IEnumerable<CodeInstruction> newInstructions =
            instructions.MethodReplacer(typeof(Pawn_StanceTracker).GetMethod("get_FullBodyBusy"), typeof(Pawn_MeleeVerbs_TryMeleeAttack).GetMethod("FullBodyAndOffHandBusy"));
            return newInstructions;
            /*
            foreach (CodeInstruction instruction in instructionsList)
            {
                if(instruction.OperandIs(typeof(Pawn_StanceTracker).GetMethod("get_FullBodyBusy")))
                {
                    yield return new CodeInstruction(OpCodes.Call, typeof(Jobdriver_Wait_CheckForAutoAttack).GetMethod("FullBodyAndOffHandBusy"));
                }
                else
                {
                    yield return instruction;
                }
            }
            */
        }
        public static bool FullBodyAndOffHandBusy(Pawn_StanceTracker instance)
        {
            bool busy = instance.stunner.Stunned || instance.curStance.StanceBusy;
            if (Prefs.DevMode && instance.pawn == Find.Selector.SingleSelectedThing && KeyBindingDefOf.ModifierIncrement_100x.IsDown)
            {
                Log.Warning(instance.pawn + " TryMeleeAttack FullBodyAndOffHandBusy: " + busy + " Mainhand Busy: " + busy);
            }
            return busy;
        }

        static void Postfix(Pawn_MeleeVerbs __instance, Thing target, Verb verbToUse, bool surpriseAttack, ref bool __result, ref Pawn ___pawn)
        {
            if (___pawn.GetStancesOffHand() == null || ___pawn.GetStancesOffHand().curStance is Stance_Warmup_DW || ___pawn.GetStancesOffHand().curStance is Stance_Cooldown)
            {
                return;
            }
            else if (Prefs.DevMode && __instance.pawn == Find.Selector.SingleSelectedThing && ___pawn.GetStancesOffHand() != null) Log.Message("curOffHandStance:: " + ___pawn.GetStancesOffHand().curStance.GetType().Name);
            if (___pawn.equipment == null || !___pawn.equipment.TryGetOffHandEquipment(out ThingWithComps offHandEquip))
            {
                return;
            }
            if(offHandEquip == ___pawn.equipment.Primary)
            {
                return;
            }
            if (___pawn.InMentalState)
            {
                return;
            }
            bool sucess = TryOffhandAttack(__instance, target);
            if (!sucess && Prefs.DevMode && __instance.pawn == Find.Selector.SingleSelectedThing) Log.Warning("OffhandAttack Failed");
            __result = __result || sucess;


        }
        public static bool TryOffhandAttack(Pawn_MeleeVerbs __instance, Thing target)
        {
            Verb verb = __instance.Pawn.TryGetMeleeVerbOffHand(target);
            if (verb != null)
            {
                return verb.OffhandTryStartCastOn(target);
            }
            return false;
        }
    }
}
