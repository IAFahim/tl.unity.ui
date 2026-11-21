#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using InventorySample;
using NUnit.Framework;
using TlUi;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace InventorySample.Tests
{
    public class InventoryTests
    {
        static IEnumerator Frames(int count)
        {
            for (var frame = 0; frame < count; frame++)
                yield return null;
        }

        static void Send<TEvent>(VisualElement target) where TEvent : EventBase<TEvent>, new()
        {
            using var sent = EventBase<TEvent>.GetPooled();
            sent.target = target;
            target.SendEvent(sent);
        }

        static void SendPointer<TEvent>(VisualElement target, EventType mouseEvent, Vector2 panelPosition) where TEvent : PointerEventBase<TEvent>, new()
        {
            using var pointer = PointerEventBase<TEvent>.GetPooled(new Event { type = mouseEvent, mousePosition = panelPosition, button = 0 });
            pointer.target = target;
            target.SendEvent(pointer);
        }

        static string TextOf(FixedString32Bytes name) => name.Length == 0 ? "" : name.ToString();

        static InventorySlot[] SlotsOf(EntityManager entityManager, Entity bag) => entityManager.GetBuffer<InventorySlot>(bag, true).AsNativeArray().ToArray();

        static string Describe(InventorySlot[] slots) => string.Join(", ", slots.Select(slot => TextOf(slot.Name) + " x" + slot.Count));

        [UnityTest]
        public IEnumerator RowsShowTheBufferClicksAndDragsReachTheBagAndNameplatesFollowTheirEntities()
        {
            var scenePath = EditorBuildSettings.scenes.Select(scene => scene.path).FirstOrDefault(path => path.EndsWith("/Inventory.unity"));
            Assert.IsNotNull(scenePath, "Run Tl UI > Samples > Build Inventory first.");
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));

            var uiPanel = UnityEngine.Object.FindAnyObjectByType<TlUiPanel>();
            var nameplates = UnityEngine.Object.FindAnyObjectByType<NameplateViews>();
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var bagQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<InventorySlot>(), ComponentType.ReadOnly<BagRules>());
            using var enemyQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Nameplate>(), ComponentType.ReadOnly<LocalToWorld>());
            var loadDeadline = Time.realtimeSinceStartup + 120f;
            while ((bagQuery.IsEmpty || uiPanel.RowElementsOf(0) == null || uiPanel.RowElementsOf(0).Count == 0 || nameplates.LiveViews < 3) && Time.realtimeSinceStartup < loadDeadline)
                yield return null;
            Assert.IsFalse(bagQuery.IsEmpty, "the Bag object bakes the bag buffer");
            var bag = bagQuery.GetSingletonEntity();
            yield return Frames(10);

            var slots = SlotsOf(entityManager, bag);
            var rows = uiPanel.RowElementsOf(0);
            Assert.AreEqual(slots.Length, rows.Count, "one row element per bag slot");
            for (var slot = 0; slot < slots.Length; slot++)
            {
                Assert.AreEqual(TextOf(slots[slot].Name), rows[slot].Q<Label>("itemName").text, "slot " + slot + " name through tl-fixed-text");
                Assert.AreEqual(slots[slot].Count.ToString(CultureInfo.InvariantCulture), rows[slot].Q<Label>("itemCount").text, "slot " + slot + " count through tl-integer");
            }

            var potionsBefore = slots[0].Count;
            Send<ClickEvent>(rows[0]);
            yield return Frames(5);
            Assert.AreEqual(potionsBefore - 1, SlotsOf(entityManager, bag)[0].Count, "a click on a bag row sends the use code with the row number");
            Assert.AreEqual((potionsBefore - 1).ToString(CultureInfo.InvariantCulture), rows[0].Q<Label>("itemCount").text, "the row shows the new count");

            var beforeDrag = SlotsOf(entityManager, bag);
            var from = rows[0].worldBound.center;
            var onto = rows[2].worldBound.center;
            Debug.Log("Inventory drag from " + from + " onto " + onto + "; bag " + Describe(beforeDrag));
            SendPointer<PointerDownEvent>(rows[0], EventType.MouseDown, from);
            SendPointer<PointerMoveEvent>(rows[0], EventType.MouseDrag, onto);
            var hoveredWhileDragging = rows[2].ClassListContains(UiRowDrag.DropTargetClass);
            SendPointer<PointerUpEvent>(rows[0], EventType.MouseUp, onto);
            yield return Frames(5);
            var afterDrag = SlotsOf(entityManager, bag);
            Debug.Log("Inventory after drag: " + Describe(afterDrag));
            Assert.IsTrue(hoveredWhileDragging, "the slot under the pointer gets tl-drop-target while dragging");
            Assert.AreEqual(TextOf(beforeDrag[2].Name), TextOf(afterDrag[0].Name), "dragging slot 0 onto slot 2 swaps them");
            Assert.AreEqual(TextOf(beforeDrag[0].Name), TextOf(afterDrag[2].Name));
            Assert.AreEqual(TextOf(afterDrag[0].Name), rows[0].Q<Label>("itemName").text);

            var shop = uiPanel.Root.Q<ListView>("shop");
            var shopRows = shop.Query<TemplateContainer>().ToList().Where(row => row.userData is UiRowHandle).OrderBy(row => ((UiRowHandle)row.userData).Index).ToList();
            Debug.Log("Inventory shop rows bound: " + shopRows.Count + ", list height " + shop.resolvedStyle.height);
            Assert.GreaterOrEqual(shopRows.Count, 3, "the shop ListView binds a row per shop item");
            Assert.AreEqual("Shield", shopRows[1].Q<Label>("shopName").text, "shop rows bind the ShopItem asset's DisplayName");
            Assert.AreEqual("60", shopRows[1].Q<Label>("shopPrice").text);
            Send<ClickEvent>(shopRows[1]);
            yield return Frames(5);
            Assert.IsTrue(SlotsOf(entityManager, bag).Any(slot => TextOf(slot.Name) == "Shield" && slot.Count == 1), "clicking the Shield row buys a Shield into the bag");

            var enemies = enemyQuery.ToEntityArray(Allocator.Temp).ToArray();
            {
                Assert.AreEqual(3, enemies.Length);
                var camera = nameplates.Camera;
                foreach (var enemy in enemies)
                {
                    var view = nameplates.ViewOf(enemy);
                    Assert.IsNotNull(view, "every enemy has a nameplate");
                    var expected = RuntimePanelUtils.CameraTransformWorldToPanel(view.panel, (Vector3)entityManager.GetComponentData<LocalToWorld>(enemy).Position + nameplates.WorldOffset, camera);
                    var shown = view.resolvedStyle.translate;
                    Assert.AreEqual(expected.x, shown.x, 3f, "the nameplate follows its entity on screen");
                    Assert.AreEqual(expected.y, shown.y, 3f);
                    var nameplate = entityManager.GetComponentData<Nameplate>(enemy);
                    Assert.AreEqual(TextOf(nameplate.Name), view.Q<Label>("nameplateName").text);
                }
                var removed = enemies[1];
                var removedView = nameplates.ViewOf(removed);
                entityManager.DestroyEntity(removed);
                yield return Frames(5);
                Assert.IsNull(nameplates.ViewOf(removed), "a destroyed entity loses its nameplate");
                Assert.AreEqual(DisplayStyle.None, removedView.resolvedStyle.display, "its view is hidden and kept for reuse");
                Assert.AreEqual(2, nameplates.LiveViews);
            }

            var probes = new GameObject("Allocation probes");
            var windowStart = probes.AddComponent<InventoryAllocationWindowStart>();
            var windowEnd = probes.AddComponent<InventoryAllocationWindowEnd>();
            windowEnd.WindowStart = windowStart;
            yield return Frames(30);
            windowEnd.Measuring = true;
            yield return Frames(240);
            windowEnd.Measuring = false;
            var uiCodeAllocated = windowEnd.UiCodeAllocatedBytes;
            Debug.Log("Inventory allocation: UI code " + uiCodeAllocated + " B over " + windowEnd.MeasuredFrames + " frames while two nameplates move and lose health every frame");
            Assert.AreEqual(0L, uiCodeAllocated, "Bag Rows, Nameplate Views and Tl UI Panel allocate nothing while entities move");
        }
    }
}
#endif
