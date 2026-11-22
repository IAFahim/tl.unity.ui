using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsSample
{
    [CreateAssetMenu(menuName = "Tl UI Samples/Card Set", fileName = "Card Set")]
    public sealed class CardSet : ScriptableObject
    {
        [Serializable]
        public struct CardDefinition
        {
            [Tooltip("The card's name, as the card shows it. Up to 29 characters.")]
            public string Name;

            [Tooltip("Energy it takes to play the card.")]
            [Min(0)]
            public int Cost;

            [Tooltip("Score the card adds when it is played.")]
            public int Power;

            [Tooltip("The card's background colour.")]
            public Color Tint;

            [Tooltip("How many copies of the card the deck holds.")]
            [Min(1)]
            public int Copies;
        }

        [Tooltip("Every card in the deck, in the order they are drawn when the shuffle seed is 0.")]
        public List<CardDefinition> Cards = new List<CardDefinition>();
    }
}
