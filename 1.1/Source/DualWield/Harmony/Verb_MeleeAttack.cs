using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Verse;

namespace DualWield.Harmony
{
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    class Verb_MeleeAttack_TryCastShot
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = new List<CodeInstruction>(instructions);
            MethodInfo fullBodyBusy = typeof(Pawn_StanceTracker).GetMethod("get_FullBodyBusy");
            foreach (CodeInstruction instruction in instructionsList)
            {
                if (instruction.OperandIs(fullBodyBusy))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, typeof(Verb_MeleeAttack_TryCastShot).GetMethod("CurrentHandBusy"));
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static bool CurrentHandBusy(Pawn_StanceTracker instance, Verb verb)
        {
            Pawn pawn = instance.pawn;
            if(verb.EquipmentSource == null || !verb.EquipmentSource.IsOffHand())
            {
                return !verb.Available() || pawn.stances.curStance.StanceBusy;
            }
            else if (pawn.GetStancesOffHand() is Pawn_StanceTracker stancesOffHand)
            {
                return !verb.Available() || stancesOffHand.curStance.StanceBusy;
            }
            return false;
        }
    }
}
