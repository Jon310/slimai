﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Styx;
using Styx.Common;
using Styx.TreeSharp;
using System.Drawing;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Helpers
{
    public static class CoroutineBuilder
    {
        /// <summary>
        /// allows generic behaviors to query current type of behavior
        /// during behavior construction
        /// </summary>
        public static BehaviorType CurrentBehaviorType { get; set; }
        public static bool SilentBehaviorCreation { get; set; }


        private static List<MethodInfo> _methods = new List<MethodInfo>();

        public static async Task<Task<PrioritySelector>> GetCoroutine(WoWClass wowClass, WoWSpec spec, BehaviorType behavior, WoWContext context, int behaviourCount, bool silent = false)
        {
            if (context == WoWContext.None)
            {
                // None is an invalid context, but rather than stopping bot wait it out with donothing logic
                Logging.Write("No Active Context -{0}{1} for{2} set to DoNothingBehavior temporarily", wowClass.ToString().CamelToSpaced(), behavior.ToString().CamelToSpaced(), spec.ToString().CamelToSpaced());
                behaviourCount = 1;
                return null;
            }

            SilentBehaviorCreation = silent;
            behaviourCount = 0;
            if (_methods.Count <= 0)
            {
                foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
                {
                    // All behavior methods should not be generic, and should have zero parameters, with their return types being of type Composite.
                    _methods.AddRange(
                        type.GetMethods(BindingFlags.Static | BindingFlags.Public).Where(
                            mi => !mi.IsGenericMethod && mi.GetParameters().Length == 0).Where(
                                mi => mi.ReturnType.IsAssignableFrom(typeof (Coroutine))));
                }
                Logging.Write("SlimAI Behaviors: Added " + _methods.Count + " behaviors");
            }

            var matchedMethods = new Dictionary<BehaviorAttribute, Coroutine>();

            foreach (MethodInfo mi in _methods)
            {
                // If the behavior is set as ignore. Don't use it? Duh?
                if (mi.GetCustomAttributes(typeof(IgnoreBehaviorCountAttribute), false).Any())
                    continue;

                // If there's no behavior attrib, then move along.
                foreach (var a in mi.GetCustomAttributes(typeof(BehaviorAttribute), false))
                {
                    var attribute = a as BehaviorAttribute;
                    if (attribute == null)
                        continue;

                    // Check if our behavior matches with what we want. If not, don't add it!
                    if (IsMatchingMethod(attribute, wowClass, spec, behavior, context))
                    {
                        if (!silent)
                            Logging.Write("{0} {1} {2}", attribute.PriorityLevel.ToString().AlignRight(4), behavior.ToString().AlignLeft(15), mi.Name);

                        CurrentBehaviorType = behavior;

                        // if it blows up here, you defined a method with the exact same attribute and priority as one already found

                        // wrap in trace class
                        var comp = mi.Invoke(null, null) as Coroutine;
                        var name = behavior.ToString() + "." + mi.Name + "." + attribute.PriorityLevel.ToString();

                        //if (SlimAI.Trace)
                        //    comp = new CallTrace( name, comp);

                        matchedMethods.Add(attribute, comp);

                        CurrentBehaviorType = 0;
                    }
                }
            }
            // If we found no methods, rofls!
            if (matchedMethods.Count <= 0)
            {
                return null;
            }

            var result = Coroutine.ExternalTask(Task.Run(() =>new PrioritySelector()));
            foreach (var kvp in matchedMethods.OrderByDescending(mm => mm.Key.PriorityLevel))
            {
                result.Equals(kvp.Value);
                behaviourCount++;
            }

            return result;
        }

        private static bool IsMatchingMethod(BehaviorAttribute attribute, WoWClass wowClass, WoWSpec spec, BehaviorType behavior, WoWContext context)
        {
            if (attribute.SpecificClass != wowClass && attribute.SpecificClass != WoWClass.None)
                return false;
            if ((attribute.Type & behavior) == 0)
                return false;
            if ((attribute.SpecificContext & context) == 0)
                return false;
            if (attribute.SpecificSpec != (WoWSpec)int.MaxValue && attribute.SpecificSpec != spec)
                return false;

            /* Logger.WriteDebug("IsMatchingMethod({0}, {1}, {2}, {3}) - {4}, {5}, {6}, {7}, {8}", wowClass, spec, behavior,
                context, attribute.SpecificClass, attribute.SpecificSpec, attribute.Type, attribute.SpecificContext,
                attribute.PriorityLevel);  */
            return true;
        }

        public static class NoContextAvailable
        {
            public static Composite CreateDoNothingBehavior()
            {
                return new Throttle(15,
                    new Action(r => Logging.Write("No Context Available - do nothing while we wait"))
                    );
            }
        }

        [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
        internal sealed class IgnoreBehaviorCountAttribute : Attribute
        {
            public IgnoreBehaviorCountAttribute(BehaviorType type)
            {
                Type = type;
            }

            public BehaviorType Type { get; private set; }
        }
    }
}