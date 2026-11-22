#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using CardsSample;
using NUnit.Framework;
using TlUi;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CardsSample.Tests
{
    public class CardsTests
    {
        const int HandList = 0;
        const int TableList = 1;

        static IEnumerator Frames(int count)
        {
            for (var frame = 0; frame < count; frame++)
                yield return null;
        }

        static void SendPointer<TEvent>(VisualElement target, EventType mouseEvent, Vector2 panelPosition) where TEvent : PointerEventBase<TEvent>, new()
        {
            using var pointer = PointerEventBase<TEvent>.GetPooled(new Event { type = mouseEvent, mousePosition = panelPosition, button = 0 });
            pointer.target = target;
            target.SendEvent(pointer);
        }

        static void Click(VisualElement target)
        {
            using var click = ClickEvent.GetPooled();
            click.target = target;
            target.SendEvent(click);
        }

        static IEnumerator Seconds(float seconds)
        {
            var until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
                yield return null;
        }

        static VisualElement[] ShownRows(TlUiPanel uiPanel, int list) => uiPanel.RowElementsOf(list).Where(row => row.resolvedStyle.display != DisplayStyle.None).ToArray();

        static string NameOn(VisualElement row) => row.Q<Label>("cardName").text;

        static CardInHand[] Hand(EntityManager entities, Entity table) => entities.GetBuffer<CardInHand>(table, true).AsNativeArray().ToArray();

        static CardOnTable[] Table(EntityManager entities, Entity table) => entities.GetBuffer<CardOnTable>(table, true).AsNativeArray().ToArray();

        static IEnumerator DragAndHoverCheck(VisualElement row, Vector2 onto, System.Action<VisualElement> whileLifted)
        {
            var from = row.worldBound.center;
            SendPointer<PointerDownEvent>(row, EventType.MouseDown, from);
            SendPointer<PointerMoveEvent>(row, EventType.MouseDrag, Vector2.Lerp(from, onto, 0.5f));
            SendPointer<PointerMoveEvent>(row, EventType.MouseDrag, onto);
            whileLifted?.Invoke(row);
            SendPointer<PointerUpEvent>(row, EventType.MouseUp, onto);
            yield return Frames(6);
        }

        [UnityTest]
        public IEnumerator DraggingACardOntoTheTablePlaysItAndBackTakesItBackWhileTheRulesStayInBurst()
        {
            var scenePath = EditorBuildSettings.scenes.Select(scene => scene.path).FirstOrDefault(path => path.EndsWith("/Cards.unity") && System.IO.File.Exists(path));
            Assert.IsNotNull(scenePath, "Run Tl UI > Samples > Build Cards first.");
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));

            var uiPanel = Object.FindAnyObjectByType<TlUiPanel>();
            var entities = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var tableQuery = entities.CreateEntityQuery(ComponentType.ReadOnly<CardRules>(), ComponentType.ReadOnly<CardInHand>());
            var loadDeadline = Time.realtimeSinceStartup + 120f;
            while ((tableQuery.IsEmpty || uiPanel.RowElementsOf(HandList) == null || ShownRows(uiPanel, HandList).Length < 5) && Time.realtimeSinceStartup < loadDeadline)
                yield return null;
            yield return Seconds(1.5f);
            var table = tableQuery.GetSingletonEntity();
            var rules = entities.GetComponentData<CardRules>(table);
            var hand = Hand(entities, table);
            Assert.AreEqual(rules.HandSize, hand.Length, "the opening hand is dealt by the Burst system");
            var handRows = ShownRows(uiPanel, HandList);
            Assert.AreEqual(hand.Length, handRows.Length, "one card element per card in the hand buffer");
            Assert.AreEqual(hand[0].Card.Name.ToString(), NameOn(handRows[0]), "the card shows its name through tl-fixed-text");
            var tint = handRows[0].Q("card").resolvedStyle.backgroundColor;
            Assert.AreEqual(hand[0].Card.Tint.r, tint.r, 0.01f, "the card's colour comes from the Card Set through a style binding");
            Assert.AreEqual(hand[0].Card.Tint.g, tint.g, 0.01f);
            var energy = uiPanel.Root.Q<Label>("energyValue");
            Assert.AreEqual(rules.EnergyPerTurn.ToString(), energy.text, "the energy label shows the Card Score singleton");

            var playable = System.Array.FindIndex(hand, card => card.Card.Cost <= rules.EnergyPerTurn && card.Card.Cost > 0);
            Assert.GreaterOrEqual(playable, 0, "the seeded deck deals a card that costs energy and fits this turn");
            var played = hand[playable].Card;
            var tableArea = uiPanel.Root.Q("table");
            var ghostShown = false;
            var tableLit = false;
            yield return DragAndHoverCheck(handRows[playable], tableArea.worldBound.center, row =>
            {
                var ghost = uiPanel.Root.Q("tl-drag-ghost");
                ghostShown = ghost != null && ghost.style.display.value == DisplayStyle.Flex && ghost.hierarchy.parent == uiPanel.Root;
                tableLit = tableArea.ClassListContains(TlRowDrag.DropTargetClass);
                Assert.IsTrue(row.ClassListContains(TlRowDrag.DraggingClass), "the card left behind is marked tl-dragging, so USS can dim it");
            });
            Assert.IsTrue(ghostShown, "a ghost of the card follows the pointer on top of the screen");
            Assert.IsTrue(tableLit, "the table lights up as a drop target while the card is over it");
            Assert.AreEqual(DisplayStyle.None, uiPanel.Root.Q("tl-drag-ghost").style.display.value, "the ghost hides after the drop");
            Assert.AreEqual(1, Table(entities, table).Length, "dropping on the table sends the table's Drop Code, and the Burst system plays the card");
            Assert.AreEqual(played.Name, Table(entities, table)[0].Card.Name);
            Assert.AreEqual(rules.HandSize - 1, Hand(entities, table).Length);
            var score = entities.GetComponentData<CardScore>(table);
            Assert.AreEqual(rules.EnergyPerTurn - played.Cost, score.Energy, "playing spends the card's cost");
            Assert.AreEqual(played.Power, score.Score, "and adds its power");
            Assert.AreEqual(score.Energy.ToString(), energy.text);
            Assert.AreEqual(played.Name.ToString(), NameOn(ShownRows(uiPanel, TableList)[0]), "the table list shows the played card");

            var handTarget = ShownRows(uiPanel, HandList)[0];
            yield return DragAndHoverCheck(ShownRows(uiPanel, TableList)[0], handTarget.worldBound.center, null);
            Assert.AreEqual(0, Table(entities, table).Length, "dragging a table card onto the hand takes it back");
            Assert.AreEqual(rules.HandSize, Hand(entities, table).Length);
            Assert.AreEqual(played.Name, Hand(entities, table)[0].Card.Name, "it lands where it was dropped, at the front");
            Assert.AreEqual(rules.EnergyPerTurn, entities.GetComponentData<CardScore>(table).Energy, "and its energy comes back");

            var firstName = Hand(entities, table)[0].Card.Name;
            handRows = ShownRows(uiPanel, HandList);
            yield return DragAndHoverCheck(handRows[0], handRows[2].worldBound.center, null);
            Assert.AreEqual(firstName, Hand(entities, table)[2].Card.Name, "dropping a hand card on another hand card moves it there (the hand's Drag Code)");

            var broke = entities.GetComponentData<CardScore>(table);
            broke.Energy = 0;
            entities.SetComponentData(table, broke);
            hand = Hand(entities, table);
            var tooDear = System.Array.FindIndex(hand, card => card.Card.Cost > 0);
            var energyBox = uiPanel.Root.Q("energy");
            yield return DragAndHoverCheck(ShownRows(uiPanel, HandList)[tooDear], tableArea.worldBound.center, null);
            Assert.AreEqual(0, Table(entities, table).Length, "with no energy left the card stays in the hand");
            var warned = false;
            var shook = false;
            var warnDeadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < warnDeadline && !(warned && shook))
            {
                warned |= energyBox.ClassListContains("warning");
                shook |= Mathf.Abs(energyBox.resolvedStyle.translate.x) > 1f;
                yield return null;
            }
            Assert.IsTrue(warned, "the refused play raises the Not Enough Energy signal, whose timeline adds the warning class");
            Assert.IsTrue(shook, "and shakes the energy box with an Animate Move X clip");

            var handCount = Hand(entities, table).Length;
            Click(uiPanel.Root.Q<Button>("draw"));
            yield return Frames(6);
            Assert.AreEqual(handCount + 1, Hand(entities, table).Length, "Draw sends the draw code through a Commands row");

            var banner = uiPanel.Root.Q<Label>("banner");
            Click(uiPanel.Root.Q<Button>("endTurn"));
            var bannerShown = false;
            var bannerDeadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < bannerDeadline && !bannerShown)
            {
                bannerShown = banner.resolvedStyle.display == DisplayStyle.Flex;
                yield return null;
            }
            Assert.IsTrue(bannerShown, "End Turn also plays the New Turn timeline, which shows the banner");
            Assert.AreEqual(2, entities.GetComponentData<CardScore>(table).Turn, "and the Burst system starts turn 2");
            Assert.AreEqual(rules.EnergyPerTurn, entities.GetComponentData<CardScore>(table).Energy, "with full energy");
        }
    }
}
#endif
