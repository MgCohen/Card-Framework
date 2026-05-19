#nullable enable
using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor.Nodes
{
    [Serializable]
    public abstract class OnTriggerEditorNode<TEnum> : Node where TEnum : struct, Enum
    {
        public const string FlowOutPortName     = "FlowOut";
        public const string PayloadPortName     = "Payload";
        public const string EventTypeOptionName = "EventType";
        public const string TimingOptionName    = "Timing";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<TEnum>(EventTypeOptionName)
                .WithDisplayName("Event Type")
                .WithTooltip("Pick the event class this trigger subscribes to.");

            context.AddOption<Timing>(TimingOptionName)
                .WithDisplayName("Timing")
                .WithTooltip("Before — trigger runs before the event's surrounding action; After — trigger runs after.")
                .WithDefaultValue(Timing.Before);
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort(FlowOutPortName)
                .WithDisplayName(string.Empty)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            // Whole-payload output. Untyped because TEvent isn't known until the picker resolves;
            // typed per-event field reads come from the catalog-driven ports below.
            context.AddOutputPort(PayloadPortName).Build();

            var opt = GetNodeOptionByName(EventTypeOptionName);
            if (opt == null) return;
            if (!opt.TryGetValue<TEnum>(out var picked)) return;

            var ports = GetPorts(picked);
            if (ports == null) return;

            foreach (var p in ports)
            {
                if (p.Direction == PortDirection.Output)
                {
                    AddOutputByType(context, p.Name, p.Type);
                }
                else
                {
                    AddInputByType(context, p.Name, p.Type);
                }
            }
        }

        protected abstract IReadOnlyList<PortMeta>? GetPorts(TEnum picked);

        static void AddOutputByType(IPortDefinitionContext context, string name, Type t)
        {
            if (t == typeof(int))         context.AddOutputPort<int>(name).Build();
            else if (t == typeof(string)) context.AddOutputPort<string>(name).Build();
            else if (t == typeof(bool))   context.AddOutputPort<bool>(name).Build();
            else if (t == typeof(float))  context.AddOutputPort<float>(name).Build();
            else                          context.AddOutputPort(name).Build();
        }

        static void AddInputByType(IPortDefinitionContext context, string name, Type t)
        {
            if (t == typeof(int))         context.AddInputPort<int>(name).Build();
            else if (t == typeof(string)) context.AddInputPort<string>(name).Build();
            else if (t == typeof(bool))   context.AddInputPort<bool>(name).Build();
            else if (t == typeof(float))  context.AddInputPort<float>(name).Build();
            else                          context.AddInputPort(name).Build();
        }
    }
}
