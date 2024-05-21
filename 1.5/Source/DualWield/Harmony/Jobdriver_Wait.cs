using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Verse;
using Verse.AI;

namespace DualWield.HarmonyInstance 
{
    
    [HarmonyPatch(typeof(JobDriver_Wait), "CheckForAutoAttack")]
    public class Jobdriver_Wait_CheckForAutoAttack
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = new List<CodeInstruction>(instructions);
            MethodInfo fullBodyBusy = typeof(Pawn_StanceTracker).GetMethod("get_FullBodyBusy");
            MethodInfo fullBodyAndOffhandBusy = typeof(Jobdriver_Wait_CheckForAutoAttack).GetMethod("FullBodyAndOffHandBusy");
            foreach (CodeInstruction instruction in instructionsList)
            {
                if(instruction.OperandIs(fullBodyBusy))
                {
                    Log.Message("FullyBodyBusy replaced");
                    yield return new CodeInstruction(OpCodes.Call, fullBodyAndOffhandBusy);
                }
                else
                {
                    yield return instruction;
                }
            }

        }
        public static bool FullBodyAndOffHandBusy(Pawn_StanceTracker instance)
        {
            if(instance.pawn.GetStancesOffHand() is Pawn_StanceTracker stOffHand && instance.pawn.equipment != null && instance.pawn.equipment.TryGetOffHandEquipment(out ThingWithComps twc))
            {
                return stOffHand.FullBodyBusy && instance.FullBodyBusy;
            }
            return instance.FullBodyBusy;
        }
    }
    

}
