using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayerProfile
{
    /// <summary>
    /// Pure function: given Action/Empathy scores and the god list,
    /// determines the primary and secondary gods.
    /// </summary>
    public static class GodAssignment
    {
        /// <summary>
        /// Evaluates the player's scores and returns (primaryGod, secondaryGod).
        /// 
        /// Primary god = quadrant matching the score signs.
        /// Secondary god = adjacent quadrant (flip the weaker axis).
        /// </summary>
        public static (GodData primary, GodData secondary) Evaluate(
            int actionScore, int empathyScore, List<GodData> gods)
        {
            // Edge case: both scores are 0 → default to Archivist of the Void / Watcher of Echoes
            if (actionScore == 0 && empathyScore == 0)
            {
                GodData defaultPrimary = FindByQuadrantSigns(gods, -1, -1);
                GodData defaultSecondary = FindByQuadrantSigns(gods, -1, 1);
                Debug.Log("[GodAssignment] Both scores are 0. Defaulting to Archivist of the Void / Watcher of Echoes.");
                return (defaultPrimary, defaultSecondary);
            }

            // Determine the sign for each axis (0 is treated as negative)
            int actionSign = actionScore > 0 ? 1 : -1;
            int empathySign = empathyScore > 0 ? 1 : -1;

            // Primary god = matching quadrant
            GodData primary = FindByQuadrantSigns(gods, actionSign, empathySign);

            // Secondary god = flip the weaker axis
            // If tied (|action| == |empathy|), flip empathy by convention
            int flippedActionSign = actionSign;
            int flippedEmpathySign = empathySign;

            if (Math.Abs(actionScore) < Math.Abs(empathyScore))
            {
                // Action is the weaker axis → flip action
                flippedActionSign = -actionSign;
            }
            else
            {
                // Empathy is the weaker axis (or tied) → flip empathy
                flippedEmpathySign = -empathySign;
            }

            GodData secondary = FindByQuadrantSigns(gods, flippedActionSign, flippedEmpathySign);

            // Safety: if secondary ended up being the same as primary (shouldn't happen), log it
            if (primary != null && secondary != null && primary.Id == secondary.Id)
            {
                Debug.Log($"[GodAssignment] Warning: primary and secondary resolved to the same god ({primary.Name}).");
            }

            Debug.Log($"[GodAssignment] Action={actionScore}, Empathy={empathyScore} → Primary: {primary?.Name}, Secondary: {secondary?.Name}");

            return (primary, secondary);
        }

        private static GodData FindByQuadrantSigns(List<GodData> gods, int actionSign, int empathySign)
        {
            foreach (var god in gods)
            {
                if (god.ActionSign == actionSign && god.EmpathySign == empathySign)
                    return god;
            }

            Debug.Log($"[GodAssignment] No god found for actionSign={actionSign}, empathySign={empathySign}.");
            return null;
        }
    }
}
