using System;
using FlaxEditor.CustomEditors.Elements;
using FlaxEditor.GUI.Input;
using FlaxEngine;

namespace FlaxEditor.CustomEditors.Editors;

/// <summary>
/// Default implementation of the inspector used to edit Color value type properties.
/// </summary>
[CustomEditor(typeof(Action)), DefaultEditor]
public sealed class ActionEditor : CustomEditor {
    private ButtonElement _element;

    /// <inheritdoc />
    public override DisplayStyle Style => DisplayStyle.InlineIntoParent;

    /// <inheritdoc />
    public override void Initialize(LayoutElementsContainer layout)
    {
        _element = layout.Button("TODO My button");
        // LinkLabel(null);
        _element.Button.Clicked += OnClicked;
    }

    /// <inheritdoc />
    protected override void Deinitialize() {
        base.Deinitialize();

        _element.Button.Clicked -= OnClicked;
    }

    private void OnClicked()
    {
        (Values[0] as Action).Invoke();
    }
}
