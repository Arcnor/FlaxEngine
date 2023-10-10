// Copyright (c) 2012-2023 Wojciech Figat. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using FlaxEditor.Content;
using FlaxEditor.Content.Import;
using FlaxEditor.CustomEditors;
using FlaxEditor.CustomEditors.Editors;
using FlaxEditor.GUI;
using FlaxEditor.GUI.ContextMenu;
using FlaxEditor.Scripting;
using FlaxEditor.Viewport.Previews;
using FlaxEngine;
using FlaxEngine.GUI;

namespace FlaxEditor.Windows.Assets
{
    /// <summary>
    /// Sprite Atlas window allows to view and edit <see cref="SpriteAtlas"/> asset.
    /// </summary>
    /// <seealso cref="SpriteAtlas" />
    /// <seealso cref="FlaxEditor.Windows.Assets.AssetEditorWindow" />
    public sealed class SpriteAtlasWindow : AssetEditorWindowBase<SpriteAtlas>
    {
        // TODO: allow to select and move sprites
        // TODO: restore changes on win close without a changes

        /// <summary>
        /// Atlas view control. Shows sprites.
        /// </summary>
        /// <seealso cref="FlaxEditor.Viewport.Previews.SpriteAtlasPreview" />
        private sealed class AtlasView : SpriteAtlasPreview
        {
            private readonly PropertiesProxy _propertiesProxy;

            public AtlasView(bool useWidgets, ref PropertiesProxy properties)
            : base(useWidgets) {
                _propertiesProxy = properties;
            }

            protected override void DrawTexture(ref Rectangle rect)
            {
                base.DrawTexture(ref rect);

                if (Asset && Asset.IsLoaded)
                {
                    var style = Style.Current;

                    // Draw all splits
                    foreach (var sprite in Asset.Sprites) {
                        var area = sprite.Area;
                        var position = area.Location * rect.Size + rect.Location;
                        var size = area.Size * rect.Size;
                        // TODO: Add "DrawRectangles" to avoid paying C# -> C++ cost multiple times
                        Render2D.DrawRectangle(new Rectangle(position, size), style.BackgroundSelected);
                    }

                    switch (_propertiesProxy.GenOptions.Mode) {
                    case PropertiesProxy.SpritesGenOptions.SliceMode.GridByCellSize: {
                        var opts = _propertiesProxy.GenOptions.GridByCellSize;
                        var sizeX = opts.PixelSize.X;
                        var sizeY = opts.PixelSize.Y;

                        if (sizeX <= 0 || sizeY <= 0) break;

                        var rectLoc = rect.Location;
                        var rectSize = rect.Size;
                        GenerateGridByCellSize(Asset, opts, sizeX, sizeY, (posX, posY, spriteSize) => {
                            // TODO: Add "DrawRectangles" to avoid paying C# -> C++ cost multiple times
                            Render2D.DrawRectangle(new Rectangle(new Float2(posX, posY) * rectSize + rectLoc, spriteSize * rectSize), Color.Red);
                        });

                        break;
                    }

                    case PropertiesProxy.SpritesGenOptions.SliceMode.GridByCellCount: break;

                    case PropertiesProxy.SpritesGenOptions.SliceMode.Freeform: break;

                    default: throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }

        /// <summary>
        /// The texture properties proxy object.
        /// </summary>
        [CustomEditor(typeof(ProxyEditor))]
        private sealed class PropertiesProxy
        {
            private SpriteAtlasWindow _window;

            public Action Reslice;

            public class SpriteEntry
            {
                [HideInEditor]
                public SpriteHandle Sprite;

                public SpriteEntry(SpriteAtlas atlas, int index)
                {
                    Sprite = new SpriteHandle(atlas, index);
                }

                [EditorOrder(0)]
                public string Name
                {
                    get => Sprite.Name;
                    set => Sprite.Name = value;
                }

                [EditorOrder(1), Limit(-4096, 4096)]
                public Float2 Location
                {
                    get => Sprite.Location;
                    set => Sprite.Location = value;
                }

                [EditorOrder(3), Limit(0, 4096)]
                public Float2 Size
                {
                    get => Sprite.Size;
                    set => Sprite.Size = value;
                }
            }

            public struct GridByCellSizeOptions {
                public Int2 PixelSize;
                public Int2 Offset;
                public Int2 Padding;
                public bool KeepEmptyRects;
            }

            public struct GridByCellCountOptions {
                [Range(0, 1000)]
                public Int2 ColumnRow;
                public Int2 Offset;
                public Int2 Padding;
                public bool KeepEmptyRects;
            }

            public struct SpritesGenOptions {
                public enum SliceMode {
                    GridByCellSize, GridByCellCount, Freeform
                }

                public SliceMode Mode;

                /// <summary>
                /// Prefix to use when generating sprites, appended by a number
                /// </summary>
                [DefaultValue("New Sprite")]
                public string Prefix;

                // [EditorDisplay("Sprites Generation", EditorDisplayAttribute.InlineStyle)]
                [VisibleIfValue(nameof(Mode), SliceMode.GridByCellSize)]
                // [EditorDisplay(null, EditorDisplayAttribute.InlineStyle)]
                public GridByCellSizeOptions GridByCellSize;
                // [EditorDisplay("Sprites Generation", EditorDisplayAttribute.InlineStyle)]
                [VisibleIfValue(nameof(Mode), SliceMode.GridByCellCount)]
                // [EditorDisplay(null, EditorDisplayAttribute.InlineStyle)]
                public GridByCellCountOptions GridByCellCount;

                public Action Reslice;
            }

            [EditorOrder(0), EditorDisplay("Sprites Generation", EditorDisplayAttribute.InlineStyle)]
            public SpritesGenOptions GenOptions;

            [EditorOrder(100), EditorDisplay("Sprites")]
            [CustomEditor(typeof(SpritesCollectionEditor))]
            public SpriteEntry[] Sprites;

            [EditorOrder(1000), EditorDisplay("Import Settings", EditorDisplayAttribute.InlineStyle)]
            public FlaxEngine.Tools.TextureTool.Options ImportSettings = new();

            public sealed class ProxyEditor : GenericEditor
            {
                public override void Initialize(LayoutElementsContainer layout)
                {
                    base.Initialize(layout);

                    layout.Space(10);

                    var reimportButton = layout.Button("Reimport");
                    reimportButton.Button.Clicked += () => ((PropertiesProxy)Values[0]).Reimport();
                }
            }

            public sealed class SpritesCollectionEditor : CustomEditor
            {
                public override DisplayStyle Style => DisplayStyle.InlineIntoParent;

                public override void Initialize(LayoutElementsContainer layout)
                {
                    var sprites = (SpriteEntry[])Values[0];
                    if (sprites != null)
                    {
                        var elementType = new ScriptType(typeof(SpriteEntry));
                        for (int i = 0; i < sprites.Length; i++)
                        {
                            var group = layout.Group(sprites[i].Name);
                            group.Panel.Tag = i;
                            group.Panel.MouseButtonRightClicked += OnGroupPanelMouseButtonRightClicked;
                            group.Object(new ListValueContainer(elementType, i, Values));
                        }
                    }
                }

                private void OnGroupPanelMouseButtonRightClicked(DropPanel groupPanel, Float2 location)
                {
                    var menu = new ContextMenu();

                    var deleteSprite = menu.AddButton("Delete sprite");
                    deleteSprite.Tag = groupPanel.Tag;
                    deleteSprite.ButtonClicked += OnDeleteSpriteClicked;

                    menu.Show(groupPanel, location);
                }

                private void OnDeleteSpriteClicked(ContextMenuButton button)
                {
                    var window = ((PropertiesProxy)ParentEditor.Values[0])._window;
                    var index = (int)button.Tag;
                    window.Asset.RemoveSprite(index);
                    window.MarkAsEdited();
                    window._properties.UpdateSprites();
                    window._propertiesEditor.BuildLayout();
                }
            }

            /// <summary>
            /// Updates the sprites collection.
            /// </summary>
            public void UpdateSprites()
            {
                var atlas = _window.Asset;
                Sprites = new SpriteEntry[atlas.SpritesCount];
                for (int i = 0; i < Sprites.Length; i++)
                {
                    Sprites[i] = new SpriteEntry(atlas, i);
                }
            }

            /// <summary>
            /// Gathers parameters from the specified texture.
            /// </summary>
            /// <param name="win">The texture window.</param>
            public void OnLoad(SpriteAtlasWindow win)
            {
                // Link
                _window = win;
                GenOptions.Reslice += Reslice;
                UpdateSprites();

                // Try to restore target asset texture import options (useful for fast reimport)
                Editor.TryRestoreImportOptions(ref ImportSettings, win.Item.Path);

                // Prepare restore data
                PeekState();
            }

            /// <summary>
            /// Records the current state to restore it on DiscardChanges.
            /// </summary>
            public void PeekState()
            {
            }

            /// <summary>
            /// Reimports asset.
            /// </summary>
            public void Reimport()
            {
                ImportSettings.Sprites = null; // Don't override sprites (use sprites from asset)
                Editor.Instance.ContentImporting.Reimport((BinaryAssetItem)_window.Item, ImportSettings, true);
            }

            /// <summary>
            /// On discard changes
            /// </summary>
            public void DiscardChanges()
            {
            }

            /// <summary>
            /// Clears temporary data.
            /// </summary>
            public void OnClean()
            {
                // Unlink
                _window = null;
                Sprites = null;
                GenOptions.Reslice -= Reslice;
            }
        }

        private readonly SplitPanel _split;
        private readonly AtlasView _preview;
        private readonly CustomEditorPresenter _propertiesEditor;
        private readonly ToolStripButton _saveButton;

        private readonly PropertiesProxy _properties;
        private bool _isWaitingForLoad;

        /// <inheritdoc />
        public SpriteAtlasWindow(Editor editor, AssetItem item)
        : base(editor, item)
        {
            // Split Panel
            _split = new SplitPanel(Orientation.Horizontal, ScrollBars.None, ScrollBars.Vertical)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, _toolstrip.Bottom, 0),
                SplitterValue = 0.7f,
                Parent = this
            };

            _properties = new PropertiesProxy();
            _properties.Reslice += OnReslice;

            // Sprite atlas preview
            _preview = new AtlasView(true, ref _properties)
            {
                Parent = _split.Panel1
            };

            // Sprite atlas properties editor
            _propertiesEditor = new CustomEditorPresenter(null);
            _propertiesEditor.Panel.Parent = _split.Panel2;
            _propertiesEditor.Select(_properties);
            _propertiesEditor.Modified += MarkAsEdited;

            // Toolstrip
            _saveButton = (ToolStripButton)_toolstrip.AddButton(editor.Icons.Save64, Save).LinkTooltip("Save");
            _toolstrip.AddButton(editor.Icons.Import64, () => Editor.ContentImporting.Reimport((BinaryAssetItem)Item)).LinkTooltip("Reimport");
            _toolstrip.AddSeparator();
            _toolstrip.AddButton(editor.Icons.AddFile64, () =>
            {
                var sprite = new Sprite
                {
                    Name = Utilities.Utils.IncrementNameNumber("New Sprite", name => Asset.Sprites.All(s => s.Name != name)),
                    Area = new Rectangle(Float2.Zero, Float2.One),
                };
                Asset.AddSprite(sprite);
                MarkAsEdited();
                _properties.UpdateSprites();
                _propertiesEditor.BuildLayout();
            }).LinkTooltip("Add a new sprite");
            _toolstrip.AddSeparator();
            _toolstrip.AddButton(editor.Icons.CenterView64, _preview.CenterView).LinkTooltip("Center view");
        }

        private void OnReslice() {
            switch (_properties.GenOptions.Mode) {
            case PropertiesProxy.SpritesGenOptions.SliceMode.GridByCellSize: {
                var opts = _properties.GenOptions.GridByCellSize;
                var sizeX = opts.PixelSize.X;
                var sizeY = opts.PixelSize.Y;

                if (sizeX <= 0 || sizeY <= 0) break;

                Asset.RemoveAllSprites();
                var sprites = new List<Sprite>();

                GenerateGridByCellSize(Asset, opts, sizeX, sizeY, (posX, posY, spriteSize) => {
                    var sprite = new Sprite
                    {
                        Name = Utilities.Utils.IncrementNameNumber(_properties.GenOptions.Prefix, name => !string.IsNullOrWhiteSpace(name) && sprites.All(spr => spr.Name != name)),
                        Area = new Rectangle(new Float2(posX, posY), spriteSize),
                    };
                    sprites.Add(sprite);
                });

                Asset.AddSprites(sprites.ToArray());

                break;
            }

            case PropertiesProxy.SpritesGenOptions.SliceMode.GridByCellCount: {
                var opts = _properties.GenOptions.GridByCellCount;
                var numX = opts.ColumnRow.X;
                var numY = opts.ColumnRow.Y;

                if (numX <= 0 || numY <= 0) break;

                Asset.RemoveAllSprites();

                var offsetX = Mathf.Clamp(opts.Offset.X / Asset.Size.X, 0f, 1f);
                var offsetY = Mathf.Clamp(opts.Offset.Y / Asset.Size.Y, 0f, 1f);
                var paddingX = Mathf.Clamp(opts.Padding.X / Asset.Size.X, 0f, 1f);
                var paddingY = Mathf.Clamp(opts.Padding.Y / Asset.Size.Y, 0f, 1f);
                var scaleX = (1.0f - offsetX) / numX;
                var scaleY = (1.0f - offsetY) / numY;

                var spriteSize = new Float2(scaleX - paddingX, scaleY - paddingY);
                // TODO: We could use a fixed array to avoid dynamic memory allocs, however we don't really know how many sprites we'll end up with if we ignore empty ones, for example
                var sprites = new List<Sprite>();

                for (int y = 0; y < numY; y++) {
                    var posY = offsetY + scaleY * y;

                    for (int x = 0; x < numX; x++) {
                        var posX = offsetX + scaleX * x;

                        var sprite = new Sprite
                        {
                            Name = Utilities.Utils.IncrementNameNumber(_properties.GenOptions.Prefix, name => !string.IsNullOrWhiteSpace(name) && sprites.All(spr => spr.Name != name)),
                            Area = new Rectangle(new Float2(posX, posY), spriteSize),
                        };
                        sprites.Add(sprite);
                    }
                }

                Asset.AddSprites(sprites.ToArray());

                break;
            }

            case PropertiesProxy.SpritesGenOptions.SliceMode.Freeform: break;

            default: throw new ArgumentOutOfRangeException();
            }

            MarkAsEdited();
            _properties.UpdateSprites();
            _propertiesEditor.BuildLayout();
        }

        private static void GenerateGridByCellSize(TextureBase asset, PropertiesProxy.GridByCellSizeOptions opts, int sizeX, int sizeY, Action<float, float, Float2> callback) {
            var offsetX = Mathf.Clamp(opts.Offset.X / asset.Size.X, 0f, 1f);
            var offsetY = Mathf.Clamp(opts.Offset.Y / asset.Size.Y, 0f, 1f);
            var paddingX = Mathf.Clamp(opts.Padding.X / asset.Size.X, 0f, 1f);
            var paddingY = Mathf.Clamp(opts.Padding.Y / asset.Size.Y, 0f, 1f);
            var scaleX = Mathf.Clamp(sizeX / asset.Size.X, 0f, 1f);
            var scaleY = Mathf.Clamp(sizeY / asset.Size.Y, 0f, 1f);

            var spriteSize = new Float2(scaleX, scaleY);

            float posY = offsetY;

            while (posY + spriteSize.Y - 1.0f < Mathf.Epsilon) {
                float posX = offsetX;

                while (posX + spriteSize.X - 1.0f < Mathf.Epsilon) {
                    callback.Invoke(posX, posY, spriteSize);
                    posX += scaleX + paddingX;
                }
                posY += scaleY + paddingY;
            }
        }

        /// <inheritdoc />
        public override void Save()
        {
            if (!IsEdited)
                return;

            if (Asset.SaveSprites())
            {
                Editor.LogError("Cannot save asset.");
                return;
            }

            ClearEditedFlag();
            _item.RefreshThumbnail();

            _properties.UpdateSprites();
            _propertiesEditor.BuildLayout();
        }

        /// <inheritdoc />
        protected override void UpdateToolstrip()
        {
            _saveButton.Enabled = IsEdited;

            base.UpdateToolstrip();
        }

        /// <inheritdoc />
        protected override void UnlinkItem()
        {
            _properties.OnClean();
            _preview.Asset = null;
            _isWaitingForLoad = false;

            base.UnlinkItem();
        }

        /// <inheritdoc />
        protected override void OnAssetLinked()
        {
            _preview.Asset = _asset;
            _isWaitingForLoad = true;

            base.OnAssetLinked();
        }

        /// <inheritdoc />
        public override void OnItemReimported(ContentItem item)
        {
            // Invalidate data
            _isWaitingForLoad = true;
        }

        /// <inheritdoc />
        protected override void OnClose()
        {
            // Discard unsaved changes
            _properties.DiscardChanges();

            base.OnClose();
        }

        /// <inheritdoc />
        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Check if need to load
            if (_isWaitingForLoad && _asset.IsLoaded)
            {
                // Clear flag
                _isWaitingForLoad = false;

                // Init properties and parameters proxy
                _properties.OnLoad(this);
                _propertiesEditor.BuildLayout();

                // Setup
                ClearEditedFlag();
            }
        }

        /// <inheritdoc />
        public override bool UseLayoutData => true;

        /// <inheritdoc />
        public override void OnLayoutSerialize(XmlWriter writer)
        {
            LayoutSerializeSplitter(writer, "Split", _split);
        }

        /// <inheritdoc />
        public override void OnLayoutDeserialize(XmlElement node)
        {
            LayoutDeserializeSplitter(node, "Split", _split);
        }

        /// <inheritdoc />
        public override void OnLayoutDeserialize()
        {
            _split.SplitterValue = 0.7f;
        }
    }
}
