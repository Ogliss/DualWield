using RimWorld;
using System;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DualWield
{
    public class FloatMenuOptionProvider_DropOffhandEquipment : FloatMenuOptionProvider
    {
        public override bool Drafted
        {
            get
            {
                return true;
            }
        }

        public override bool Undrafted
        {
            get
            {
                return true;
            }
        }

        public override bool Multiselect
        {
            get
            {
                return false;
            }
        }

        public override bool CanSelfTarget
        {
            get
            {
                return true;
            }
        }

        public override bool AppliesInt(FloatMenuContext context)
        {
            Pawn_EquipmentTracker equipment = context.FirstSelectedPawn.equipment;
            if (equipment == null && equipment.TryGetOffHandEquipment(out ThingWithComps eq))
            {
                return true;
            }
            return false;
        }

        public override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (clickedPawn != context.FirstSelectedPawn)
            {
                return null;
            }
            if (clickedPawn.equipment.TryGetOffHandEquipment(out ThingWithComps eq))
            {
                if (clickedPawn.IsQuestLodger() && !EquipmentUtility.QuestLodgerCanUnequip(eq, clickedPawn))
                {
                    return new FloatMenuOption("CannotDrop".Translate(eq.Label, eq) + ": " + "QuestRelated".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                }
                Action action = delegate ()
                {
                    clickedPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.DropEquipment, eq), new JobTag?(JobTag.Misc), false);
                };
                return new FloatMenuOption("DW_DropOffHand".Translate(eq.LabelShort, eq), action, eq, Color.white, MenuOptionPriority.Default, null, clickedPawn, 0f, null, null, true, 0);
            }
            return null;
        }
    }
}
