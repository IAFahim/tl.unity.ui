using Unity.Collections;
using Unity.Entities;
using Unity.Properties;
using UnityEngine;

namespace CardsSample
{
    [GeneratePropertyBag]
    public struct Card
    {
        public FixedString32Bytes Name;
        public int Cost;
        public int Power;
        public Color Tint;
    }

    public struct CardInDeck : IBufferElementData
    {
        public Card Card;
    }

    [GeneratePropertyBag]
    public struct CardInHand : IBufferElementData
    {
        public Card Card;
    }

    [GeneratePropertyBag]
    public struct CardOnTable : IBufferElementData
    {
        public Card Card;
    }

    [GeneratePropertyBag]
    public struct CardScore : IComponentData
    {
        public int Energy;
        public int EnergyPerTurn;
        public int Score;
        public int CardsInDeck;
        public int Turn;
    }

    public struct CardRules : IComponentData
    {
        public int HandSize;
        public int MostCardsInHand;
        public int EnergyPerTurn;
        public int DrawCode;
        public int EndTurnCode;
        public int PlayCode;
        public int ReturnCode;
        public int ReorderCode;
        public byte NotEnoughEnergySignal;
        public Entity Signals;
    }
}
