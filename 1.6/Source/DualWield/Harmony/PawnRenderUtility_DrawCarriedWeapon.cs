using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace DualWield.HarmonyInstance
{
//    [HarmonyPatch(typeof(PawnRenderUtility), "DrawCarriedWeapon")]
    public class PawnRenderUtility_DrawCarriedWeapon
    {

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo drawEquipmentAiming = AccessTools.Method(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming));
            MethodInfo drawEquipmentAimingModified = AccessTools.Method(typeof(PawnRenderUtility_DrawEquipmentAndApparelExtras), nameof(PawnRenderUtility_DrawEquipmentAndApparelExtras.DrawEquipmenModified));
            var instructionsList = new List<CodeInstruction>(instructions);
            for (int i = 0; i < instructionsList.Count; i++)
            {
                CodeInstruction instruction = instructionsList[i];
                if (instruction.OperandIs(drawEquipmentAiming))
                {
                    if (drawEquipmentAimingModified != null)
                    {
                    //    Log.Message("DrawCarriedWeapon");
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        instruction = new CodeInstruction(OpCodes.Call, drawEquipmentAimingModified);
                    }
                }
                yield return instruction;
            }
        }

    }
}
