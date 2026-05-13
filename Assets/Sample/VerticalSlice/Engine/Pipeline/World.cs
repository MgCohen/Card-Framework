#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// The engine. Owns the entity registry, the event log, the hook bus,
    /// and the current-block stack. Every state change is caused by a
    /// <see cref="Command"/> passed to <see cref="Execute"/> — or by a
    /// direct tag write, which gets auto-wrapped in a one-event
    /// <c>BlockType.System</c> block.
    /// </summary>
    public sealed class World
    {
        public EventLog EventLog { get; } = new();
        public SignalBus Hooks { get; } = new();

        private int _nextId = 1;
        private readonly Stack<Block> _blockStack = new();
        private readonly Dictionary<int, Entity> _entities = new();

        private static readonly IReadOnlyDictionary<string, object?> EmptyPayload
            = new Dictionary<string, object?>();

        public IReadOnlyDictionary<int, Entity> Entities => _entities;
        public int CurrentBlockDepth => _blockStack.Count;

        // --- Entity allocation ------------------------------------------------

        public TEntity Create<TEntity>() where TEntity : Entity, new()
        {
            var entity = new TEntity();
            entity.Id = _nextId++;
            entity.TagChanged += OnEntityTagChanged;
            _entities[entity.Id] = entity;

            EnsureBlockOpen(() =>
                EventLog.Append(new EntityCreatedEvent(entity.Id, typeof(TEntity).Name)));

            return entity;
        }

        // --- Command pipeline -------------------------------------------------

        public void Execute(Command cmd)
        {
            var block = new Block(
                cmd.BlockType,
                cmd,
                _blockStack.Count > 0 ? _blockStack.Peek() : null);
            _blockStack.Push(block);
            EventLog.Append(new BlockStartEvent(cmd.BlockType, cmd.GetType().Name, CapturePayload(cmd)));

            try
            {
                Hooks.Fire(cmd, HookPhase.Before);
                cmd.Apply(this);
                Hooks.Fire(cmd, HookPhase.After);
            }
            finally
            {
                EventLog.Append(new BlockEndEvent(cmd.BlockType));
                _blockStack.Pop();
            }
        }

        // --- Tag change routing -----------------------------------------------

        private void OnEntityTagChanged(Entity entity, TagKey key, object? prev, object? next)
        {
            EnsureBlockOpen(() =>
                EventLog.Append(new TagChangeEvent(entity.Id, key.Name, prev, next)));
        }

        // --- Block discipline -------------------------------------------------

        /// <summary>
        /// If a block is already open, run <paramref name="emit"/> inside it.
        /// Otherwise wrap with a synthetic <c>BlockType.System</c> block so
        /// the wire invariant "every TagChange is inside a block" holds.
        /// </summary>
        private void EnsureBlockOpen(Action emit)
        {
            if (_blockStack.Count > 0)
            {
                emit();
                return;
            }

            var sys = new Block(BlockType.System, null, null);
            _blockStack.Push(sys);
            EventLog.Append(new BlockStartEvent(BlockType.System, null, EmptyPayload));
            try { emit(); }
            finally
            {
                EventLog.Append(new BlockEndEvent(BlockType.System));
                _blockStack.Pop();
            }
        }

        // --- Payload capture --------------------------------------------------

        private static IReadOnlyDictionary<string, object?> CapturePayload(Command cmd)
        {
            var dict = new Dictionary<string, object?>();
            var props = cmd.GetType()
                           .GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (p.Name == nameof(Command.BlockType)) continue;
                if (!p.CanRead) continue;
                try { dict[p.Name] = p.GetValue(cmd); }
                catch { dict[p.Name] = "<unreadable>"; }
            }
            return dict;
        }
    }
}
