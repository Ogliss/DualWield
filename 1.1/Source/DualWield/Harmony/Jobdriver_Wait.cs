using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using Verse;
using Verse.AI;

namespace DualWield.Harmony
{
    
    [HarmonyPatch(typeof(JobDriver_Wait), "CheckForAutoAttack")]
    public class Jobdriver_Wait_CheckForAutoAttack
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = new List<CodeInstruction>(instructions);
            IEnumerable<CodeInstruction> newInstructions =
            instructions.MethodReplacer(typeof(Pawn_StanceTracker).GetMethod("get_FullBodyBusy"), typeof(Jobdriver_Wait_CheckForAutoAttack).GetMethod("FullBodyAndOffHandBusy"));
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
            if (instance.pawn.GetStancesOffHand() is Pawn_StanceTracker stOffHand && instance.pawn.equipment != null && instance.pawn.equipment.TryGetOffHandEquipment(out ThingWithComps twc))
            {
                if (Prefs.DevMode && instance.pawn == Find.Selector.SingleSelectedThing && KeyBindingDefOf.ModifierIncrement_10x.IsDown)
                {
                    Log.Warning(instance.pawn+ " CheckForAutoAttack FullBodyAndOffHandBusy Mainhand Busy: " + instance.FullBodyBusy + " Offhand Busy: "+ stOffHand.FullBodyBusy);
                }
                return stOffHand.FullBodyBusy && instance.FullBodyBusy;
            }
            return instance.FullBodyBusy;
        }
    }
    

}
