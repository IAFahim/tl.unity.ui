using System.Collections.Generic;
using Tl;
using Tl.Ui;
using Tl.Unity;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace CardsSample
{
    [AddComponentMenu("Tl UI Samples/Cards")]
    public sealed class CardsAuthoring : MonoBehaviour
    {
        [Tooltip("The deck: which cards, their cost, power, colour and copies.")]
        public CardSet Deck;

        [Tooltip("Shuffles the deck with this seed. 0 keeps the Card Set's order.")]
        public int ShuffleSeed = 7;

        [Tooltip("Cards dealt at the start, and drawn up to at the end of every turn.")]
        [Min(0)]
        public int HandSize = 5;

        [Tooltip("The hand never holds more than this; Draw does nothing then.")]
        [Min(1)]
        public int MostCardsInHand = 8;

        [Tooltip("Energy at the start of every turn. Playing a card spends its cost.")]
        [Min(0)]
        public int EnergyPerTurn = 3;

        [Header("Codes the Tl UI Panel sends")]
        [Tooltip("Draw button (a Commands row): draw one card.")]
        public int DrawCode = 1;

        [Tooltip("End Turn button (a Commands row): clear the table, refill energy, draw up to Hand Size.")]
        public int EndTurnCode = 2;

        [Tooltip("Drop Code of the table list: play the hand card dropped on the table.")]
        public int PlayCode = 3;

        [Tooltip("Drop Code of the hand list: take the table card dropped on the hand back.")]
        public int ReturnCode = 4;

        [Tooltip("Drag Code of the hand list: move the dragged card to where it was dropped.")]
        public int ReorderCode = 5;

        [Tooltip("Signal code raised when a card costs more energy than is left. The Tl UI Panel's Signals table says what it plays.")]
        [Range(0, 255)]
        public int NotEnoughEnergySignal = 1;

        sealed class Baker : Baker<CardsAuthoring>
        {
            public override void Bake(CardsAuthoring authoring)
            {
                var table = GetEntity(TransformUsageFlags.None);
                var deck = AddBuffer<CardInDeck>(table);
                if (authoring.Deck == null)
                {
                    Debug.LogError("Cards: assign the Deck, a Card Set asset.", authoring);
                }
                else
                {
                    DependsOn(authoring.Deck);
                    var cards = new List<CardInDeck>();
                    foreach (var definition in authoring.Deck.Cards)
                        for (var copy = 0; copy < Mathf.Max(1, definition.Copies); copy++)
                            cards.Add(new CardInDeck { Card = new Card { Name = new FixedString32Bytes(definition.Name ?? ""), Cost = definition.Cost, Power = definition.Power, Tint = definition.Tint } });
                    if (authoring.ShuffleSeed != 0)
                    {
                        var random = new System.Random(authoring.ShuffleSeed);
                        for (var last = cards.Count - 1; last > 0; last--)
                        {
                            var swapWith = random.Next(last + 1);
                            (cards[last], cards[swapWith]) = (cards[swapWith], cards[last]);
                        }
                    }
                    foreach (var card in cards)
                        deck.Add(card);
                }
                AddBuffer<CardInHand>(table);
                AddBuffer<CardOnTable>(table);
                AddComponent(table, new CardScore { EnergyPerTurn = authoring.EnergyPerTurn });
                var signals = CreateAdditionalEntity(TransformUsageFlags.None, entityName: "Card Signals");
                AddBuffer<TimelineEvent<UiSignal>>(signals);
                AddComponent(table, new CardRules
                {
                    HandSize = authoring.HandSize,
                    MostCardsInHand = authoring.MostCardsInHand,
                    EnergyPerTurn = authoring.EnergyPerTurn,
                    DrawCode = authoring.DrawCode,
                    EndTurnCode = authoring.EndTurnCode,
                    PlayCode = authoring.PlayCode,
                    ReturnCode = authoring.ReturnCode,
                    ReorderCode = authoring.ReorderCode,
                    NotEnoughEnergySignal = (byte)authoring.NotEnoughEnergySignal,
                    Signals = signals,
                });
            }
        }
    }
}
