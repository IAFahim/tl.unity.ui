using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Properties;
using UnityEngine;

namespace TlUi
{
    [DefaultExecutionOrder(TlUiOrder.Data)]
    public abstract class TlUiData<T> : MonoBehaviour, ITlUiDataSource where T : unmanaged, IComponentData
    {
        readonly TlUiValue<T> _shown = new TlUiValue<T>();
        T _fromGame;
        readonly PropertyPath _pathToData = new PropertyPath(nameof(TlUiValue<T>.Value));
        World _boundWorld;
        EntityQuery _singletonQuery;
        bool _anythingShown;

        public object Source => _shown;

        public PropertyPath PathToData => _pathToData;

        public T Shown => _shown.Value;

        public long Version => _shown.Version;

        public World BoundWorld => _boundWorld;

        public void Bind(World world)
        {
            _boundWorld = world;
            _singletonQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<T>());
            _anythingShown = false;
        }

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

        static unsafe bool SameBytes(T first, T second) => UnsafeUtility.MemCmp(&first, &second, UnsafeUtility.SizeOf<T>()) == 0;

        protected virtual void Awake()
        {
            PropertyBag.Register(new TlUiValuePropertyBag<T>());
        }

        protected virtual void Update()
        {
            if (!BoundWorldIsUsable() || _singletonQuery.IsEmptyIgnoreFilter)
                return;
            _singletonQuery.CompleteDependency();
            if (_anythingShown && !SameBytes(_shown.Value, _fromGame))
            {
                _singletonQuery.SetSingleton(_shown.Value);
                _fromGame = _shown.Value;
                _shown.Version++;
                return;
            }
            var latest = _singletonQuery.GetSingleton<T>();
            if (_anythingShown && SameBytes(latest, _shown.Value))
                return;
            _shown.Value = latest;
            _fromGame = latest;
            _shown.Version++;
            _anythingShown = true;
        }
    }
}
