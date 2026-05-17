#nullable enable
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TypedPayloads.Engine;
using TypedPayloads.Sample;
// Disambiguate from UnityEngine.Vector3 — the spike has its own struct.
using SampleVec3 = TypedPayloads.Sample.Vector3;

namespace TypedPayloads.UnityScene
{
    /// <summary>
    /// IMGUI sandbox for the wire-typed-payloads spike. Drop on a GameObject
    /// and press Play.
    ///
    /// Three panels:
    /// <list type="bullet">
    /// <item><b>PRIMARY ENTITIES</b> — per-entity rows with typed input fields
    /// (Health:int, IsMinion:bool, Pos:Vector3). Set/Remove/Destroy buttons drive
    /// the engine via <c>Entity.Set</c> / <c>Entity.Remove</c> / <c>EntityStore.Destroy</c>.</item>
    /// <item><b>WIRE LOG</b> — every <see cref="WireEvent"/> in order, with each
    /// <see cref="TagValue"/> printed as <c>i64(N)</c> or <c>bytes[K]=0xHH…</c>
    /// so you can SEE the encoding. Slot id resolves to the tag name via
    /// <see cref="TagRegistry.BySlot"/>.</item>
    /// <item><b>MIRROR STATE</b> — what a "dumb client" reconstructs from the wire
    /// alone. Reads through <see cref="MirrorEntity.Get{T}"/> exercise the
    /// type-directed decode path (codec dispatched by <see cref="TagKey{T}"/>).</item>
    /// </list>
    ///
    /// <para>The mirror catches up to the live log every frame in
    /// <see cref="PumpMirror"/> — same shape a network consumer would use,
    /// just in-process here.</para>
    /// </summary>
    public sealed class TypedPayloadsBootstrap : MonoBehaviour
    {
        private EventLog    _log     = null!;
        private EntityStore _store   = null!;
        private MirrorWorld _mirror  = null!;
        private int _appliedToMirror;   // cursor into _log

        private readonly Dictionary<long, EditState> _edit = new();
        private Vector2 _entityScroll, _logScroll, _mirrorScroll;

        // Per-entity GUI text state — keeps in-progress field edits.
        private sealed class EditState
        {
            public string Health   = "30";
            public bool   IsMinion = false;
            public string PosX = "0", PosY = "0", PosZ = "0";
        }

        private void Awake() => Setup();

        private void Setup()
        {
            Codecs.Install();   // idempotent

            _log    = new EventLog();
            _store  = new EntityStore(_log);
            _mirror = new MirrorWorld();
            _appliedToMirror = 0;
            _edit.Clear();

            // Seed: one entity so there's something to play with on Reset.
            _store.Create();
        }

        private void PumpMirror()
        {
            while (_appliedToMirror < _log.Count)
            {
                _mirror.Apply(_log[_appliedToMirror]);
                _appliedToMirror++;
            }
        }

        private EditState EditFor(long id)
        {
            if (!_edit.TryGetValue(id, out var s)) { s = new EditState(); _edit[id] = s; }
            return s;
        }

        // ---- GUI -----------------------------------------------------------

        private void OnGUI()
        {
            PumpMirror();

            GUILayout.BeginArea(new Rect(8, 8, Screen.width - 16, Screen.height - 16));

            // Top bar
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset", GUILayout.Width(80)))
            {
                Setup();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            if (GUILayout.Button("Create Entity", GUILayout.Width(120))) _store.Create();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"events: {_log.Count}    mirrored entities: {_mirror.Entities.Count}");
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Three columns
            GUILayout.BeginHorizontal();
            DrawPrimaryPanel();
            DrawLogPanel();
            DrawMirrorPanel();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawPrimaryPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(Screen.width * 0.36f));
            GUILayout.Label("PRIMARY ENTITIES — Set / Remove / Destroy");
            _entityScroll = GUILayout.BeginScrollView(_entityScroll);

            // Source-of-truth ids come from the mirror — it reflects the log
            // we've appended to. Store.Get(id) gives us the live Entity.
            var ids = new List<long>(_mirror.Entities.Keys);
            ids.Sort();
            foreach (var id in ids)
            {
                var e = _store.Get(id);
                if (e == null) continue;
                DrawEntityRow(e);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawEntityRow(Entity e)
        {
            var st = EditFor(e.Id);

            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"#{e.Id}", GUILayout.Width(40));
            GUILayout.FlexibleSpace();
            bool destroy = GUILayout.Button("Destroy", GUILayout.Width(80));
            GUILayout.EndHorizontal();

            if (destroy)
            {
                _store.Destroy(e);
                _edit.Remove(e.Id);
                GUILayout.EndVertical();
                return;
            }

            // Health (int → I64 variant)
            GUILayout.BeginHorizontal();
            GUILayout.Label("Health", GUILayout.Width(60));
            st.Health = GUILayout.TextField(st.Health, GUILayout.Width(60));
            if (GUILayout.Button("Set", GUILayout.Width(50)) && int.TryParse(st.Health, out var hp))
                e.Set(Tags.Health, hp);
            if (GUILayout.Button("Remove", GUILayout.Width(70)))
                e.Remove(Tags.Health);
            GUILayout.EndHorizontal();

            // IsMinion (bool → I64 variant)
            GUILayout.BeginHorizontal();
            GUILayout.Label("IsMinion", GUILayout.Width(60));
            st.IsMinion = GUILayout.Toggle(st.IsMinion, "", GUILayout.Width(30));
            if (GUILayout.Button("Set", GUILayout.Width(50)))
                e.Set(Tags.IsMinion, st.IsMinion);
            if (GUILayout.Button("Remove", GUILayout.Width(70)))
                e.Remove(Tags.IsMinion);
            GUILayout.EndHorizontal();

            // Pos (custom Vector3 → Bytes variant via custom codec)
            GUILayout.BeginHorizontal();
            GUILayout.Label("Pos", GUILayout.Width(60));
            st.PosX = GUILayout.TextField(st.PosX, GUILayout.Width(50));
            st.PosY = GUILayout.TextField(st.PosY, GUILayout.Width(50));
            st.PosZ = GUILayout.TextField(st.PosZ, GUILayout.Width(50));
            if (GUILayout.Button("Set", GUILayout.Width(50))
                && float.TryParse(st.PosX, out var px)
                && float.TryParse(st.PosY, out var py)
                && float.TryParse(st.PosZ, out var pz))
                e.Set(Tags.Pos, new SampleVec3(px, py, pz));
            if (GUILayout.Button("Remove", GUILayout.Width(70)))
                e.Remove(Tags.Pos);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawLogPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(Screen.width * 0.32f));
            GUILayout.Label("WIRE LOG — TagValue inspection");
            _logScroll = GUILayout.BeginScrollView(_logScroll);

            for (int i = 0; i < _log.Count; i++)
                GUILayout.Label($"{i:00}  {FormatEvent(_log[i])}");

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static string FormatEvent(WireEvent ev) => ev switch
        {
            EntityCreatedEvent c   => $"EntityCreated(#{c.EntityId})",
            EntityDestroyedEvent d => $"EntityDestroyed(#{d.EntityId})",
            TagSetEvent ts         => $"TagSet(#{ts.EntityId}, {SlotName(ts.Slot)}, {FormatValue(ts.Value)})",
            TagChangeEvent tc      => $"TagChange(#{tc.EntityId}, {SlotName(tc.Slot)}, " +
                                      $"{FormatValue(tc.Prev)} -> {FormatValue(tc.Next)})",
            TagRemovedEvent tr     => $"TagRemoved(#{tr.EntityId}, {SlotName(tr.Slot)}, " +
                                      $"prev={FormatValue(tr.Prev)})",
            _                      => ev.ToString() ?? ev.GetType().Name,
        };

        private static string SlotName(int slot)
        {
            try { return TagRegistry.BySlot(slot).Name; }
            catch { return $"slot{slot}"; }
        }

        private static string FormatValue(TagValue v) => v.Kind switch
        {
            TagValue.K.I64   => $"i64({v.IntValue})",
            TagValue.K.Bytes => $"bytes[{v.BytesValue.Length}]={HexPreview(v.BytesValue.ToArray())}",
            _                => "?",
        };

        private static string HexPreview(byte[] data)
        {
            var sb = new StringBuilder("0x");
            int max = data.Length < 12 ? data.Length : 12;
            for (int i = 0; i < max; i++) sb.AppendFormat("{0:X2}", data[i]);
            if (data.Length > max) sb.Append("…");
            return sb.ToString();
        }

        private void DrawMirrorPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(Screen.width * 0.28f));
            GUILayout.Label("MIRROR STATE — decoded via TagKey<T>");
            _mirrorScroll = GUILayout.BeginScrollView(_mirrorScroll);

            var ids = new List<long>(_mirror.Entities.Keys);
            ids.Sort();
            foreach (var id in ids)
            {
                var me = _mirror.Get(id);
                if (me == null) continue;
                GUILayout.Label($"#{me.Id}");
                if (me.Has(Tags.Health.Slot))   GUILayout.Label($"  Health    = {me.Get(Tags.Health)}");
                if (me.Has(Tags.IsMinion.Slot)) GUILayout.Label($"  IsMinion  = {me.Get(Tags.IsMinion)}");
                if (me.Has(Tags.Pos.Slot))      GUILayout.Label($"  Pos       = {me.Get(Tags.Pos)}");
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
    }
}
