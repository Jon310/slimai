﻿using System.Linq;
using CommonBehaviors.Actions;
using SlimAI;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Inventory;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using System;
using Action = Styx.TreeSharp.Action;
using System.Collections.Generic;
using System.Drawing;

namespace SlimAI.Helpers
{
    internal static class Death
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }

        const int RezMaxMobsNear = 0;
        const int RezWaitTime = 10;
        const int RezWaitDist = 20;

        private static string SelfRezSpell { get; set; }
        private static int MobsNearby { get; set; }
        private static DateTime NextSuppressMessage = DateTime.MinValue;

        [Behavior(BehaviorType.Death)]
        public static Composite CreateDefaultDeathBehavior()
        {
            return new Throttle( 60,
                new Decorator(
                    req => {
                        if (Me.IsAlive || Me.IsGhost)
                            return false;

                        List<string> hasSoulstone = Lua.GetReturnValues("return HasSoulstone()", "hawker.lua");
                        if (hasSoulstone == null || hasSoulstone.Count == 0 || String.IsNullOrEmpty(hasSoulstone[0]) || hasSoulstone[0].ToLower() == "nil")
                            return false;

                        if (!SlimAI.AFK)
                        {
                            if (NextSuppressMessage < DateTime.Now)
                            {
                                NextSuppressMessage = DateTime.Now.AddSeconds(RezWaitTime);
                                Logging.Write("Suppressing {0} behavior since movement disabled...", hasSoulstone[0]);
                            }
                            return false;
                        }

                        SelfRezSpell = hasSoulstone[0];
                        return true;
                        },
                    new Sequence(
                        new Action( r => Logging.Write("Waiting up to {0} seconds for clear area to use {1}...", RezWaitTime, SelfRezSpell)),
                        new Wait( 
                            RezWaitTime, 
                            until => {
                                MobsNearby = Unit.UnfriendlyUnits(RezWaitDist).Count();
                                return MobsNearby <= RezMaxMobsNear || Me.IsAlive || Me.IsGhost;
                                },
                            new Action( r => {
                                if ( Me.IsGhost )
                                {
                                    Logging.Write("Insignia taken or corpse release by something other than SlimAI...");
                                    return RunStatus.Failure;
                                }

                                if ( Me.IsAlive)
                                {
                                    Logging.Write("Ressurected by something other than SlimAI...");
                                    return RunStatus.Failure;
                                }

                                return RunStatus.Success;
                                })
                            ),
                        new DecoratorContinue(
                            req => MobsNearby > RezMaxMobsNear,
                            new Action( r => {
                                Logging.Write("Still {0} enemies within {1} yds, skipping {2}", MobsNearby, RezWaitDist, SelfRezSpell);
                                return RunStatus.Failure;
                                })
                            ),

                        new Action(r => Logging.Write("Ressurrecting SlimAI by invoking {0}...", SelfRezSpell)),

                        new Action(r => Lua.DoString("UseSoulstone()")),

                        new WaitContinue( 1, until => Me.IsAlive || Me.IsGhost, new ActionAlwaysSucceed())
                        )
                    )
                );
        }

    }
}
