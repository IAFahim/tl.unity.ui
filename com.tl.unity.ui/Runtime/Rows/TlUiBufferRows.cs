using System.Collections;
using System.Collections.Generic;
using Tl.Ui;
using Unity.Collections;
using Unity.Entities;
using Unity.Properties;
using UnityEngine;

namespace TlUi
{
    [DefaultExecutionOrder(TlUiOrder.Data)]
    public abstract class TlUiBufferRows<T> : MonoBehaviour, ITlUiRows where T : unmanaged, IBufferElementData
    {
        readonly List<TlUiValue<T>> _rows = new List<TlUiValue<T>>();
        readonly PropertyPath _pathToRow = new PropertyPath(nameof(TlUiValue<T>.Value));
        World _boundWorld;
        EntityQuery _ownerQuery;
        long _countVersion;

        public IList Items => _rows;

        public PropertyPath PathToRow => _pathToRow;

        public long CountVersion => _countVersion;

        public World BoundWorld => _boundWorld;

        public void Bind(World world)
        {
            _boundWorld = world;
            _ownerQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
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

        protected virtual void Awake()
        {
            PropertyBag.Register(new TlUiValuePropertyBag<T>());
        }

        protected virtual void Update()
        {
            if (!BoundWorldIsUsable() || _ownerQuery.IsEmptyIgnoreFilter)
                return;
            _ownerQuery.CompleteDependency();
            using var owners = _ownerQuery.ToEntityArray(Allocator.Temp);
            var buffer = _boundWorld.EntityManager.GetBuffer<T>(owners[0], true);
            if (buffer.Length != _rows.Count)
            {
                while (_rows.Count < buffer.Length)
                    _rows.Add(new TlUiValue<T>());
                if (_rows.Count > buffer.Length)
                    _rows.RemoveRange(buffer.Length, _rows.Count - buffer.Length);
                _countVersion++;
            }
            for (var row = 0; row < buffer.Length; row++)
            {
                var latest = buffer[row];
                var shown = _rows[row];
                if (UiRowChanges.SameBytes(latest, shown.Value))
                    continue;
                shown.Value = latest;
                shown.Version++;
            }
        }
    }
}
