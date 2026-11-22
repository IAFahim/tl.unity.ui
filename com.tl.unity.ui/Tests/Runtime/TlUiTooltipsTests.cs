using System;
using System.Collections;
using NUnit.Framework;
using TlUi;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TlUiPackage.Tests
{
    public class TlUiTooltipsTests
    {
        GameObject _screen;

        [TearDown]
        public void DestroyScreen()
        {
            if (_screen != null)
                UnityEngine.Object.Destroy(_screen);
        }

        static void PointerOver(VisualElement target)
        {
            using var over = PointerEventBase<PointerOverEvent>.GetPooled(new Event { type = EventType.MouseMove, mousePosition = target.worldBound.center });
            over.target = target;
            target.SendEvent(over);
        }

        static readonly long[] s_RoundBytes = new long[256];

        static long BytesForFocusRounds(Button heal, Button flash, int rounds)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var round = 0; round < rounds; round++)
            {
                var roundStart = GC.GetAllocatedBytesForCurrentThread();
                heal.Focus();
                var afterHeal = GC.GetAllocatedBytesForCurrentThread();
                flash.Focus();
                s_RoundBytes[round * 2] = afterHeal - roundStart;
                s_RoundBytes[round * 2 + 1] = GC.GetAllocatedBytesForCurrentThread() - afterHeal;
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        static string AllocatingSteps(int rounds)
        {
            var steps = new System.Text.StringBuilder();
            for (var step = 0; step < rounds * 2; step++)
                if (s_RoundBytes[step] != 0)
                    steps.Append(step % 2 == 0 ? " heal#" : " flash#").Append(step / 2).Append('=').Append(s_RoundBytes[step]);
            return steps.ToString();
        }

        [UnityTest]
        public IEnumerator TheTooltipFieldShowsAfterADelayOnHoverAndAtOnceOnFocusWithoutAllocating()
        {
            _screen = new GameObject("Tooltip screen");
            _screen.SetActive(false);
            var panelRenderer = _screen.AddComponent<PanelRenderer>();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            panelRenderer.panelSettings = panelSettings;
            var tooltips = _screen.AddComponent<TlUiTooltips>();
            tooltips.DelayMs = 150;
            VisualElement root = null;
            panelRenderer.RegisterUIReloadCallback((panel, reloadedRoot, version) => root = reloadedRoot);
            _screen.SetActive(true);
            var deadline = Time.realtimeSinceStartup + 10f;
            while ((root == null || tooltips.Label == null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(tooltips.Label, "the tooltip label is made once when the panel loads");

            var heal = new Button { text = "Heal", tooltip = "Heal 25 HP" };
            heal.style.width = 120;
            heal.style.height = 40;
            var healIcon = new Label("+");
            heal.Add(healIcon);
            var flash = new Button { text = "Flash", tooltip = "Flash the screen" };
            var plain = new Label("no tooltip");
            root.Add(heal);
            root.Add(flash);
            root.Add(plain);
            yield return null;
            yield return null;

            PointerOver(healIcon);
            Assert.AreEqual(DisplayStyle.None, tooltips.Label.style.display.value, "nothing shows before the delay");
            var shownDeadline = Time.realtimeSinceStartup + 3f;
            while (tooltips.ShownFor == null && Time.realtimeSinceStartup < shownDeadline)
                yield return null;
            yield return null;
            Assert.AreSame(heal, tooltips.ShownFor, "hovering a child shows the nearest ancestor's tooltip after the delay");
            Assert.AreEqual("Heal 25 HP", tooltips.Label.text);
            Assert.AreEqual(DisplayStyle.Flex, tooltips.Label.resolvedStyle.display);
            Assert.AreEqual(heal.worldBound.yMax + tooltips.Gap, tooltips.Label.worldBound.yMin, 1f, "the tooltip sits under its element");

            PointerOver(plain);
            Assert.IsNull(tooltips.ShownFor, "moving onto an element with no tooltip hides it");

            flash.Focus();
            Assert.AreSame(flash, tooltips.ShownFor, "keyboard or gamepad focus shows the tooltip at once");
            Assert.AreEqual("Flash the screen", tooltips.Label.text);
            flash.Blur();
            Assert.IsNull(tooltips.ShownFor, "losing focus hides it");

            BytesForFocusRounds(heal, flash, 50);
            yield return null;
            yield return null;
            var allocated = BytesForFocusRounds(heal, flash, 100);
            Debug.Log("Tooltips: 200 focus moves between two tooltipped buttons allocated " + allocated + " B; steps:" + AllocatingSteps(100));
            Assert.AreEqual(0L, allocated, "showing and moving tooltips allocates nothing once warm");
        }
    }
}
