using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace DualWield.Stances
{
    class Stance_Cooldown_DW : Stance_Cooldown
    {
        public override bool StanceBusy
        {
            get
            {
                return true;
            }
        }
        public Stance_Cooldown_DW()
        {
        }
        public Stance_Cooldown_DW(int ticks, LocalTargetInfo focusTarg, Verb verb) : base(ticks, focusTarg, verb)
        {
        }
    }
}
