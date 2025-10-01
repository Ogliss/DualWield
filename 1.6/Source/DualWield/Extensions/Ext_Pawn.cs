using DualWield.Stances;
using DualWield.Storage;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace DualWield
{
    public static class Ext_Pawn
    {
        public static Pawn_StanceTracker GetStancesOffHand(this Pawn instance)
        {
            if(Base.Instance.GetExtendedDataStorage() is ExtendedDataStorage store)
            {
                return store.GetExtendedDataFor(instance).stancesOffhand;
            }
            return null;
        }
        public static void SetStancesOffHand(this Pawn instance, Pawn_StanceTracker stancesOffHand)
        {
            if (Base.Instance.GetExtendedDataStorage() is ExtendedDataStorage store)
            {
                store.GetExtendedDataFor(instance).stancesOffhand = stancesOffHand;
            }
        }
        public static void TryStartOffHandAttack(this Pawn pawn, LocalTargetInfo targ, ref bool result)
        {
        //    Log.Message("try start offhand attack");
            if(pawn.equipment == null || !pawn.equipment.TryGetOffHandEquipment(out ThingWithComps offHandEquip))
            {
            //    Log.Message("no offhand equp");
                return;
            }
            var offhandStance = pawn.GetStancesOffHand();
            if (offhandStance.curStance is Stance_Warmup_DW || offhandStance.curStance is Stance_Cooldown)
            {
             //   Log.Message($"On cooldown or mobile {offhandStance.curStance}");
                return;
            }
            if (pawn.story != null && pawn.story.DisabledWorkTagsBackstoryAndTraits.HasFlag(WorkTags.Violent))
            {
            //    Log.Message("not violent");
                return;
            }
            if (pawn.jobs.curDriver.GetType().Name.Contains("Ability"))//Compatbility for Jecstools' abilities.
            {
             //   Log.Message("driver contains ability");
                return;
            }
        //    Log.Message("Cur offhand stance: " + __instance.GetStancesOffHand().curStance.GetType().Name);
            bool allowManualCastWeapons = !pawn.IsColonist;
            Verb verb = pawn.TryGetOffhandAttackVerb(targ.Thing, true);
            
            if (verb != null)
            {
                bool success = verb.OffhandTryStartCastOn(targ);
                result = result || (verb != null && success);
            }
        }
        public static Verb TryGetOffhandAttackVerb(this Pawn instance, Thing target, bool allowManualCastWeapons = false)
        {
            Pawn_EquipmentTracker equipment = instance.equipment;
            ThingWithComps offHandEquip = null;
            CompEquippable compEquippable = null;
            if (equipment != null && equipment.TryGetOffHandEquipment(out ThingWithComps result) && result != equipment.Primary)
            {
             //   Log.Message("returning offhand attack verb");
                offHandEquip = result;//TODO: replace this temp code.
                compEquippable = offHandEquip.TryGetComp<CompEquippable>();
            }
            if (compEquippable != null && compEquippable.PrimaryVerb.Available() && (!compEquippable.PrimaryVerb.verbProps.onlyManualCast || (instance.CurJob != null && instance.CurJob.def != JobDefOf.Wait_Combat) || allowManualCastWeapons))
            {
            //    Log.Message("returning primary verb");
                return compEquippable.PrimaryVerb;
            }
            else
            {
                return instance.TryGetMeleeVerbOffHand(target);
            }
        }
        public static bool HasMissingArmOrHand(this Pawn instance)
        {
            bool hasMissingHand = false;
            if (instance == null)
            {
                Log.Message($"Pawn null");
            }
            if (instance.health == null)
            {
                Log.Message($"{instance} health null");
            }
            if (instance.health.hediffSet == null)
            {
                Log.Message($"{instance} hediffSet null");
            }
            foreach (Hediff_MissingPart missingPart in instance.health.hediffSet.GetMissingPartsCommonAncestors())
            {
                Log.Message($"{instance} {missingPart.Label}");
                if (missingPart.Part.def == BodyPartDefOf.Hand || missingPart.Part.def == BodyPartDefOf.Arm)
                {
                    hasMissingHand = true;
                }
            }
            return hasMissingHand;
        }
        public static Verb TryGetMeleeVerbOffHand(this Pawn instance, Thing target)
        {

            List<VerbEntry> usableVerbs = new List<VerbEntry>();
            if (instance.equipment != null && instance.equipment.TryGetOffHandEquipment(out ThingWithComps offHandEquip))
            {              
                CompEquippable comp = offHandEquip.GetComp<CompEquippable>();
                //if(comp.AllVerbs.First((Verb verb) => verb.bu
                if (comp != null)
                {
                    List<Verb> allVerbs = comp.AllVerbs;
                    if (allVerbs != null)
                    {
                        for (int k = 0; k < allVerbs.Count; k++)
                        {
                            if (allVerbs[k].IsMeleeAttack && allVerbs[k].IsStillUsableBy(instance))
                            {
                                usableVerbs.Add(new VerbEntry(allVerbs[k], instance, comp.AllVerbs, allVerbs.Count));
                            }
                        }
                    }
                }           
            }
            if(usableVerbs.TryRandomElementByWeight((VerbEntry ve) => ve.GetSelectionWeight(target), out VerbEntry result))
            {
                return result.verb;
            }
            return null;       
        }

    }
}
