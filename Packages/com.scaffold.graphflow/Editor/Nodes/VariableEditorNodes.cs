#nullable enable
using System;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor.Nodes
{
    [Serializable]
    public abstract class GetVariableEditorNode<TEnum> : Node where TEnum : struct, Enum
    {
        public const string TypeOptionName = "VariableType";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<TEnum>(TypeOptionName)
                .WithDisplayName("Type")
                .WithTooltip("The value type of the variable to read.");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            var opt = GetNodeOptionByName(TypeOptionName);
            if (opt == null || !opt.TryGetValue<TEnum>(out var picked)) return;
            var valueType = GetValueType(picked);
            if (valueType == null) return;

            VariablePortHelper.AddOutputByType(context, "Value", valueType);
        }

        protected abstract Type? GetValueType(TEnum picked);
    }

    [Serializable]
    public abstract class SetVariableEditorNode<TEnum> : Node where TEnum : struct, Enum
    {
        public const string TypeOptionName = "VariableType";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<TEnum>(TypeOptionName)
                .WithDisplayName("Type")
                .WithTooltip("The value type of the variable to write.");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In")
                .WithDisplayName(string.Empty)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddOutputPort("Done")
                .WithDisplayName(string.Empty)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            var opt = GetNodeOptionByName(TypeOptionName);
            if (opt == null || !opt.TryGetValue<TEnum>(out var picked)) return;
            var valueType = GetValueType(picked);
            if (valueType == null) return;

            VariablePortHelper.AddInputByType(context, "NewValue", valueType);
        }

        protected abstract Type? GetValueType(TEnum picked);
    }

    [Serializable]
    public abstract class ObserveVariableEditorNode<TEnum> : Node where TEnum : struct, Enum
    {
        public const string TypeOptionName = "VariableType";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<TEnum>(TypeOptionName)
                .WithDisplayName("Type")
                .WithTooltip("The value type of the variable to observe.");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort("FlowOut")
                .WithDisplayName(string.Empty)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            var opt = GetNodeOptionByName(TypeOptionName);
            if (opt == null || !opt.TryGetValue<TEnum>(out var picked)) return;
            var valueType = GetValueType(picked);
            if (valueType == null) return;

            VariablePortHelper.AddOutputByType(context, "NewValue", valueType);
        }

        protected abstract Type? GetValueType(TEnum picked);
    }

    internal static class VariablePortHelper
    {
        internal static void AddOutputByType(Node.IPortDefinitionContext context, string name, Type t)
        {
            if (t == typeof(int))              context.AddOutputPort<int>(name).Build();
            else if (t == typeof(float))       context.AddOutputPort<float>(name).Build();
            else if (t == typeof(bool))        context.AddOutputPort<bool>(name).Build();
            else if (t == typeof(string))      context.AddOutputPort<string>(name).Build();
            else                               context.AddOutputPort(name).Build();
        }

        internal static void AddInputByType(Node.IPortDefinitionContext context, string name, Type t)
        {
            if (t == typeof(int))              context.AddInputPort<int>(name).Build();
            else if (t == typeof(float))       context.AddInputPort<float>(name).Build();
            else if (t == typeof(bool))        context.AddInputPort<bool>(name).Build();
            else if (t == typeof(string))      context.AddInputPort<string>(name).Build();
            else                               context.AddInputPort(name).Build();
        }
    }
}
