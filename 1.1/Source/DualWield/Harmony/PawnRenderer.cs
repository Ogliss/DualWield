using DualWield.Settings;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using Verse;

namespace DualWield.Harmony
{
    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
    [HarmonyPatch(new Type[]{typeof(Vector3), typeof(RotDrawMode), typeof(bool), typeof(bool) })]
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

//    [HarmonyPatch(typeof(PawnRenderer), "DrawEquipment")]
    public static class PawnRenderer_DrawEquipment
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo drawEquipmentAiming = AccessTools.Method(typeof(PawnRenderer), "DrawEquipmentAiming");
            MethodInfo preDrawEquipmentAiming = AccessTools.Method(typeof(PawnRenderer_DrawEquipment), "PreDrawEquipmentAiming");
            IEnumerable<CodeInstruction> newInstructions =
            instructions.MethodReplacer(drawEquipmentAiming, preDrawEquipmentAiming);
            var instructionsList = new List<CodeInstruction>(newInstructions);
            int instCount = instructionsList.Count - 1;
            for (int i = 0; i < instructionsList.Count; i++)
            {
                CodeInstruction instruction = instructionsList[i];
                if (i + 3 < instCount && instructionsList[i + 3].OperandIs(drawEquipmentAiming))
                {
                    Log.Message(i + ": 3 opcode: " + instruction.opcode + " operand: " + instruction.operand);
                }
                if (i + 2 < instCount && instructionsList[i + 2].OperandIs(drawEquipmentAiming))
                {
                    Log.Message(i + ": 2 opcode: " + instruction.opcode + " operand: " + instruction.operand);
                }
                if (i + 1 < instCount && instructionsList[i + 1].OperandIs(drawEquipmentAiming))
                {
                    Log.Message(i + ": 1 opcode: " + instruction.opcode + " operand: " + instruction.operand);
                }

                /*
                if (instruction.OperandIs(drawEquipmentAiming))
                {
                    instruction = new CodeInstruction(instruction.opcode, AccessTools.Method(typeof(PawnRenderer_DrawEquipment), "PreDrawEquipmentAiming"));
                }
                yield return instruction;
                */
            }
            return newInstructions;
        }

        // PawnRenderer_DrawEquipment.PreDrawEquipmentAiming
        public static void PreDrawEquipmentAiming(PawnRenderer instance, Thing eq, Vector3 drawLoc, float aimAngle)
        {
            Pawn pawn = instance.pawn;
            ThingWithComps offHandEquip = null;
            if (pawn.equipment == null)
            {
                instance.DrawEquipmentAiming(eq, drawLoc, aimAngle);
                return;
            }
            if (pawn.equipment.TryGetOffHandEquipment(out ThingWithComps result))
            {
                offHandEquip = result;
            }
            if (offHandEquip == null)
            {
                instance.DrawEquipmentAiming(eq, drawLoc, aimAngle);
                return;
            }
            float mainHandAngle = aimAngle;
            float offHandAngle = aimAngle;
            Stance_Busy mainhandStance = pawn.stances.curStance as Stance_Busy;
            Stance_Busy offHandStance = null;
            if (pawn.GetStancesOffHand() != null)
            {
                offHandStance = pawn.GetStancesOffHand().curStance as Stance_Busy;
            }
            LocalTargetInfo focusTarg = null;
            if (mainhandStance != null && !mainhandStance.neverAimWeapon && mainhandStance.focusTarg.IsValid)
            {
                focusTarg = mainhandStance.focusTarg;
            }
            else if (offHandStance != null && !offHandStance.neverAimWeapon && offHandStance.focusTarg.IsValid)
            {
                focusTarg = offHandStance.focusTarg;
            }

            bool mainHandAiming = CurrentlyAiming(mainhandStance);
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
                Vector3 offsetAtkAnim = new Vector3();
                float offsetAngleAtkAnim = 0;
                Vector3 adjustedDrawPos = pawn.DrawPos + offsetMainHand + offsetAtkAnim;

                if ((offHandAiming || mainHandAiming) && (mainhandStance?.focusTarg != null || offHandStance?.focusTarg != null))
                {
                    string log = string.Empty;
                    AnimationOffsetsAttack(pawn, eq, mainhandStance, out offsetAtkAnim, out offsetAngleAtkAnim, mainHandAngle, ref log);
                    adjustedDrawPos = pawn.DrawPos /*+ new Vector3(0f, 0f, 0.4f).RotatedBy(mainHandAngle)*/ + offsetMainHand + offsetAtkAnim;
                }

                instance.DrawEquipmentAiming(eq, adjustedDrawPos, mainHandAngle + offsetAngleAtkAnim);
            }

            if ((offHandAiming || mainHandAiming) && (mainhandStance?.focusTarg != null || offHandStance?.focusTarg != null))
            {
                Vector3 offsetAtkAnim = new Vector3();
                float offsetAngleAtkAnim = 0;
                offHandAngle = GetAimingRotation(pawn, focusTarg);
                offsetOffHand.y += 0.1f;
                string log = string.Empty;
                AnimationOffsetsAttack(pawn, eq, offHandStance, out offsetAtkAnim, out offsetAngleAtkAnim, mainHandAngle, ref log, true);
                Vector3 adjustedDrawPos = pawn.DrawPos + new Vector3(0f, 0f, 0.4f).RotatedBy(offHandAngle) + offsetOffHand + offsetAtkAnim;
                instance.DrawEquipmentAiming(offHandEquip, adjustedDrawPos, offHandAngle + offsetAngleAtkAnim);
            }
            else

            {
                instance.DrawEquipmentAiming(offHandEquip, pawn.DrawPos + offsetOffHand, offHandAngle);
            }
        }
        public static void AnimationOffsetsAttack(Pawn pawn, Thing weapon, Stance_Busy stance_Busy, out Vector3 drawOffset, out float angleOffset, float aimAngle, ref string logstring, bool isSub = false, bool log = false)
        {
            drawOffset = Vector3.zero;
            angleOffset = 0f;
            // 랜덤 공격타입 결정 
            // Random attack type determination
            int atkType = (pawn.LastAttackTargetTick + weapon.thingIDNumber) % 10000 % 1000 % 100 % 3;
            // if (log) Log.Message("B");
            // 공격 타입 테스트 
            // attack type test
            atkType = isSub ? 2 : atkType;
            // 공격 타입에 따른 각도 
            // Angle according to attack type
            string atktype;
            float addAngle = 0f;
            float addX = 0.35f;
            float addZ = 0f;
            switch (atkType)
            {
                // 낮을수록 위로, 높을수록 아래로 휘두름 
                // The lower the swing, the higher the swing (Translations weird, not sure its correct)
                default:
                    // Normal Attack
                    //  addZ = 0.25f;//
                    addAngle = 20f;
                    atktype = "Normal Attack";
                    break;
                case 1:
                    // Low Attack
                    addAngle = 35f;
                    addZ = -0.25f;//
                    atktype = "Low Attack";
                    break;
                case 2:
                    // 머리찌르기 
                    // High Attack
                    //  addAngle = isSub ? (pawn.Rotation == Rot4.West ? -25f : 25f) : -25f;
                    addZ = 0.35f;//
                    atktype = "High Attack";
                    break;
            }

            if (pawn.Rotation == Rot4.West)
            {
                addAngle = -addAngle;
                addX = -addX;
            }
            //if (log) Log.Message("C");
            // 원거리 무기일경우 각도보정 // Angle correction for ranged weapons
            if (weapon.def.IsRangedWeapon)
            {
                addAngle -= isSub ? -35f : 35f;
            }
            SimpleCurve
                curveAxisZ = new SimpleCurve
                {
                    {
                        new CurvePoint(0.1f, 0f),
                        true
                    },
                    {
                        new CurvePoint(0.25f, addZ * 0.5f),
                        true
                    },
                    {
                        new CurvePoint(0.5f, addZ),
                        true
                    }
                };
            SimpleCurve
                curveAxisX = new SimpleCurve
                {
                    {
                        new CurvePoint(0.1f, 0f),
                        true
                    },
                    {
                        new CurvePoint(0.25f, addX * 0.5f),
                        true
                    },
                    {
                        new CurvePoint(0.5f, addX),
                        true
                    }
                };
            SimpleCurve
                curveRotation = new SimpleCurve
                {
                    {
                        new CurvePoint(0.1f, 0f),
                        true
                    },
                    {
                        new CurvePoint(0.125f, addAngle * 0.5f),
                        true
                    },
                    {
                        new CurvePoint(0.5f, addAngle),
                        true
                    }
                };

            if (stance_Busy != null && stance_Busy.ticksLeft > 0f && stance_Busy.verb != null)
            {
                Rand.PushState(stance_Busy.ticksLeft);
                try
                {
                    /*
                    FloatRange range = new FloatRange(YayoCombat.meleeDelay * (0.5f * YayoCombat.meleeRandom), YayoCombat.meleeDelay * (1.5f * YayoCombat.meleeRandom));
                    float multiply = range.min;
                    */
                    float multiply = 1;
                    int baseCooldown = Mathf.Max(stance_Busy.verb.verbProps.AdjustedCooldown_NewTmp(stance_Busy.verb.tool, pawn, weapon.def, weapon.Stuff) * multiply, 0.2f).SecondsToTicks();
                    int ticksSinceLastShot = Mathf.Max(baseCooldown - stance_Busy.ticksLeft, 0);

                    if ((float)ticksSinceLastShot < baseCooldown)
                    {
                        float time = Mathf.InverseLerp(0, baseCooldown, stance_Busy.ticksLeft);
                        drawOffset = new Vector3(curveAxisX.Evaluate(time), 0f, curveAxisZ.Evaluate(time));
                        angleOffset = curveRotation.Evaluate(time);
                        aimAngle += angleOffset;
                        //    drawOffset = drawOffset.RotatedBy(angleOffset);
                        string details = string.Format("drawOffset: {0}, angleOffset: {1}", drawOffset, angleOffset);
                        if (log) logstring += (" " + details + " " + stance_Busy.verb.tool.LabelCap + "'s " + stance_Busy.verb.maneuver.defName + " " + atktype + " Anim: " + time + " LastShot: " + ticksSinceLastShot + " = baseCooldown: " + baseCooldown + " - ticksLeft: " + stance_Busy.ticksLeft + " addAngle: " + addAngle);

                    }
                }
                finally
                {
                    Rand.PopState();
                }
            }

        }

        public static void AnimationOffsetsIdle(Thing weapon, Pawn pawn, bool useTwirl, Vector3 offset, LocalTargetInfo focusTarg, out Vector3 drawOffset, out float angleOffset, float aimAngle, ref string logstring, bool isSub = false, bool log = false)
        {
            drawOffset = Vector3.zero;
            angleOffset = 0f;
            int tick = Mathf.Abs(pawn.HashOffsetTicks() % 1000000000);
            tick = tick % 100000000;
            tick = tick % 10000000;
            tick = tick % 1000000;

            tick = tick % 100000;
            tick = tick % 10000;
            tick = tick % 1000;
            float wiggle = 0f;
            if (!isSub)
            {
                wiggle = Mathf.Sin((float)tick * 0.05f);
            }
            else
            {
                wiggle = Mathf.Sin((float)tick * 0.05f + 1.5f);
            }
            float aniAngle = -5f;


            if (useTwirl)
            {
                if (!isSub)
                {
                    if (tick < 80 && tick >= 40)
                    {
                        angleOffset += (float)tick * 36f;
                        drawOffset += new Vector3(-0.2f, 0f, 0.1f);
                    }
                }
                else
                {
                    if (tick < 40)
                    {
                        angleOffset += (float)(tick - 40) * -36f;
                        drawOffset += new Vector3(0.2f, 0f, 0.1f);
                    }
                }
            }
            float angle = aimAngle;
            if (pawn.Rotation == Rot4.South)
            {
                drawOffset = new Vector3(0f, offset.z, wiggle * 0.05f);
                if (isSub)
                {
                    aniAngle *= -1f;
                }
                drawOffset.y += 0.03787879f;
            }
            if (pawn.Rotation == Rot4.North)
            {
                drawOffset = new Vector3(0f, offset.z, wiggle * 0.05f);
                if (isSub)
                {
                    aniAngle *= -1f;
                }
            }
            if (pawn.Rotation == Rot4.East)
            {
                drawOffset = new Vector3(0.2f, offset.z, wiggle * 0.05f);
                drawOffset.y += 0.03787879f;
            }
            if (pawn.Rotation == Rot4.West)
            {
                angle = 360 - aimAngle;
                drawOffset = new Vector3(-0.2f, offset.z, wiggle * 0.05f);
                drawOffset.y += 0.03787879f;
            }
            angleOffset = (angleOffset + angle + wiggle * aniAngle) - angle;
            drawOffset = drawOffset.RotatedBy(angleOffset);
            string details = string.Format("drawOffset: {0}, angleOffset: {1}", drawOffset, angleOffset);
            if (log) logstring += (" " + details + ": 대기 : Waiting" + " aimAngle: " + angle);

        }

        public static void SetAnglesAndOffsets(Thing eq, ThingWithComps offHandEquip, float aimAngle, Pawn pawn, ref Vector3 offsetMainHand, ref Vector3 offsetOffHand, ref float mainHandAngle, ref float offHandAngle, bool mainHandAiming, bool offHandAiming)
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
                offsetMainHand.z = 0.1f;
                //zOffsetMain = 0.25f;
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

        public static float GetAimingRotation(Pawn pawn, LocalTargetInfo focusTarg)
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
        public static bool CurrentlyAiming(Stance_Busy stance)
        {
            return (stance != null && !stance.neverAimWeapon && stance.focusTarg.IsValid);
        }
        public static bool IsMeleeWeapon(ThingWithComps eq)
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

    [StaticConstructorOnStartup]
    internal static class RenderHelpers
    {
        internal static readonly Material ArrowMatRed = MaterialPool.MatFrom("UI/Overlays/Arrow", ShaderDatabase.CutoutFlying, Color.red);
        internal static readonly Material ArrowMatGreen = MaterialPool.MatFrom("UI/Overlays/Arrow", ShaderDatabase.CutoutFlying, Color.green);
        internal static readonly Material ArrowMatWhite = MaterialPool.MatFrom("UI/Overlays/Arrow", ShaderDatabase.CutoutFlying, Color.white);
        internal static readonly Material ArrowMatBlue = MaterialPool.MatFrom("UI/Overlays/Arrow", ShaderDatabase.CutoutFlying, Color.blue);
    }


    [HarmonyPatch(typeof(PawnRenderer), "DrawEquipmentAiming")]
    internal static class PawnRenderer_DrawEquipmentAimingActual
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {

            List<CodeInstruction> list = instructions.ToList<CodeInstruction>();

            for (int i = 0; i < list.Count; i++)
            {
                CodeInstruction instruction = list[i];
                if (i > 1 && list[i - 1].opcode == OpCodes.Isinst && list[i - 1].OperandIs(typeof(Graphic_StackCount)))
                {
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldarga_S, 2);
                    yield return new CodeInstruction(OpCodes.Ldarg_3);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 0);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 1);
                    instruction = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PawnRenderer_DrawEquipmentAimingActual), "DrawPosModified"));
                    if (Prefs.DevMode) Log.Message("Dual Wield: DrawEquipmentAiming Transpiled");
                }
                if (instruction.opcode == OpCodes.Stloc_3)
                {
                }
                yield return instruction;
            }
        }
        public static void DrawPosModified(PawnRenderer instance, Thing eq, ref Vector3 drawLoc, float aimAngle, ref Mesh mesh, ref float drawAngle)
        {

            Pawn pawn = instance.pawn;
            bool log = Prefs.DevMode && Find.Selector.IsSelected(pawn);
            if (pawn.stances.stunner.Stunned)
            {
                return;
            }
            Stance_Busy mainStance = pawn.stances.curStance as Stance_Busy;
            Stance_Busy offHandStance = null;
            bool offhand = false;
            if (pawn.equipment.TryGetOffHandEquipment(out ThingWithComps result))
            {
                offhand = result == eq;
            }
            else
            {
                return;
            }
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

            Vector3 adjustedDrawPos = drawLoc;
            if ((PawnRenderer_DrawEquipmentAiming.CurrentlyAiming(offHandStance) || PawnRenderer_DrawEquipmentAiming.CurrentlyAiming(mainStance)) && focusTarg != null)
            {
                AnimationOffsetsAttack(pawn, ref drawLoc, eq, ref drawAngle, aimAngle, offhand ? offHandStance : mainStance, offhand, log);
            }
            //    num %= 360f;
            //    GenDraw.DrawLineBetween(drawLoc + new Vector3(0f, 0.1f, 0f), adjustedDrawPos + new Vector3(0f, 0.1f, 0f), RenderHelpers.ArrowMatWhite);
        //    GenDraw.DrawLineBetween(adjustedDrawPos + new Vector3(0f, 0.1f, 0f), adjustedDrawPos + new Vector3(0f, 0.1f, 0f), offhand ? RenderHelpers.ArrowMatGreen : RenderHelpers.ArrowMatRed);
            return;
        }

        public static void AnimationOffsetsAttack(Pawn pawn, ref Vector3 drawLoc, Thing weapon, ref float finalAngle, float aimAngle , Stance_Busy stance_Busy, bool isSub = false, bool log = false)
        {
            Vector3 drawOffset = Vector3.zero;
            float angleOffset = 0f;
            if (stance_Busy != null && stance_Busy.ticksLeft > 0f && stance_Busy.verb is Verb verb)
            {
                if (weapon.def.IsRangedWeapon && !verb.IsMeleeAttack)
                {
                    Vector3 offset = new Vector3(0f, 0f, 0.5f);
                    bool isMechanoid = pawn.RaceProps.IsMechanoid;
                    //Log.Message((pawn.LastAttackTargetTick + thing.thingIDNumber).ToString());
                    int ticksToNextBurstShot = verb.ticksToNextBurstShot;
                    int atkType = (pawn.LastAttackTargetTick + weapon.thingIDNumber) % 10000 % 1000 % 100 % 5; // 랜덤 공격타입 결정
                    Stance_Cooldown Stance_Cooldown = pawn.stances.curStance as Stance_Cooldown;
                    Stance_Warmup Stance_Warmup = pawn.stances.curStance as Stance_Warmup;

                    if (ticksToNextBurstShot > 10)
                    {
                        ticksToNextBurstShot = 10;
                    }

                    //atkType = 2; // 공격타입 테스트




                    float ani_burst = (float)ticksToNextBurstShot;
                    float ani_cool = (float)stance_Busy.ticksLeft;

                    float ani = 0f;
                    if (!isMechanoid)
                    {
                        ani = Mathf.Max(ani_cool, 25f) * 0.001f;
                    }

                    if (ticksToNextBurstShot > 0)
                    {
                        ani = ani_burst * 0.02f;
                    }

                    float addAngle = 0f;
                    float addX = offset.x;
                    float addY = offset.y;


                    // 준비동작 애니메이션
                    if (!isMechanoid)
                    {
                        float wiggle_slow = 0f;
                        if (!isSub)
                        {
                            wiggle_slow = Mathf.Sin(ani_cool * 0.035f) * 0.05f;
                        }
                        else
                        {
                            wiggle_slow = Mathf.Sin(ani_cool * 0.035f + 0.5f) * 0.05f;
                        }

                        switch (atkType)
                        {
                            case 0:
                                // 회전
                                if (stance_Busy.ticksLeft > 1)
                                {
                                    addY += wiggle_slow;
                                }

                                break;
                            case 1:
                                // 재장전
                                if (ticksToNextBurstShot == 0)
                                {
                                    if (stance_Busy.ticksLeft > 78)
                                    {

                                    }
                                    else if (stance_Busy.ticksLeft > 48 && Stance_Warmup == null)
                                    {
                                        float wiggle = Mathf.Sin(ani_cool * 0.1f) * 0.05f;
                                        addX += wiggle - 0.2f;
                                        addY += wiggle + 0.2f;
                                        addAngle += wiggle + 30f + ani_cool * 0.5f;
                                    }
                                    else if (stance_Busy.ticksLeft > 40 && Stance_Warmup == null)
                                    {
                                        float wiggle = Mathf.Sin(ani_cool * 0.1f) * 0.05f;
                                        float wiggle_fast = Mathf.Sin(ani_cool) * 0.05f;
                                        addX += wiggle_fast + 0.05f;
                                        addY += wiggle - 0.05f;
                                        addAngle += wiggle_fast * 100f - 15f;

                                    }
                                    else if (stance_Busy.ticksLeft > 1)
                                    {
                                        addY += wiggle_slow;
                                    }

                                }
                                break;
                            default:
                                if (stance_Busy.ticksLeft > 1)
                                {
                                    addY += wiggle_slow;
                                }
                                break;
                        }
                    }

                    if (pawn.Rotation == Rot4.South)
                    {
                        drawOffset = new Vector3(-addY, offset.z, 0.4f + addX - ani).RotatedBy(aimAngle);
                    }
                    if (pawn.Rotation == Rot4.North)
                    {
                        drawOffset = new Vector3(-addY, offset.z, 0.4f + addX - ani).RotatedBy(aimAngle);
                    }
                    if (pawn.Rotation == Rot4.East)
                    {
                        drawOffset = new Vector3(-addY, offset.z, 0.4f + addX - ani).RotatedBy(aimAngle);
                    }
                    if (pawn.Rotation == Rot4.West)
                    {
                        drawOffset = new Vector3(addY, offset.z, 0.4f + addX - ani).RotatedBy(aimAngle);
                    }


                    // 반동 계수
                    float reboundFactor = 70f;

                    angleOffset = pawn.Rotation == Rot4.West ? ani * reboundFactor + addAngle : -(ani * reboundFactor - addAngle);
                    drawLoc += drawOffset;
                }
                else
                { 
                    /*
                    FloatRange range = new FloatRange(YayoCombat.meleeDelay * (0.5f * YayoCombat.meleeRandom), YayoCombat.meleeDelay * (1.5f * YayoCombat.meleeRandom));
                    float multiply = range.min;
                    */
                    float multiply = 1;
                    int baseCooldown = Mathf.Max(verb.verbProps.AdjustedCooldown_NewTmp(verb.tool, pawn, weapon.def, weapon.Stuff) * multiply, 0.2f).SecondsToTicks();
                    baseCooldown = Math.Min(60, baseCooldown);
                    int ticksSinceLastShot = Mathf.Max(baseCooldown - stance_Busy.ticksLeft, 0);

                    // 랜덤 공격타입 결정 
                    // Random attack type determination
                    int atkType = (pawn.LastAttackTargetTick + weapon.thingIDNumber) % 10000 % 1000 % 100 % 3;
                    // if (log) Log.Message("B");
                    // 공격 타입 테스트 
                    // attack type test
                    //atkType = isSub ? 2 : atkType;
                    // 공격 타입에 따른 각도 
                    // Angle according to attack type
                    string atktype;
                    float addAngle = 0f;
                    float addX = 0.35f;
                    float addZ = 0f;
                    string tooltype = string.Empty;
                    string manuver = string.Empty;
                    if (stance_Busy?.verb?.tool?.label != null)
                    {
                        tooltype = verb.tool.label.ToLower();
                        manuver = verb.maneuver.defName.ToLower();
                    }
                    /*
                    if (tooltype.Contains("hilt") || tooltype.Contains("pommel") || tooltype.Contains("handle"))
                    {
                        atkType = 0;
                        addAngle = -95f;
                        addX = 0.1f;
                        addZ = 0.1f;
                    }
                    if (tooltype.Contains("point") || tooltype.Contains("tip") || manuver.Contains("stab"))
                    {
                        atkType = 0;
                        addAngle = 25f;
                        addX += 0.1f;
                        addZ = 0.1f;
                    }
                    */
                    Vector3 a;
                    if (stance_Busy.focusTarg.HasThing)
                    {
                        a = stance_Busy.focusTarg.Thing.DrawPos;
                    }
                    else
                    {
                        a = stance_Busy.focusTarg.Cell.ToVector3Shifted();
                    }
                    Vector2 b;
                    switch (atkType)
                    {
                        default:
                            // Normal Attack
                            //  addZ = 0.25f;//
                            atktype = "Normal Attack";
                            break;
                        case 1:
                            // Low Attack
                            addX += 0.05f;
                            a.z += -0.25f;//
                            atktype = "Low Attack";
                            break;
                        case 2:
                            // 머리찌르기 
                            // High Attack
                            //  addAngle = isSub ? (pawn.Rotation == Rot4.West ? -25f : 25f) : -25f;
                            addX += 0.15f;
                            a.z += 0.35f;//
                            atktype = "High Attack";
                            break;
                    }
                    if (isSub)
                    {
                        addX += 0.15f;
                    }
                    float num = 0f;
                    if ((a - drawLoc).MagnitudeHorizontalSquared() > 0.001f)
                    {
                        num = (a - drawLoc).AngleFlat();
                    }
                    //    drawOffset = a - drawLoc;
                    drawOffset = AddOffset(addX, num);
                    //if (log) Log.Message("C");
                    // 원거리 무기일경우 각도보정 // Angle correction for ranged weapons
                    if (weapon.def.IsRangedWeapon)
                    {
                        addAngle -= isSub ? -35f : 35f;
                    }
                    if (log) Log.Message("pre aimAngle addAngle: " + addAngle + " num: " + num + " aimAngle: " + aimAngle);
                    addAngle += distance(finalAngle, num);
                    addAngle -= 90;
                    if (log) Log.Message("pre curve addAngle: "+ addAngle + " num: "+ num+ " aimAngle: "+ aimAngle);
                    SimpleCurve
                        curveAxisZ = new SimpleCurve
                        {
                        {
                            new CurvePoint(0.1f, 0f),
                            true
                        },
                        {
                            new CurvePoint(0.5f, Math.Min(drawOffset.z * 0.5f, 0.5f)),
                            true
                        },
                        {
                            new CurvePoint(0.95f, Math.Min(drawOffset.z, 0.5f)),
                            true
                        }
                        };
                    SimpleCurve
                        curveAxisX = new SimpleCurve
                        {
                        {
                            new CurvePoint(0.1f, 0f),
                            true
                        },
                        {
                            new CurvePoint(0.5f, (drawOffset.x * addX) * 0.5f),
                            true
                        },
                        {
                            new CurvePoint(0.95f, drawOffset.x * addX),
                            true
                        }
                        };
                    SimpleCurve
                        curveRotation = new SimpleCurve
                        {
                        {
                            new CurvePoint(0.1f, 0f),
                            true
                        },
                        {
                            new CurvePoint(0.5f, addAngle * 0.5f),
                            true
                        },
                        {
                            new CurvePoint(0.95f, addAngle),
                            true
                        }
                        };

                    if ((float)ticksSinceLastShot < baseCooldown)
                    {
                        float time = Mathf.InverseLerp(0, baseCooldown, stance_Busy.ticksLeft);
                        addX = curveAxisX.Evaluate(time);
                        addZ = curveAxisZ.Evaluate(time);
                        addAngle = curveRotation.Evaluate(time);
                        if (pawn.Rotation == Rot4.West)
                        {
                            addAngle = -addAngle;
                            //    addX = -addX;
                        }
                    //    GenDraw.DrawLineBetween(drawLoc + new Vector3(addX, 0f, addZ) + new Vector3(0f, 0.1f, 0f), drawLoc + new Vector3(0f, 0.1f, 0f), isSub ? RenderHelpers.ArrowMatGreen : RenderHelpers.ArrowMatRed);
                    //    GenDraw.DrawLineBetween(drawLoc + new Vector3(addX, 0f, addZ) + AddOffset(0.25f, num) + new Vector3(0f, 0.1f, 0f), drawLoc + new Vector3(addX, 0f, addZ) + new Vector3(0f, 0.1f, 0f), RenderHelpers.ArrowMatBlue) ;

                        drawLoc += new Vector3(addX, 0f, addZ);
                        if (log) Log.Message("Original: " + finalAngle + " used finalAngle: " + finalAngle + addAngle + " addAngle: " + addAngle + " num: " + num + " aimAngle: " + aimAngle);
                        finalAngle += addAngle;

                        if (log) Log.Message(verb.tool.LabelCap + "'s " + verb.maneuver.defName + " " + atktype + " Anim: " + time + " LastShot: " + ticksSinceLastShot + " = baseCooldown: " + baseCooldown + " - ticksLeft: " + stance_Busy.ticksLeft + " addAngle: " + addAngle + string.Format(" drawOffset: {0}, angleOffset: {1}", drawOffset, angleOffset));

                    }
                }
            }

        }
        public static float distance(float alpha, float beta)
        {
            float phi = Math.Abs(beta - alpha) % 360;
            float distance = phi > 180f ? 360f - phi : phi;
            return distance;
        }
        public static Vector3 AddOffset(float dist, float dir)
        {
            Vector3 curOffset = new Vector3();
            curOffset += Quaternion.AngleAxis(dir, Vector3.up) * Vector3.forward * dist;
            if (curOffset.sqrMagnitude > JitterMax * JitterMax)
            {
                curOffset *= JitterMax / curOffset.magnitude;
            }
            return curOffset;
        }

        private static float JitterMax = 0.5f;
    }

    [HarmonyPatch(typeof(PawnRenderer), "DrawEquipment")]
    public class PawnRenderer_DrawEquipmentAiming
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo drawEquipmentAiming = AccessTools.Method(typeof(PawnRenderer), "DrawEquipmentAiming");
            MethodInfo preDrawEquipmentAiming = AccessTools.Method(typeof(PawnRenderer_DrawEquipmentAiming), "Prefixx");
            IEnumerable<CodeInstruction> newInstructions =
            instructions.MethodReplacer(drawEquipmentAiming, preDrawEquipmentAiming);
            var instructionsList = new List<CodeInstruction>(newInstructions);
            int instCount = instructionsList.Count - 1;
            for (int i = 0; i < instructionsList.Count; i++)
            {
                CodeInstruction instruction = instructionsList[i];
                if (instruction.opcode == OpCodes.Ldc_R4 && (float)instruction.operand == 0.4f)
                {
                    if (instruction.opcode == OpCodes.Ldc_R4)
                    {
                        instruction = new CodeInstruction(OpCodes.Ldc_R4, Base.using_yayoCombat ? 0.2f : 0.2f);
                    }
                }
                if (instruction.opcode == OpCodes.Stloc_2 && instructionsList[i + 1].opcode == OpCodes.Ldarg_1)
                {
                    Log.Message(i + ": Draw Angle Aiming opcode: " + instruction.opcode + " operand: " + instruction.operand);
                }
                if (instruction.opcode == OpCodes.Stloc_3)
                {
                    Log.Message(i + ": Draw Loc Aiming opcode: " + instruction.opcode + " operand: " + instruction.operand);
                }
                if (instruction.opcode == OpCodes.Stloc_S && ((LocalBuilder)instruction.operand).LocalIndex == 5)
                {
                    Log.Message(i + ": Draw Loc South opcode: " + instruction.opcode + " operand: " + instruction.operand);
                    instructionsList.Insert(i + 1, new CodeInstruction(OpCodes.Stloc_2, 143));
                }
                if (instruction.opcode == OpCodes.Stloc_S && ((LocalBuilder)instruction.operand).LocalIndex == 6)
                {
                    Log.Message(i + ": Draw Loc North opcode: " + instruction.opcode + " operand: " + instruction.operand);
                    instructionsList.Insert(i + 1, new CodeInstruction(OpCodes.Stloc_2, 143));
                }
                if (instruction.opcode == OpCodes.Stloc_S && ((LocalBuilder)instruction.operand).LocalIndex == 7)
                {
                    Log.Message(i + ": Draw Loc East opcode: " + instruction.opcode + " operand: " + instruction.operand);
                    instructionsList.Insert(i + 1, new CodeInstruction(OpCodes.Stloc_2, 143));
                }
                if (instruction.opcode == OpCodes.Stloc_S && ((LocalBuilder)instruction.operand).LocalIndex == 8)
                {
                    Log.Message(i + ": Draw Loc West opcode: " + instruction.opcode + " operand: " + instruction.operand);
                    instructionsList.Insert(i + 1, new CodeInstruction(OpCodes.Stloc_2, 217));
                }
                if (i + 3 < instCount && instructionsList[i + 3].OperandIs(preDrawEquipmentAiming))
                {
                    Log.Message(i + ": 3 opcode: " + instruction.opcode + " operand: " + instruction.operand);
                }
                if (i + 2 < instCount && instructionsList[i + 2].OperandIs(preDrawEquipmentAiming))
                {
                    Log.Message(i + ": 2 opcode: " + instruction.opcode + " operand: " + instruction.operand);
                }
                if (i + 1 < instCount && instructionsList[i + 1].OperandIs(preDrawEquipmentAiming))
                {
                    if (instruction.opcode == OpCodes.Ldc_R4)
                    {
                        instruction = new CodeInstruction(OpCodes.Ldloc_2);
                    }
                    Log.Message(i + ": 1 opcode: " + instruction.opcode + " operand: " + instruction.operand);
                }


                //   if (instruction.OperandIs(drawEquipmentAiming))
                //   {
                //       instruction = new CodeInstruction(instruction.opcode, AccessTools.Method(typeof(PawnRenderer_DrawEquipment), "PreDrawEquipmentAiming"));
                //   }
                //   yield return instruction;

            }
            return newInstructions;
        }
        /*
        [TweakValue("Dual Wield Tweak")]
        static float MainhandXTweak = 0f;
        [TweakValue("Dual Wield Tweak")]
        static float MainhandZTweak = 0f;
        [TweakValue("Dual Wield Tweak")]
        static float MainhandRotTweak = 0f;

        [TweakValue("Dual Wield Tweak")]
        static float OffhandXTweak = 0f;
        [TweakValue("Dual Wield Tweak")]
        static float OffhandZTweak = 0f;
        [TweakValue("Dual Wield Tweak")]
        static float OffhandRotTweak = 0f;
        */

        static void Prefixx(PawnRenderer __instance, Thing eq, Vector3 drawLoc, float aimAngle)
        {
            Pawn ___pawn = __instance.pawn;
            ThingWithComps offHandEquip = null;
            if (___pawn.equipment == null)
            {
                return;
            }
            if (___pawn.equipment.TryGetOffHandEquipment(out ThingWithComps result))
            {
                offHandEquip = result;
            }
            if(offHandEquip == null)
            {
                return;
            }
            bool log = Prefs.DevMode && Find.Selector.IsSelected(___pawn);
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
                float angleOffset = 0f;
                Vector3 drawOffset = new Vector3();
                Vector3 adjustedDrawPos = drawLoc + offsetMainHand;
                
                if (!((offHandAiming || mainHandAiming) && focusTarg != null))
                {
                    AnimationOffsetsIdle(___pawn, eq, new Vector3(0, 0, 0.5f), false, out drawOffset, out angleOffset, mainHandAngle);
                    mainHandAngle += angleOffset;
                }
                else
                {
                    // AnimationOffsetsAttack(___pawn, eq, mainStance, out drawOffset, out angleOffset, mainHandAngle);
                    // drawOffset.z += 0.4f;
                    mainHandAngle = GetAimingRotation(adjustedDrawPos, focusTarg);
                }


                __instance.DrawEquipmentAiming(eq, adjustedDrawPos + drawOffset, mainHandAngle);
            }
            {
                float angleOffset = 0f;
                Vector3 drawOffset = new Vector3();
                Vector3 adjustedDrawPos = drawLoc + offsetOffHand;

                if (!((offHandAiming || mainHandAiming) && focusTarg != null))
                {
                    AnimationOffsetsIdle(___pawn, offHandEquip, new Vector3(0,0,0.5f), false, out drawOffset, out angleOffset, offHandAngle, true);

                    offHandAngle += angleOffset;
                }
                
                else
                {
                    offHandAngle = GetAimingRotation(adjustedDrawPos, focusTarg);
                    // offHandAngle = GetAimingRotation(___pawn, focusTarg);
                    // offsetOffHand.y += 0.1f;
                    // adjustedDrawPos = ___pawn.DrawPos + new Vector3(0f, 0f, 0.4f).RotatedBy(offHandAngle) + offsetOffHand;
                    // AnimationOffsetsAttack(___pawn, offHandEquip, offHandStance, out drawOffset, out angleOffset, offHandAngle, true);
                    // drawOffset.z += 0.4f;
                }


                __instance.DrawEquipmentAiming(offHandEquip, adjustedDrawPos + drawOffset, offHandAngle);
            }
        }
        

        public static void AnimationOffsetsAttack_New(Pawn pawn, Thing weapon, Stance_Busy stance_Busy, out Vector3 drawOffset, out float angleOffset, float aimAngle, bool isSub = false, bool log = false)
        {
            Vector3 offset = new Vector3(0f, 0f, 0.5f);
            bool isMechanoid = pawn.RaceProps.IsMechanoid;
            drawOffset = Vector3.zero;
            angleOffset = 0f;
            if (stance_Busy != null && stance_Busy.ticksLeft > 0f && stance_Busy.verb is Verb verb)
            {
                if (weapon.def.IsRangedWeapon && !verb.IsMeleeAttack)
                {
                    //Log.Message((pawn.LastAttackTargetTick + thing.thingIDNumber).ToString());
                    int ticksToNextBurstShot = verb.ticksToNextBurstShot;
                    int atkType = (pawn.LastAttackTargetTick + weapon.thingIDNumber) % 10000 % 1000 % 100 % 5; // 랜덤 공격타입 결정
                    Stance_Cooldown Stance_Cooldown = pawn.stances.curStance as Stance_Cooldown;
                    Stance_Warmup Stance_Warmup = pawn.stances.curStance as Stance_Warmup;

                    if (ticksToNextBurstShot > 10)
                    {
                        ticksToNextBurstShot = 10;
                    }

                    //atkType = 2; // 공격타입 테스트




                    float ani_burst = (float)ticksToNextBurstShot;
                    float ani_cool = (float)stance_Busy.ticksLeft;

                    float ani = 0f;
                    if (!isMechanoid)
                    {
                        ani = Mathf.Max(ani_cool, 25f) * 0.001f;
                    }

                    if (ticksToNextBurstShot > 0)
                    {
                        ani = ani_burst * 0.02f;
                    }

                    float addAngle = 0f;
                    float addX = offset.x;
                    float addY = offset.y;


                    // 준비동작 애니메이션
                    if (!isMechanoid)
                    {
                        float wiggle_slow = 0f;
                        if (!isSub)
                        {
                            wiggle_slow = Mathf.Sin(ani_cool * 0.035f) * 0.05f;
                        }
                        else
                        {
                            wiggle_slow = Mathf.Sin(ani_cool * 0.035f + 0.5f) * 0.05f;
                        }

                        switch (atkType)
                        {
                            case 0:
                                // 회전
                                if (stance_Busy.ticksLeft > 1)
                                {
                                    addY += wiggle_slow;
                                }

                                break;
                            case 1:
                                // 재장전
                                if (ticksToNextBurstShot == 0)
                                {
                                    if (stance_Busy.ticksLeft > 78)
                                    {

                                    }
                                    else if (stance_Busy.ticksLeft > 48 && Stance_Warmup == null)
                                    {
                                        float wiggle = Mathf.Sin(ani_cool * 0.1f) * 0.05f;
                                        addX += wiggle - 0.2f;
                                        addY += wiggle + 0.2f;
                                        addAngle += wiggle + 30f + ani_cool * 0.5f;
                                    }
                                    else if (stance_Busy.ticksLeft > 40 && Stance_Warmup == null)
                                    {
                                        float wiggle = Mathf.Sin(ani_cool * 0.1f) * 0.05f;
                                        float wiggle_fast = Mathf.Sin(ani_cool) * 0.05f;
                                        addX += wiggle_fast + 0.05f;
                                        addY += wiggle - 0.05f;
                                        addAngle += wiggle_fast * 100f - 15f;

                                    }
                                    else if (stance_Busy.ticksLeft > 1)
                                    {
                                        addY += wiggle_slow;
                                    }

                                }
                                break;
                            default:
                                if (stance_Busy.ticksLeft > 1)
                                {
                                    addY += wiggle_slow;
                                }
                                break;
                        }
                    }

                    if (pawn.Rotation == Rot4.South)
                    {
                        drawOffset = new Vector3(-addY, offset.z, 0.4f + addX - ani).RotatedBy(aimAngle);
                    }
                    if (pawn.Rotation == Rot4.North)
                    {
                        drawOffset = new Vector3(-addY, offset.z, 0.4f + addX - ani).RotatedBy(aimAngle);
                    }
                    if (pawn.Rotation == Rot4.East)
                    {
                        drawOffset = new Vector3(-addY, offset.z, 0.4f + addX - ani).RotatedBy(aimAngle);
                    }
                    if (pawn.Rotation == Rot4.West)
                    {
                        drawOffset = new Vector3(addY, offset.z, 0.4f + addX - ani).RotatedBy(aimAngle);
                    }


                    // 반동 계수
                    float reboundFactor = 70f;

                    angleOffset = pawn.Rotation == Rot4.West ? ani * reboundFactor + addAngle : - (ani * reboundFactor - addAngle);
                    return;
                }
                else
                {
                    float addAngle = 0f;
                    int atkType = (pawn.LastAttackTargetTick + weapon.thingIDNumber) % 10000 % 1000 % 100 % 3; // 랜덤 공격타입 결정

                    //Log.Message("B");
                    //atkType = 1; // 공격 타입 테스트

                    // 공격 타입에 따른 각도
                    switch (atkType)
                    {
                        // 낮을수록 위로, 높을수록 아래로 휘두름
                        default:
                            // 평범
                            addAngle = 0f;
                            break;
                        case 1:
                            // 내려찍기
                            addAngle = 25f;
                            break;
                        case 2:
                            // 머리찌르기
                            addAngle = -25f;
                            break;
                    }
                    //Log.Message("C");
                    // 원거리 무기일경우 각도보정
                    if (weapon.def.IsRangedWeapon)
                    {
                        addAngle -= 35f;
                    }

                    //Log.Message("D");

                    float readyZ = 0.1f;

                    //Log.Message("E");
                    if (stance_Busy.ticksLeft > 15)
                    {
                        //Log.Message("F");
                        // 애니메이션

                        float ani = Mathf.Min((float)stance_Busy.ticksLeft, 60f);
                        float ani2 = ani * 0.0075f; // 0.45f -> 0f
                        float addZ = offset.x;
                        float addX = offset.y;

                        switch (atkType)
                        {
                            default:
                                // 평범한 공격
                                addZ += readyZ + 0.05f + ani2; // 높을 수록 무기를 적쪽으로 내밀음
                                addX += 0.45f - 0.5f - ani2 * 0.1f; // 높을수록 무기를 아래까지 내려침
                                break;
                            case 1:
                                // 내려찍기
                                addZ += readyZ + 0.05f + ani2; // 높을 수록 무기를 적쪽으로 내밀음
                                addX += 0.45f - 0.35f + ani2 * 0.5f; // 높을수록 무기를 아래까지 내려침, 애니메이션 반대방향
                                ani = 30f + ani * 0.5f; // 각도 고정값 + 각도 변화량
                                break;
                            case 2:
                                // 머리찌르기
                                addZ += readyZ + 0.05f + ani2; // 높을 수록 무기를 적쪽으로 내밀음
                                addX += 0.45f - 0.35f - ani2; // 높을수록 무기를 아래까지 내려침
                                break;
                        }

                        // 회전 애니메이션
                        /*
                        if (useTwirl && pawn.LastAttackTargetTick % 5 == 0 && stance_Busy.ticksLeft <= 25)
                        {
                            //addAngle += ani2 * 5000f;
                        }
                        */
                        // 캐릭터 방향에 따라 적용
                        if (pawn.Rotation == Rot4.South)
                        {
                            drawOffset = new Vector3(-addX, offset.z, addZ);//.RotatedBy(aimAngle);
                            angleOffset += addAngle;
                            angleOffset += ani;
                        }
                        if (pawn.Rotation == Rot4.North)
                        {
                            drawOffset = new Vector3(-addX, offset.z, addZ);//.RotatedBy(aimAngle);
                            angleOffset += addAngle;
                            angleOffset += ani;
                        }
                        if (pawn.Rotation == Rot4.East)
                        {
                            drawOffset = new Vector3(addX, offset.z, addZ);//.RotatedBy(aimAngle);
                            angleOffset += addAngle;
                            angleOffset += ani;
                        }
                        if (pawn.Rotation == Rot4.West)
                        {
                            drawOffset = new Vector3(-addX, offset.z, addZ);//.RotatedBy(aimAngle);
                            angleOffset += addAngle;
                            angleOffset -= ani;
                        }
                    }
                    else
                    {
                        drawOffset = new Vector3(0f, offset.z, readyZ);//.RotatedBy(aimAngle);
                    }
                }
            }

        }
        
        public static void AnimationOffsetsAttack(Pawn pawn, Thing weapon, Stance_Busy stance_Busy, out Vector3 drawOffset, out float angleOffset, float aimAngle, bool isSub = false, bool log = false)
        {
            drawOffset = Vector3.zero;
            angleOffset = 0f;
            if (stance_Busy != null && stance_Busy.ticksLeft > 0f && stance_Busy.verb is Verb verb)
            {
                /*
                FloatRange range = new FloatRange(YayoCombat.meleeDelay * (0.5f * YayoCombat.meleeRandom), YayoCombat.meleeDelay * (1.5f * YayoCombat.meleeRandom));
                float multiply = range.min;
                */
                float multiply = 1;
                int baseCooldown = Mathf.Max(verb.verbProps.AdjustedCooldown_NewTmp(verb.tool, pawn, weapon.def, weapon.Stuff) * multiply, 0.2f).SecondsToTicks();
                int ticksSinceLastShot = Mathf.Max(baseCooldown - stance_Busy.ticksLeft, 0);

                // 랜덤 공격타입 결정 
                // Random attack type determination
                int atkType = (pawn.LastAttackTargetTick + weapon.thingIDNumber) % 10000 % 1000 % 100 % 3;
                // if (log) Log.Message("B");
                // 공격 타입 테스트 
                // attack type test
                //atkType = isSub ? 2 : atkType;
                // 공격 타입에 따른 각도 
                // Angle according to attack type
                string atktype;
                float addAngle = 0f;
                float addX = 0.35f;
                float addZ = 0f;
                string tooltype = string.Empty;
                string manuver = string.Empty;
                if (stance_Busy?.verb?.tool?.label != null)
                {
                    tooltype = verb.tool.label.ToLower();
                    manuver = verb.maneuver.defName.ToLower();
                }
                /*
                if (tooltype.Contains("hilt") || tooltype.Contains("pommel") || tooltype.Contains("handle"))
                {
                    atkType = 0;
                    addAngle = -95f;
                    addX = 0.1f;
                    addZ = 0.1f;
                }
                if (tooltype.Contains("point") || tooltype.Contains("tip") || manuver.Contains("stab"))
                {
                    atkType = 0;
                    addAngle = 25f;
                    addX += 0.1f;
                    addZ = 0.1f;
                }
                */
                switch (atkType)
                {
                    // 낮을수록 위로, 높을수록 아래로 휘두름 
                    // The lower the swing, the higher the swing (Translations weird, not sure its correct)
                    default:
                        // Normal Attack
                        //  addZ = 0.25f;//
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
                        if ((a - pawn.DrawPos).MagnitudeHorizontalSquared() > 0.001f)
                        {
                            num = (a - pawn.DrawPos).AngleFlat();
                        }
                        addAngle += 20f;
                        atktype = "Normal Attack";
                        break;
                    case 1:
                        // Low Attack
                        addAngle += 35f;
                        addZ += -0.25f;//
                        atktype = "Low Attack";
                        break;
                    case 2:
                        // 머리찌르기 
                        // High Attack
                        //  addAngle = isSub ? (pawn.Rotation == Rot4.West ? -25f : 25f) : -25f;
                        addZ += 0.35f;//
                        addAngle += -25f;
                        atktype = "High Attack";
                        break;
                }

                //if (log) Log.Message("C");
                // 원거리 무기일경우 각도보정 // Angle correction for ranged weapons
                if (weapon.def.IsRangedWeapon)
                {
                    addAngle -= isSub ? -35f : 35f;
                }
                SimpleCurve
                    curveAxisZ = new SimpleCurve
                    {
                    {
                        new CurvePoint(0.1f, 0f),
                        true
                    },
                    {
                        new CurvePoint(0.5f, addZ * 0.5f),
                        true
                    },
                    {
                        new CurvePoint(0.75f, addZ),
                        true
                    }
                    };
                SimpleCurve
                    curveAxisX = new SimpleCurve
                    {
                    {
                        new CurvePoint(0.1f, 0f),
                        true
                    },
                    {
                        new CurvePoint(0.5f, addX * 0.5f),
                        true
                    },
                    {
                        new CurvePoint(0.75f, addX),
                        true
                    }
                    };
                SimpleCurve
                    curveRotation = new SimpleCurve
                    {
                    {
                        new CurvePoint(0.1f, 0f),
                        true
                    },
                    {
                        new CurvePoint(0.5f, addAngle * 0.5f),
                        true
                    },
                    {
                        new CurvePoint(0.75f, addAngle),
                        true
                    }
                    };

                if ((float)ticksSinceLastShot < baseCooldown)
                {
                    float time = Mathf.InverseLerp(0, baseCooldown, stance_Busy.ticksLeft);
                    addX = curveAxisX.Evaluate(time);
                    addZ = curveAxisZ.Evaluate(time);
                    addAngle = curveRotation.Evaluate(time);
                    if (pawn.Rotation == Rot4.West)
                    {
                        addAngle = -addAngle;
                        addX = -addX;
                    }
                    drawOffset = new Vector3(addX, 0f, addZ);
                    angleOffset = addAngle;
                    aimAngle += angleOffset;
                    if (log) Log.Message(verb.tool.LabelCap + "'s " + verb.maneuver.defName + " " + atktype + " Anim: " + time + " LastShot: " + ticksSinceLastShot + " = baseCooldown: " + baseCooldown + " - ticksLeft: " + stance_Busy.ticksLeft + " addAngle: " + addAngle+ string.Format(" drawOffset: {0}, angleOffset: {1}", drawOffset, angleOffset));

                }
            }

        }

        public static void AnimationOffsetsIdle( Pawn pawn, Thing weapon, Vector3 offset, bool useTwirl, out Vector3 drawOffset, out float angleOffset, float aimAngle, bool isSub = false, bool log = false)
        {
            drawOffset = Vector3.zero;
            angleOffset = 0f;
            int tick = Mathf.Abs(pawn.HashOffsetTicks() % 1000000000);
            tick = tick % 100000000;
            tick = tick % 10000000;
            tick = tick % 1000000;

            tick = tick % 100000;
            tick = tick % 10000;
            tick = tick % 1000;
            float wiggle = 0f;
            if (!isSub)
            {
                wiggle = Mathf.Sin((float)tick * 0.05f);
            }
            else
            {
                wiggle = Mathf.Sin((float)tick * 0.05f + 1.5f);
            }
            float aniAngle = -5f;


            if (useTwirl)
            {
                if (!isSub)
                {
                    if (tick < 80 && tick >= 40)
                    {
                        angleOffset += (float)tick * 36f;
                        drawOffset += new Vector3(-0.2f, 0f, 0.1f);
                    }
                }
                else
                {
                    if (tick < 40)
                    {
                        angleOffset += (float)(tick - 40) * -36f;
                        drawOffset += new Vector3(0.2f, 0f, 0.1f);
                    }
                }
            }
            float angle = aimAngle;
            if (pawn.Rotation == Rot4.South)
            {
                drawOffset = new Vector3(0f, offset.z, wiggle * 0.05f);
                if (isSub)
                {
                    aniAngle *= -1f;
                }
                drawOffset.y += 0.03787879f;
            }
            if (pawn.Rotation == Rot4.North)
            {
                drawOffset = new Vector3(0f, offset.z, wiggle * 0.05f);
                if (isSub)
                {
                    aniAngle *= -1f;
                }
            }
            if (pawn.Rotation == Rot4.East)
            {
                drawOffset = new Vector3(0.2f, offset.z, wiggle * 0.05f);
                drawOffset.y += 0.03787879f;
            }
            if (pawn.Rotation == Rot4.West)
            {
                angle = 360 - aimAngle;
                drawOffset = new Vector3(-0.2f, offset.z, wiggle * 0.05f);
                drawOffset.y += 0.03787879f;
            }
            angleOffset = (angleOffset + angle + wiggle * aniAngle) - angle;
            drawOffset = drawOffset.RotatedBy(angleOffset);
            if (log) Log.Message(string.Format("Waiting drawOffset: {0}, angleOffset: {1}, aimAngle: {2}", drawOffset, angleOffset, angle));

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
            Graphic_StackCount graphic_StackCount = eq.Graphic as Graphic_StackCount;
            Material matSingle;
            if (graphic_StackCount != null)
            {
                matSingle = graphic_StackCount.SubGraphicForStackCount(1, eq.def).MatSingle;
            }
            else
            {
                matSingle = eq.Graphic.MatSingle;
            }
            Graphics.DrawMesh(mesh, drawLoc, Quaternion.AngleAxis(num, Vector3.up), matSingle, 0);
        }

        internal static void SetAnglesAndOffsets(Thing eq, ThingWithComps offHandEquip, float aimAngle, Pawn pawn, ref Vector3 offsetMainHand, ref Vector3 offsetOffHand, ref float mainHandAngle, ref float offHandAngle, bool mainHandAiming, bool offHandAiming)
        {
            bool offHandIsMelee = IsMeleeWeapon(offHandEquip);
            bool mainHandIsMelee = IsMeleeWeapon(pawn.equipment.Primary);
            float meleeAngleFlipped = Base.meleeMirrored ? 360 - Base.meleeAngle : Base.meleeAngle;
            float rangedAngleFlipped = Base.rangedMirrored ? 360 - Base.rangedAngle : Base.rangedAngle;
            bool flipMeleeZOffset = true;

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
                    offsetOffHand.x = offHandIsMelee ?  -Base.meleeXOffset : -Base.rangedXOffset;
                    offsetMainHand.z = mainHandIsMelee ? flipMeleeZOffset ? -Base.meleeZOffset : Base.meleeZOffset : Base.rangedZOffset;
                    offsetOffHand.z = offHandIsMelee ? -Base.meleeZOffset : -Base.rangedZOffset;
                    offHandAngle = offHandIsMelee ? Base.meleeAngle : Base.rangedAngle;
                    mainHandAngle = mainHandIsMelee ? meleeAngleFlipped : rangedAngleFlipped;

                }
                else
                {
                    offsetMainHand.x = mainHandIsMelee ? Base.meleeXOffset : Base.rangedXOffset;
                    offsetOffHand.x = offHandIsMelee ? -Base.meleeXOffset : -Base.rangedXOffset;
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
                    offsetOffHand.z = offHandIsMelee ? flipMeleeZOffset ? -Base.meleeZOffset : Base.meleeZOffset : Base.rangedZOffset;
                    offHandAngle = offHandIsMelee ? meleeAngleFlipped : rangedAngleFlipped;
                    mainHandAngle = mainHandIsMelee ? Base.meleeAngle : Base.rangedAngle;
                }
                else
                {
                    offsetMainHand.x = mainHandIsMelee ? -Base.meleeXOffset * 0.5f : -Base.rangedXOffset;
                    offsetOffHand.x = offHandIsMelee ? Base.meleeXOffset * 0.5f : Base.rangedXOffset;
                }
            }
            /*
            offsetMainHand.x += MainhandXTweak;
            offsetMainHand.z += MainhandZTweak;
            mainHandAngle += MainhandRotTweak;
            offsetOffHand.x += OffhandXTweak;
            offsetOffHand.z += OffhandZTweak;
            offHandAngle += OffhandRotTweak;
            */
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

        internal static float GetAimingRotation(Vector3 pos, LocalTargetInfo focusTarg)
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
            if ((a - pos).MagnitudeHorizontalSquared() > 0.001f)
            {
                num = (a - pos).AngleFlat();
            }

            return num;
        }
        internal static float GetAimingRotation(Pawn pawn, LocalTargetInfo focusTarg)
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
        internal static bool CurrentlyAiming(Stance_Busy stance)
        {
            return (stance != null && !stance.neverAimWeapon && stance.focusTarg.IsValid);
        }
        internal static bool IsMeleeWeapon(ThingWithComps eq)
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
