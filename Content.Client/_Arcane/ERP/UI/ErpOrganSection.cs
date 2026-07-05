using Content.Shared._Arcane.ERP.Preferences;
using Content.Shared.Humanoid;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._Arcane.ERP.UI;

public sealed class ErpOrganSection : BoxContainer
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;

    private ErpOrganPreferences _prefs = ErpOrganPreferences.Default();
    private string _species = string.Empty;
    private Sex _sex = Sex.Male;
    private bool _settingPreferences;
    private bool _penisArousedPreview;

    private readonly Dictionary<string, OrganControls> _organControls = new();

    public event Action<ErpOrganPreferences>? OnPreferencesChanged;
    public event Action<bool>? OnPenisArousedPreviewChanged;

    public ErpOrganSection()
    {
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 4;
        Margin = new Thickness(0, 8, 0, 0);
        IoCManager.InjectDependencies(this);
        Build();
    }

    private void Build()
    {
        RemoveAllChildren();
        _organControls.Clear();

        AddChild(new Label
        {
            Text = Loc.GetString("erp-organ-section-title"),
            Margin = new Thickness(0, 0, 0, 2),
        });

        var definitions = ErpOrganEditorDefinitions.GetForSpecies(_species, _sex, _prototype, _componentFactory);
        foreach (var definition in definitions)
        {
            var slotId = definition.SlotId;
            var container = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
                Margin = new Thickness(0, 2),
            };

            // ── Row 1: label + variant + size ──────────────────────────────
            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                SeparationOverride = 8,
            };

            row.AddChild(new Label
            {
                Text = Loc.GetString($"erp-preferences-tab-organ-{slotId}"),
                MinWidth = 90,
                VAlign = Label.VAlignMode.Center,
            });

            OptionButton? variantBtn = null;
            if (definition.Variants.Length > 0)
            {
                variantBtn = new OptionButton { MinWidth = 120 };
                foreach (var v in definition.Variants)
                    variantBtn.AddItem(Loc.GetString($"erp-preferences-tab-variant-{v}"), variantBtn.ItemCount);

                variantBtn.OnItemSelected += args =>
                {
                    variantBtn.SelectId(args.Id);
                    NotifyChange(slotId);
                };

                row.AddChild(variantBtn);
            }

            // Size slider
            Slider? sizeSlider = null;
            Label? sizeLabel = null;
            if (definition.MaxSize > 1)
            {
                sizeLabel = new Label
                {
                    Text = "1",
                    MinWidth = 20,
                    VAlign = Label.VAlignMode.Center,
                };

                sizeSlider = new Slider
                {
                    MinValue = 1,
                    MaxValue = definition.MaxSize,
                    Value = 1,
                    HorizontalExpand = true,
                    MinWidth = 80,
                };

                sizeSlider.OnValueChanged += _ =>
                {
                    var snapped = MathF.Round(sizeSlider.Value);
                    if (MathF.Abs(sizeSlider.Value - snapped) > 0.01f)
                        sizeSlider.SetValueWithoutEvent(snapped);
                    sizeLabel.Text = ((int) snapped).ToString();
                    NotifyChange(slotId);
                };

                row.AddChild(new Label
                {
                    Text = Loc.GetString("erp-organ-size-label"),
                    VAlign = Label.VAlignMode.Center,
                });
                row.AddChild(sizeSlider);
                row.AddChild(sizeLabel);
            }

            // "Skin color" checkbox
            var skinCheck = new CheckBox
            {
                Text = Loc.GetString("erp-organ-skin-color-label"),
                Pressed = true,
                Visible = definition.AllowColor,
            };

            row.AddChild(skinCheck);

            CheckBox? arousedPreview = null;
            if (slotId == ErpOrganSlots.Penis)
            {
                arousedPreview = new CheckBox
                {
                    Text = Loc.GetString("erp-organ-penis-aroused-preview-label"),
                    Pressed = _penisArousedPreview,
                };

                arousedPreview.OnToggled += args =>
                {
                    if (_settingPreferences)
                        return;

                    _penisArousedPreview = args.Pressed;
                    OnPenisArousedPreviewChanged?.Invoke(_penisArousedPreview);
                };

                row.AddChild(arousedPreview);
            }

            var resetButton = new Button
            {
                Text = Loc.GetString("erp-organ-reset-button"),
                MinWidth = 70,
            };

            resetButton.OnPressed += _ => ResetOrgan(slotId);
            row.AddChild(resetButton);

            container.AddChild(row);

            // ── Row 2: color selector (hidden by default) ──────────────────
            var colorSelector = new ColorSelectorSliders
            {
                SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv,
                Visible = false,
                HorizontalExpand = true,
                Margin = new Thickness(90, 0, 0, 0),
            };

            colorSelector.OnColorChanged += _ => NotifyChange(slotId);

            skinCheck.OnToggled += args =>
            {
                colorSelector.Visible = !args.Pressed;
                NotifyChange(slotId);
            };

            container.AddChild(colorSelector);
            AddChild(container);

            _organControls[slotId] = new OrganControls(container, variantBtn, sizeSlider, skinCheck, arousedPreview, colorSelector, definition);
        }
    }

    public void Update(string species, Sex sex, ErpOrganPreferences prefs)
    {
        _species = species;
        _sex = sex;
        _prefs = prefs;
        _settingPreferences = true;
        try
        {
            Build();
            ApplyPreferences();
        }
        finally
        {
            _settingPreferences = false;
        }
    }

    public void SetSpecies(string species)
    {
        _species = species;
        _settingPreferences = true;
        try
        {
            Build();
            ApplyPreferences();
        }
        finally
        {
            _settingPreferences = false;
        }
    }

    public void SetSex(Sex sex)
    {
        _sex = sex;
        _settingPreferences = true;
        try
        {
            Build();
            ApplyPreferences();
        }
        finally
        {
            _settingPreferences = false;
        }
    }

    public void SetPenisArousedPreview(bool aroused)
    {
        _penisArousedPreview = aroused;

        if (!_organControls.TryGetValue(ErpOrganSlots.Penis, out var ctrl) || ctrl.ArousedPreview == null)
            return;

        _settingPreferences = true;
        try
        {
            ctrl.ArousedPreview.Pressed = aroused;
        }
        finally
        {
            _settingPreferences = false;
        }
    }

    public void SetPreferences(ErpOrganPreferences prefs)
    {
        _settingPreferences = true;
        try
        {
            _prefs = prefs;
            ApplyPreferences();
        }
        finally
        {
            _settingPreferences = false;
        }
    }

    private void NotifyChange(string slotId)
    {
        if (_settingPreferences)
            return;

        if (!_organControls.TryGetValue(slotId, out var ctrl))
            return;

        var variants = ctrl.Definition.Variants;
        var variantIdx = ctrl.Variant?.SelectedId ?? 0;
        var variant = variantIdx < variants.Length ? variants[variantIdx] : ctrl.Definition.DefaultVariant;

        var size = (int) MathF.Round(ctrl.Size?.Value ?? 1f);
        var color = !ctrl.Definition.AllowColor || ctrl.SkinCheck.Pressed ? (Color?) null : ctrl.ColorSelector.Color;

        _prefs.SetOrgan(slotId, new ErpOrganConfig { Variant = variant, Size = size, Color = color });
        OnPreferencesChanged?.Invoke(_prefs);
    }

    private void ResetOrgan(string slotId)
    {
        if (!_organControls.TryGetValue(slotId, out var ctrl))
            return;

        _prefs.SetOrgan(slotId, ErpOrganEditorDefinitions.CreateDefaultConfig(ctrl.Definition));

        _settingPreferences = true;
        try
        {
            ApplyPreferences();
        }
        finally
        {
            _settingPreferences = false;
        }

        OnPreferencesChanged?.Invoke(_prefs);
    }

    private void ApplyPreferences()
    {
        foreach (var (slotId, ctrl) in _organControls)
        {
            var cfg = _prefs.GetOrgan(slotId);

            if (ctrl.Variant != null)
            {
                var variants = ctrl.Definition.Variants;
                var idx = Array.IndexOf(variants, cfg.Variant);
                if (idx < 0 && variants.Length > 0)
                {
                    cfg = new ErpOrganConfig { Variant = variants[0], Size = cfg.Size, Color = cfg.Color };
                    _prefs.SetOrgan(slotId, cfg);
                    idx = 0;
                }
                ctrl.Variant.SelectId(Math.Max(0, idx));
            }

            if (ctrl.Size != null)
                ctrl.Size.Value = Math.Clamp(cfg.Size, ctrl.Size.MinValue, ctrl.Size.MaxValue);

            var hasCustomColor = ctrl.Definition.AllowColor && cfg.Color.HasValue;
            ctrl.SkinCheck.Pressed = !hasCustomColor;
            ctrl.ColorSelector.Visible = hasCustomColor;
            if (hasCustomColor)
                ctrl.ColorSelector.Color = cfg.Color!.Value;
        }
    }

    private sealed class OrganControls
    {
        public readonly BoxContainer Container;
        public readonly OptionButton? Variant;
        public readonly Slider? Size;
        public readonly CheckBox SkinCheck;
        public readonly CheckBox? ArousedPreview;
        public readonly ColorSelectorSliders ColorSelector;
        public readonly ErpOrganEditorDefinition Definition;

        public OrganControls(BoxContainer container, OptionButton? variant, Slider? size,
            CheckBox skinCheck, CheckBox? arousedPreview, ColorSelectorSliders colorSelector, ErpOrganEditorDefinition definition)
        {
            Container = container;
            Variant = variant;
            Size = size;
            SkinCheck = skinCheck;
            ArousedPreview = arousedPreview;
            ColorSelector = colorSelector;
            Definition = definition;
        }
    }
}
