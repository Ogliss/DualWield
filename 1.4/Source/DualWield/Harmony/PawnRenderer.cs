﻿using DualWield.Settings;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
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
    [HarmonyPatch(typeof(PawnRenderer), "DrawEquipment")]
    public class PawnRenderer_DrawEquipment
    {
        
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo drawEquipmentAiming = AccessTools.Method(typeof(PawnRenderer), nameof(PawnRenderer.DrawEquipmentAiming));
            MethodInfo drawEquipmentAimingModified = AccessTools.Method(typeof(PawnRenderer_DrawEquipment), nameof(PawnRenderer_DrawEquipment.DrawEquipmenModified));
            var instructionsList = new List<CodeInstruction>(instructions);
            for (int i = 0; i < instructionsList.Count; i++)
            {
                CodeInstruction instruction = instructionsList[i];
                
                    if (instruction.OperandIs(drawEquipmentAiming))
                    {
                        Log.Message($"{i}  opcode: {instruction.opcode} operand: {instruction.operand}");
                        if (drawEquipmentAimingModified != null)
                        {
                            instruction = new CodeInstruction(OpCodes.Call, drawEquipmentAimingModified);
                            Log.Message($"{i}  Patched");
                        }
                    }
                
                
                yield return instruction;
            }
        }
        /*
        private static bool Prefix(Vector3 rootLoc, Rot4 pawnRotation, PawnRenderFlags flags, PawnRenderer __instance)
        {
            if (__instance.pawn.Dead || !__instance.pawn.Spawned)
            {
                return true;
            }
            if (__instance.pawn.equipment == null || __instance.pawn.equipment.Primary == null)
            {
                return true;
            }
            if (__instance.pawn.CurJob != null && __instance.pawn.CurJob.def.neverShowWeapon)
            {
                return true;
            }
            Vector3 vector = new Vector3(0f, (pawnRotation == Rot4.North) ? -0.0028957527f : 0.03474903f, 0f);
            Stance_Busy stance_Busy = __instance.pawn.stances.curStance as Stance_Busy;
            float equipmentDrawDistanceFactor = __instance.pawn.ageTracker.CurLifeStage.equipmentDrawDistanceFactor;
            if (stance_Busy != null && !stance_Busy.neverAimWeapon && stance_Busy.focusTarg.IsValid && (flags & PawnRenderFlags.NeverAimWeapon) == PawnRenderFlags.None)
            {
                Vector3 a;
                if (stance_Busy.focusTarg.HasThing)
                {
                    a = stance_Busy.focusTarg.Thing.DrawPos;
                }
                else
                {
                    a = stance_Busy.focusTarg.Cell.ToVector3Shifted();
                }
                float num = 0f;
                if ((a - __instance.pawn.DrawPos).MagnitudeHorizontalSquared() > 0.001f)
                {
                    num = (a - __instance.pawn.DrawPos).AngleFlat();
                }
                Verb currentEffectiveVerb = __instance.pawn.CurrentEffectiveVerb;
                if (currentEffectiveVerb != null && currentEffectiveVerb.AimAngleOverride != null)
                {
                    num = currentEffectiveVerb.AimAngleOverride.Value;
                }
                vector += rootLoc + new Vector3(0f, 0f, 0.4f + __instance.pawn.equipment.Primary.def.equippedDistanceOffset).RotatedBy(num) * equipmentDrawDistanceFactor;
                DrawEquipmenModified(__instance.pawn.equipment.Primary, vector, num, __instance);
                return false;
            }
            if (__instance.CarryWeaponOpenly())
            {
                if (pawnRotation == Rot4.South)
                {
                    vector += rootLoc + new Vector3(0f, 0f, -0.22f) * equipmentDrawDistanceFactor;
                    DrawEquipmenModified(__instance__instance.pawn.equipment.Primary, vector, 143f);
                    return false;
                }
                if (pawnRotation == Rot4.North)
                {
                    vector += rootLoc + new Vector3(0f, 0f, -0.11f) * equipmentDrawDistanceFactor;
                    DrawEquipmenModified(__instance.pawn.equipment.Primary, vector, 143f, __instance);
                    return false;
                }
                if (pawnRotation == Rot4.East)
                {
                    vector += rootLoc + new Vector3(0.2f, 0f, -0.22f) * equipmentDrawDistanceFactor;
                    DrawEquipmenModified(__instance.pawn.equipment.Primary, vector, 143f, __instance);
                    return false;
                }
                if (pawnRotation == Rot4.West)
                {
                    vector += rootLoc + new Vector3(-0.2f, 0f, -0.22f) * equipmentDrawDistanceFactor;
                    DrawEquipmenModified(__instance.pawn.equipment.Primary, vector, 217f, __instance);
                    return false;
                }
            }
            return true;
        }
        */

        public static void DrawEquipmenModified(PawnRenderer __instance, Thing eq, Vector3 drawLoc, float aimAngle)
        {
            ThingWithComps offHandEquip = null;
            Pawn pawn = __instance.pawn;
            if (pawn.equipment == null)
            {
                __instance.DrawEquipmentAiming(eq, drawLoc, aimAngle);
                return;
            }
            if (pawn.equipment.TryGetOffHandEquipment(out ThingWithComps result))
            {
                offHandEquip = result;
            }
            if (offHandEquip == null)
            {
                __instance.DrawEquipmentAiming(eq, drawLoc, aimAngle);
                return;
            }
            float mainHandAngle = aimAngle;
            float offHandAngle = aimAngle;
            Stance_Busy mainStance = pawn.stances.curStance as Stance_Busy;
            Stance_Busy offHandStance = null;
            if (pawn.GetStancesOffHand() != null)
            {
                offHandStance = pawn.GetStancesOffHand().curStance as Stance_Busy;
            }
            LocalTargetInfo focusTarg = null;
            if (mainStance != null && !mainStance.neverAimWeapon)
            {
                focusTarg = mainStance.focusTarg;
            }
            else if (offHandStance != null && !offHandStance.neverAimWeapon)
            {
                focusTarg = offHandStance.focusTarg;
            }

            bool mainHandAiming = CurrentlyAiming(mainStance);
            bool offHandAiming = CurrentlyAiming(offHandStance);

            Vector3 offsetMainHand = new Vector3();
            Vector3 offsetOffHand = new Vector3();
            //bool currentlyAiming = (mainStance != null && !mainStance.neverAimWeapon && mainStance.focusTarg.IsValid) || stancesOffHand.curStance is Stance_Busy ohs && !ohs.neverAimWeapon && ohs.focusTarg.IsValid;
            //When wielding offhand weapon, facing south, and not aiming, draw differently 

            SetAnglesAndOffsets(eq, offHandEquip, aimAngle, pawn, ref offsetMainHand, ref offsetOffHand, ref mainHandAngle, ref offHandAngle, mainHandAiming, offHandAiming);

            if (offHandEquip != pawn.equipment.Primary)
            {
                //drawLoc += offsetMainHand;
                //aimAngle = mainHandAngle;
                //__instance.DrawEquipmentAiming(eq, drawLoc + offsetMainHand, mainHandAngle);
                __instance.DrawEquipmentAiming(eq, drawLoc + offsetMainHand, mainHandAngle);
            }
            if ((offHandAiming || mainHandAiming) && focusTarg != null)
            {
                offHandAngle = GetAimingRotation(pawn, focusTarg);
                offsetOffHand.y += 0.1f;
                Vector3 adjustedDrawPos = pawn.DrawPos + new Vector3(0f, 0f, 0.4f).RotatedBy(offHandAngle) + offsetOffHand;
                __instance.DrawEquipmentAiming(offHandEquip, adjustedDrawPos, offHandAngle);
            }
            else
            {
                __instance.DrawEquipmentAiming(offHandEquip, drawLoc + offsetOffHand, offHandAngle);
            }
        }

        private static void SetAnglesAndOffsets(Thing eq, ThingWithComps offHandEquip, float aimAngle, Pawn pawn, ref Vector3 offsetMainHand, ref Vector3 offsetOffHand, ref float mainHandAngle, ref float offHandAngle, bool mainHandAiming, bool offHandAiming)
        {
            bool offHandIsMelee = IsMeleeWeapon(offHandEquip);
            bool mainHandIsMelee = IsMeleeWeapon(pawn.equipment.Primary);
            float meleeAngleFlipped = Base.meleeMirrored ? 360 - Base.meleeAngle : Base.meleeAngle;
            float rangedAngleFlipped = Base.rangedMirrored ? 360 - Base.rangedAngle : Base.rangedAngle;

            if (pawn.Rotation == Rot4.East)
            {
                offsetOffHand.y = -1f;
                offsetOffHand.z = 0.1f;
            }
            else if (pawn.Rotation == Rot4.West)
            {
                offsetMainHand.y = -1f;
                //zOffsetMain = 0.25f;
                offsetOffHand.z = -0.1f;
            }
            else if (pawn.Rotation == Rot4.North)
            {
                if (!mainHandAiming && !offHandAiming)
                {
                    offsetMainHand.x = mainHandIsMelee ? Base.meleeXOffset : Base.rangedXOffset;
                    offsetOffHand.x = offHandIsMelee ? -Base.meleeXOffset : -Base.rangedXOffset;
                    offsetMainHand.z = mainHandIsMelee ? Base.meleeZOffset : Base.rangedZOffset;
                    offsetOffHand.z = offHandIsMelee ? -Base.meleeZOffset : -Base.rangedZOffset;
                    offHandAngle = offHandIsMelee ? Base.meleeAngle : Base.rangedAngle;
                    mainHandAngle = mainHandIsMelee ? meleeAngleFlipped : rangedAngleFlipped;

                }
                else
                {
                    offsetOffHand.x = -0.1f;
                }
            }
            else
            {
                if (!mainHandAiming && !offHandAiming)
                {
                    offsetMainHand.y = 1f;
                    offsetMainHand.x = mainHandIsMelee ? -Base.meleeXOffset : -Base.rangedXOffset;
                    offsetOffHand.x = offHandIsMelee ? Base.meleeXOffset : Base.rangedXOffset;
                    offsetMainHand.z = mainHandIsMelee ? -Base.meleeZOffset : -Base.rangedZOffset;
                    offsetOffHand.z = offHandIsMelee ? Base.meleeZOffset : Base.rangedZOffset;
                    offHandAngle = offHandIsMelee ? meleeAngleFlipped : rangedAngleFlipped;
                    mainHandAngle = mainHandIsMelee ? Base.meleeAngle : Base.rangedAngle;
                }
                else
                {
                    offsetOffHand.x = 0.1f;
                }
            }
            if (!pawn.Rotation.IsHorizontal)
            {
                if (Base.customRotations.Value.inner.TryGetValue((offHandEquip.def.defName), out Record offHandValue))
                {
                    offHandAngle += pawn.Rotation == Rot4.North ? offHandValue.extraRotation : -offHandValue.extraRotation;
                    //offHandAngle %= 360;
                }
                if (Base.customRotations.Value.inner.TryGetValue((eq.def.defName), out Record mainHandValue))
                {
                    mainHandAngle += pawn.Rotation == Rot4.North ? -mainHandValue.extraRotation : mainHandValue.extraRotation;
                    //mainHandAngle %= 360;
                }
            }
        }

        private static float GetAimingRotation(Pawn pawn, LocalTargetInfo focusTarg)
        {
            Vector3 a;
            if (focusTarg.HasThing)
            {
                a = focusTarg.Thing.DrawPos;
            }
            else
            {
                a = focusTarg.Cell.ToVector3Shifted();
            }
            float num = 0f;
            if ((a - pawn.DrawPos).MagnitudeHorizontalSquared() > 0.001f)
            {
                num = (a - pawn.DrawPos).AngleFlat();
            }

            return num;
        }
        private static bool CurrentlyAiming(Stance_Busy stance)
        {
            return (stance != null && !stance.neverAimWeapon && stance.focusTarg.IsValid);
        }
        private static bool IsMeleeWeapon(ThingWithComps eq)
        {
            if (eq == null)
            {
                return false;
            }
            if (eq.TryGetComp<CompEquippable>() is CompEquippable ceq)
            {
                if (ceq.PrimaryVerb.IsMeleeAttack)
                {
                    return true;
                }
            }
            return false;
        }
    }

    // old method, no longer used
//    [HarmonyPatch(typeof(PawnRenderer), "DrawEquipmentAiming")]
    public class PawnRenderer_DrawEquipmentAiming
    {
        /*
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = new List<CodeInstruction>(instructions);
            foreach (CodeInstruction instruction in instructionsList)
            {
                if (instruction.operand == typeof(PawnRenderer).GetMethod("DrawEquipmentAiming"))
                {
                    yield return new CodeInstruction(OpCodes.Call, typeof(PawnRenderer_DrawEquipment).GetMethod("DrawEquipmentAimingModified"));
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        */
        static bool Prefix(PawnRenderer __instance, Thing eq, ref Vector3 drawLoc, ref float aimAngle, ref Pawn ___pawn)
        {
            ThingWithComps offHandEquip = null;
            if (___pawn.equipment == null)
            {
                return true;
            }
            if (___pawn.equipment.TryGetOffHandEquipment(out ThingWithComps result))
            {
                offHandEquip = result;
            }
            if(offHandEquip == null)
            {
                return true;
            }
            float mainHandAngle = aimAngle;
            float offHandAngle = aimAngle;
            Stance_Busy mainStance = ___pawn.stances.curStance as Stance_Busy;
            Stance_Busy offHandStance = null;
            if (___pawn.GetStancesOffHand() != null)
            {
                offHandStance = ___pawn.GetStancesOffHand().curStance as Stance_Busy;
            }
            LocalTargetInfo focusTarg = null;
            if (mainStance != null && !mainStance.neverAimWeapon)
            {
                focusTarg = mainStance.focusTarg;
            }
            else if (offHandStance != null && !offHandStance.neverAimWeapon)
            {
                focusTarg = offHandStance.focusTarg;
            }

            bool mainHandAiming = CurrentlyAiming(mainStance);
            bool offHandAiming = CurrentlyAiming(offHandStance);

            Vector3 offsetMainHand = new Vector3();
            Vector3 offsetOffHand = new Vector3();
            //bool currentlyAiming = (mainStance != null && !mainStance.neverAimWeapon && mainStance.focusTarg.IsValid) || stancesOffHand.curStance is Stance_Busy ohs && !ohs.neverAimWeapon && ohs.focusTarg.IsValid;
            //When wielding offhand weapon, facing south, and not aiming, draw differently 

            SetAnglesAndOffsets(eq, offHandEquip, aimAngle, ___pawn, ref offsetMainHand, ref offsetOffHand, ref mainHandAngle, ref offHandAngle, mainHandAiming, offHandAiming);

            if (offHandEquip != ___pawn.equipment.Primary)
            {
                //drawLoc += offsetMainHand;
                //aimAngle = mainHandAngle;
                //__instance.DrawEquipmentAiming(eq, drawLoc + offsetMainHand, mainHandAngle);
                DrawEquipmentAimingOverride(eq, drawLoc + offsetMainHand, mainHandAngle);
            }
            if ((offHandAiming || mainHandAiming) && focusTarg != null)
            {
                offHandAngle = GetAimingRotation(___pawn, focusTarg);
                offsetOffHand.y += 0.1f;
                Vector3 adjustedDrawPos = ___pawn.DrawPos + new Vector3(0f, 0f, 0.4f).RotatedBy(offHandAngle) + offsetOffHand;
                DrawEquipmentAimingOverride(offHandEquip, adjustedDrawPos, offHandAngle);
            }
            else
            {
                DrawEquipmentAimingOverride(offHandEquip, drawLoc + offsetOffHand, offHandAngle);
            }
            return false;      
        }

        //Copied from vanilla. 
        public static void DrawEquipmentAimingOverride(Thing eq, Vector3 drawLoc, float aimAngle)
        {
            float num = aimAngle - 90f;
            Mesh mesh;
            if (aimAngle > 20f && aimAngle < 160f)
            {
                mesh = MeshPool.plane10;
                num += eq.def.equippedAngleOffset;
            }
            else if (aimAngle > 200f && aimAngle < 340f)
            {
                mesh = MeshPool.plane10Flip;
                num -= 180f;
                num -= eq.def.equippedAngleOffset;
            }
            else
            {
                mesh = MeshPool.plane10;
                num += eq.def.equippedAngleOffset;
            }
            num %= 360f;
            CompEquippable compEquippable = eq.TryGetComp<CompEquippable>();
            if (compEquippable != null)
            {
                Vector3 b;
                float num2;
                EquipmentUtility.Recoil(eq.def, EquipmentUtility.GetRecoilVerb(compEquippable.AllVerbs), out b, out num2, aimAngle);
                drawLoc += b;
                num += num2;
            }
            Graphic_StackCount graphic_StackCount = eq.Graphic as Graphic_StackCount;
            Material material;
            if (graphic_StackCount != null)
            {
                material = graphic_StackCount.SubGraphicForStackCount(1, eq.def).MatSingleFor(eq);
            }
            else
            {
                material = eq.Graphic.MatSingleFor(eq);
            }
            Vector3 s = new Vector3(eq.Graphic.drawSize.x, 0f, eq.Graphic.drawSize.y);
            Matrix4x4 matrix = Matrix4x4.TRS(drawLoc, Quaternion.AngleAxis(num, Vector3.up), s);
            Graphics.DrawMesh(mesh, matrix, material, 0);
        }

        private static void SetAnglesAndOffsets(Thing eq, ThingWithComps offHandEquip, float aimAngle, Pawn pawn, ref Vector3 offsetMainHand, ref Vector3 offsetOffHand, ref float mainHandAngle, ref float offHandAngle, bool mainHandAiming, bool offHandAiming)
        {
            bool offHandIsMelee = IsMeleeWeapon(offHandEquip);
            bool mainHandIsMelee = IsMeleeWeapon(pawn.equipment.Primary);
            float meleeAngleFlipped = Base.meleeMirrored ? 360 - Base.meleeAngle : Base.meleeAngle;
            float rangedAngleFlipped = Base.rangedMirrored ? 360 - Base.rangedAngle : Base.rangedAngle;

            if (pawn.Rotation == Rot4.East)
            {
                offsetOffHand.y = -1f;
                offsetOffHand.z = 0.1f;
            }
            else if (pawn.Rotation == Rot4.West)
            {
                offsetMainHand.y = -1f;
                //zOffsetMain = 0.25f;
                offsetOffHand.z = -0.1f;
            }
            else if (pawn.Rotation == Rot4.North)
            {
                if (!mainHandAiming && !offHandAiming)
                {
                    offsetMainHand.x = mainHandIsMelee ? Base.meleeXOffset : Base.rangedXOffset;
                    offsetOffHand.x = offHandIsMelee ? -Base.meleeXOffset : -Base.rangedXOffset;
                    offsetMainHand.z = mainHandIsMelee ? Base.meleeZOffset : Base.rangedZOffset;
                    offsetOffHand.z = offHandIsMelee ? -Base.meleeZOffset : -Base.rangedZOffset;
                    offHandAngle = offHandIsMelee ? Base.meleeAngle : Base.rangedAngle;
                    mainHandAngle = mainHandIsMelee ? meleeAngleFlipped : rangedAngleFlipped;

                }
                else
                {
                    offsetOffHand.x = -0.1f;
                }
            }
            else
            {
                if (!mainHandAiming && !offHandAiming)
                {
                    offsetMainHand.y = 1f;
                    offsetMainHand.x = mainHandIsMelee ? -Base.meleeXOffset : -Base.rangedXOffset;
                    offsetOffHand.x = offHandIsMelee ? Base.meleeXOffset : Base.rangedXOffset;
                    offsetMainHand.z = mainHandIsMelee ? -Base.meleeZOffset : -Base.rangedZOffset;
                    offsetOffHand.z = offHandIsMelee ? Base.meleeZOffset : Base.rangedZOffset;
                    offHandAngle = offHandIsMelee ? meleeAngleFlipped : rangedAngleFlipped;
                    mainHandAngle = mainHandIsMelee ? Base.meleeAngle : Base.rangedAngle;
                }
                else
                {
                    offsetOffHand.x = 0.1f;
                }
            }
            if (!pawn.Rotation.IsHorizontal)
            {
                if (Base.customRotations.Value.inner.TryGetValue((offHandEquip.def.defName), out Record offHandValue))
                {
                    offHandAngle += pawn.Rotation == Rot4.North ? offHandValue.extraRotation : -offHandValue.extraRotation;
                    //offHandAngle %= 360;
                }                
                if (Base.customRotations.Value.inner.TryGetValue((eq.def.defName), out Record mainHandValue))
                {
                    mainHandAngle += pawn.Rotation == Rot4.North ? -mainHandValue.extraRotation : mainHandValue.extraRotation;
                    //mainHandAngle %= 360;
                }
            }
        }

        private static float GetAimingRotation(Pawn pawn, LocalTargetInfo focusTarg)
        {
            Vector3 a;
            if (focusTarg.HasThing)
            {
                a = focusTarg.Thing.DrawPos;
            }
            else
            {
                a =focusTarg.Cell.ToVector3Shifted();
            }
            float num = 0f;
            if ((a - pawn.DrawPos).MagnitudeHorizontalSquared() > 0.001f)
            {
                num = (a - pawn.DrawPos).AngleFlat();
            }

            return num;
        }
        private static bool CurrentlyAiming(Stance_Busy stance)
        {
            return (stance != null && !stance.neverAimWeapon && stance.focusTarg.IsValid);
        }
        private static bool IsMeleeWeapon(ThingWithComps eq)
        {
            if(eq == null)
            {
                return false;
            }
            if(eq.TryGetComp<CompEquippable>() is CompEquippable ceq)
            {
                if (ceq.PrimaryVerb.IsMeleeAttack)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
