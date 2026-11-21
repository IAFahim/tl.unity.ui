using System.Collections.Generic;
using Tl.Ui;
using Unity.Collections;
using Unity.Entities;
using Unity.Properties;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

namespace TlUi
{
    [DefaultExecutionOrder(TlUiOrder.Data)]
    public abstract class TlUiEntityViews<T> : MonoBehaviour where T : unmanaged, IComponentData
    {
        sealed class View
        {
            public TemplateContainer Element;
            public readonly TlUiValue<T> Data = new TlUiValue<T>();
            public bool Shown;
            public Vector2 ShownPosition = new Vector2(float.NaN, float.NaN);
        }

        [Tooltip("The Tl UI Panel whose Targets table names the container the views go in.")]
        public TlUiPanel Panel;

        [Tooltip("The target Id of the container element; give it position: absolute and full size in USS so views can be placed anywhere.")]
        [Range(0, UiIds.HighestId)]
        public int ContainerId;

        [Tooltip("The UXML made once per entity. Its data source is the entity's component, so bind its fields by name.")]
        public VisualTreeAsset ViewTemplate;

        [Tooltip("The camera that sees the entities. Views follow their entity on screen; leave empty to keep views in the container's layout instead.")]
        public Camera Camera;

        [Tooltip("Where the view sits relative to the entity, in world units.")]
        public Vector3 WorldOffset = new Vector3(0f, 2f, 0f);

        [Tooltip("Hide a view while its entity is behind the camera.")]
        public bool HideBehindCamera = true;

        readonly UiViewSlots _slots = new UiViewSlots(16);
        readonly List<View> _views = new List<View>();
        readonly PropertyPath _pathToData = new PropertyPath(nameof(TlUiValue<T>.Value));
        int[] _releasedSlots = new int[16];
        World _boundWorld;
        EntityQuery _entityQuery;

        public World BoundWorld => _boundWorld;

        public int LiveViews => _slots.LiveCount;

        public void Bind(World world)
        {
            _boundWorld = world;
            _entityQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>(), ComponentType.ReadOnly<LocalToWorld>());
        }

        public VisualElement ViewOf(Entity entity) => _slots.TryGetSlot(KeyOf(entity), out var slot) && _views[slot].Shown ? _views[slot].Element : null;

        static long KeyOf(Entity entity) => (long)entity.Index << 32 | (uint)entity.Version;

        bool BoundWorldIsUsable()
        {
            if (_boundWorld != null && _boundWorld.IsCreated)
                return true;
            var defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null || !defaultWorld.IsCreated)
                return false;
            Bind(defaultWorld);
            return true;
        }

        View ViewIn(int slot, VisualElement container)
        {
            while (_views.Count <= slot)
                _views.Add(new View());
            var view = _views[slot];
            if (view.Element != null)
                return view;
            view.Element = ViewTemplate.Instantiate();
            view.Element.dataSource = view.Data;
            view.Element.dataSourcePath = _pathToData;
            if (Camera != null)
            {
                view.Element.style.position = Position.Absolute;
                view.Element.style.left = 0f;
                view.Element.style.top = 0f;
            }
            container.Add(view.Element);
            return view;
        }

        void Show(View view, bool shown)
        {
            if (view.Shown == shown)
                return;
            view.Shown = shown;
            view.Element.style.display = shown ? new StyleEnum<DisplayStyle>(StyleKeyword.Null) : new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }

        void Place(View view, Vector3 entityPosition, IPanel panel)
        {
            if (Camera == null)
                return;
            var worldPosition = entityPosition + WorldOffset;
            var cameraTransform = Camera.transform;
            if (HideBehindCamera && Vector3.Dot(cameraTransform.forward, worldPosition - cameraTransform.position) <= 0f)
            {
                Show(view, false);
                return;
            }
            var panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(panel, worldPosition, Camera);
            if (panelPosition == view.ShownPosition)
                return;
            view.ShownPosition = panelPosition;
            view.Element.style.translate = new Translate(new Length(panelPosition.x), new Length(panelPosition.y));
        }

        protected virtual void Awake()
        {
            PropertyBag.Register(new TlUiValuePropertyBag<T>());
        }

        protected virtual void Update()
        {
            if (Panel == null || ViewTemplate == null || !BoundWorldIsUsable())
                return;
            var container = Panel.FirstElementOf(ContainerId);
            if (container == null || container.panel == null)
                return;
            _entityQuery.CompleteDependency();
            using var entities = _entityQuery.ToEntityArray(Allocator.Temp);
            using var components = _entityQuery.ToComponentDataArray<T>(Allocator.Temp);
            using var placements = _entityQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            _slots.BeginFrame();
            for (var entity = 0; entity < entities.Length; entity++)
            {
                var view = ViewIn(_slots.Touch(KeyOf(entities[entity]), out var newToThisSlot), container);
                Show(view, true);
                var latest = components[entity];
                if (newToThisSlot || !UiRowChanges.SameBytes(latest, view.Data.Value))
                {
                    view.Data.Value = latest;
                    view.Data.Version++;
                }
                Place(view, placements[entity].Position, container.panel);
            }
            if (_releasedSlots.Length < _slots.SlotCount)
                _releasedSlots = new int[_slots.SlotCount * 2];
            var released = _slots.ReleaseUnseen(_releasedSlots);
            for (var index = 0; index < released; index++)
                Show(_views[_releasedSlots[index]], false);
        }
    }
}
