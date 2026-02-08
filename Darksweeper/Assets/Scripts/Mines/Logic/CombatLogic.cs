using System.Collections.Generic;
using Mines.Data;

namespace Mines.Logic
{
    /// <summary>
    /// Deterministic combat resolution.
    /// From the bible: player attacks first with Force (F), creature retaliates
    /// with remaining Force (Fc). Repeat until creature dies.
    /// Combat is always winnable if player has enough HP.
    /// The only question: "how many HP will this cost me?"
    /// </summary>
    public static class CombatLogic
    {
        /// <summary>
        /// Resolve a combat deterministically.
        /// </summary>
        /// <param name="playerForce">Player's Force stat (F)</param>
        /// <param name="creatureForce">Creature's Force (Fc) — also its HP</param>
        /// <param name="leftClickPenalty">If true, total damage is doubled (accident penalty)</param>
        /// <param name="currentHp">Player's current HP (to check if they die)</param>
        /// <returns>Full combat result with exchange breakdown</returns>
        public static ResolutionResult Resolve(int playerForce, int creatureForce, bool leftClickPenalty, int currentHp)
        {
            int fc = creatureForce;
            int totalDamage = 0;
            var exchanges = new List<CombatExchange>();
            int exchangeNum = 0;

            // Ensure playerForce is at least 1 to avoid infinite loop
            if (playerForce < 1) playerForce = 1;

            while (fc > 0)
            {
                exchangeNum++;

                // Player attacks first
                fc -= playerForce;
                int creatureHpAfter = fc > 0 ? fc : 0;
                int creatureDamage = 0;

                // Creature retaliates if still alive
                if (fc > 0)
                {
                    creatureDamage = fc;
                    totalDamage += creatureDamage;
                }

                exchanges.Add(new CombatExchange
                {
                    exchangeNumber = exchangeNum,
                    playerDamageDealt = playerForce,
                    creatureHpAfter = creatureHpAfter,
                    creatureDamageDealt = creatureDamage
                });
            }

            // Apply left-click penalty: double all damage
            if (leftClickPenalty)
            {
                totalDamage *= 2;
            }

            int hpAfter = currentHp - totalDamage;

            return new ResolutionResult
            {
                hpDelta = -totalDamage,
                reward = RewardType.None,   // reward is set by caller from CombatParams
                rewardValue = 0,
                resultText = leftClickPenalty
                    ? $"Attaque surprise ! Vous subissez des degats doubles.\nLa creature (Force {creatureForce}) est vaincue en {exchangeNum} echange(s).\nDegats totaux : {totalDamage} PV"
                    : $"La creature (Force {creatureForce}) est vaincue en {exchangeNum} echange(s).\nDegats subis : {totalDamage} PV",
                playerDied = hpAfter <= 0,
                exchanges = exchanges.ToArray()
            };
        }

        /// <summary>
        /// Preview the cost of a combat without resolving it.
        /// Useful for UI hints.
        /// </summary>
        public static int EstimateDamage(int playerForce, int creatureForce)
        {
            int fc = creatureForce;
            int totalDamage = 0;
            if (playerForce < 1) playerForce = 1;

            while (fc > 0)
            {
                fc -= playerForce;
                if (fc > 0)
                    totalDamage += fc;
            }

            return totalDamage;
        }
    }
}
