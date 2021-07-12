using DualWield.Stances;
using DualWield.Storage;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Verse;
using Verse.AI;

namespace DualWield.Harmony
{
    [HarmonyPatch(typeof(Verb),"TryStartCastOn", new Type[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool) })]
    public class Verb_TryStartCastOn {
        static void Postfix(Verb __instance, LocalTargetInfo castTarg, ref bool __result)
        {
            if(__instance.caster is Pawn casterPawn)
            {
                //Check if it's an enemy that's attacked, and not a fire or an arguing husband
                if ((!casterPawn.InMentalState && !(castTarg.Thing is Fire)))
                {
                    //Check that the offhand isnt busy
                    if (!casterPawn.GetStancesOffHand().curStance.StanceBusy)
                    {
                        casterPawn.TryStartOffHandAttack(castTarg, ref __result);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Verb), "TryCastNextBurstShot")]
    public class Verb_TryCastNextBurstShot
    {
        [HarmonyPriority(Priority.Low)]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = new List<CodeInstruction>(instructions);
            MethodInfo setStance = typeof(Pawn_StanceTracker).GetMethod("SetStance");
            MethodInfo nofityAttack = typeof(Pawn_MindState).GetMethod("Notify_AttackedTarget", BindingFlags.NonPublic | BindingFlags.Instance);
            IEnumerable<CodeInstruction> newInstructions =
            instructions.MethodReplacer(setStance, typeof(Verb_TryCastNextBurstShot).GetMethod("SetStanceOffHand"));
            foreach (var item in newInstructions)
            {
                if (item.OperandIs(nofityAttack))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, typeof(Verb_TryCastNextBurstShot).GetMethod("Notify_AttackedTarget"));
                }
                else yield return item;
            }
        }
        public static void SetStanceOffHand(Pawn_StanceTracker stanceTracker,  Stance_Cooldown stance)
        {
            ThingWithComps offHandEquip = null;
            CompEquippable compEquippable = null;
            Pawn pawn = stanceTracker.pawn;


            if (stance.verb.EquipmentSource != null)
            {
                if (Base.Instance.GetExtendedDataStorage().TryGetExtendedDataFor(stance.verb.EquipmentSource, out ExtendedThingWithCompsData twcdata) && twcdata.isOffHand)
                {
                    if (Prefs.DevMode && pawn == Find.Selector.SingleSelectedThing) Log.Message("offhand attack with " + stance.verb.EquipmentSource.def.LabelCap+"'s "+ stance.verb);
                    offHandEquip = stance.verb.EquipmentSource;
                    compEquippable = offHandEquip.TryGetCompFast<CompEquippable>();
                }
                else
                {
                    Log.Message("mainhand attack with " + stance.verb.EquipmentSource + "'s " + stance.verb);
                }
            }
            //Check if verb is one from a offhand weapon. 
            if (compEquippable != null && offHandEquip != pawn.equipment.Primary) //TODO: check this code 
            {
                pawn.GetStancesOffHand().SetStance(stance);
            }
            else if (stanceTracker.curStance.GetType().Name != "Stance_RunAndGun_Cooldown")
            {
                stanceTracker.SetStance(stance);
            }
        }

        public static void Notify_AttackedTarget(Pawn_MindState mindState, LocalTargetInfo target, Verb verb)
        {
            if (verb.EquipmentSource != null && verb.EquipmentSource == mindState.pawn.equipment.Primary)
            {
                mindState.lastAttackTargetTick = Find.TickManager.TicksGame;
            }
            mindState.lastAttackedTarget = target;
        }
    }
}
