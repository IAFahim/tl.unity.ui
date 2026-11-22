using Tl;
using Tl.Ui;
using Tl.Unity;
using TlUi;
using Unity.Burst;
using Unity.Entities;

namespace CardsSample
{
    [BurstCompile]
    public partial struct CardTableSystem : ISystem
    {
        static bool Draw(DynamicBuffer<CardInDeck> deck, DynamicBuffer<CardInHand> hand, int mostCardsInHand)
        {
            if (deck.Length == 0 || hand.Length >= mostCardsInHand)
                return false;
            hand.Add(new CardInHand { Card = deck[0].Card });
            deck.RemoveAt(0);
            return true;
        }

        static void DrawUpTo(DynamicBuffer<CardInDeck> deck, DynamicBuffer<CardInHand> hand, int handSize, int mostCardsInHand)
        {
            while (hand.Length < handSize && Draw(deck, hand, mostCardsInHand))
            {
            }
        }

        static int WhereToPut(int droppedOnRow, int rowCount) => droppedOnRow >= 0 && droppedOnRow <= rowCount ? droppedOnRow : rowCount;

        static bool Play(ref CardScore score, DynamicBuffer<CardInHand> hand, DynamicBuffer<CardOnTable> table, int handRow, int droppedOnTableRow)
        {
            var card = hand[handRow].Card;
            if (card.Cost > score.Energy)
                return false;
            score.Energy -= card.Cost;
            score.Score += card.Power;
            hand.RemoveAt(handRow);
            table.Insert(WhereToPut(droppedOnTableRow, table.Length), new CardOnTable { Card = card });
            return true;
        }

        static void TakeBack(ref CardScore score, DynamicBuffer<CardOnTable> table, DynamicBuffer<CardInHand> hand, int tableRow, int droppedOnHandRow, int mostCardsInHand)
        {
            if (hand.Length >= mostCardsInHand)
                return;
            var card = table[tableRow].Card;
            score.Energy += card.Cost;
            score.Score -= card.Power;
            table.RemoveAt(tableRow);
            hand.Insert(WhereToPut(droppedOnHandRow, hand.Length), new CardInHand { Card = card });
        }

        static void MoveInHand(DynamicBuffer<CardInHand> hand, int fromRow, int toRow)
        {
            var card = hand[fromRow];
            hand.RemoveAt(fromRow);
            hand.Insert(WhereToPut(toRow, hand.Length), card);
        }

        static void EndTurn(ref CardScore score, in CardRules rules, DynamicBuffer<CardInDeck> deck, DynamicBuffer<CardInHand> hand, DynamicBuffer<CardOnTable> table)
        {
            table.Clear();
            score.Turn++;
            score.Energy = rules.EnergyPerTurn;
            DrawUpTo(deck, hand, rules.HandSize, rules.MostCardsInHand);
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<CardRules>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (rules, score, deck, hand, table, inbox) in SystemAPI.Query<RefRO<CardRules>, RefRW<CardScore>, DynamicBuffer<CardInDeck>, DynamicBuffer<CardInHand>, DynamicBuffer<CardOnTable>, DynamicBuffer<UiCommandInbox>>())
            {
                if (score.ValueRO.Turn == 0)
                    EndTurn(ref score.ValueRW, rules.ValueRO, deck, hand, table);
                for (var index = 0; index < inbox.Length; index++)
                {
                    var command = inbox[index];
                    var handRow = command.Payload >= 0 && command.Payload < hand.Length;
                    var tableRow = command.Payload >= 0 && command.Payload < table.Length;
                    if (command.Code == rules.ValueRO.DrawCode)
                        Draw(deck, hand, rules.ValueRO.MostCardsInHand);
                    else if (command.Code == rules.ValueRO.EndTurnCode)
                        EndTurn(ref score.ValueRW, rules.ValueRO, deck, hand, table);
                    else if (command.Code == rules.ValueRO.PlayCode && handRow && !Play(ref score.ValueRW, hand, table, command.Payload, command.Target))
                        SystemAPI.GetBuffer<TimelineEvent<UiSignal>>(rules.ValueRO.Signals).Raise(rules.ValueRO.NotEnoughEnergySignal);
                    else if (command.Code == rules.ValueRO.ReturnCode && tableRow)
                        TakeBack(ref score.ValueRW, table, hand, command.Payload, command.Target, rules.ValueRO.MostCardsInHand);
                    else if (command.Code == rules.ValueRO.ReorderCode && handRow)
                        MoveInHand(hand, command.Payload, command.Target);
                }
                inbox.Clear();
                score.ValueRW.CardsInDeck = deck.Length;
            }
        }
    }
}
