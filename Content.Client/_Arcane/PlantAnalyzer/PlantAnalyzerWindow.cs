using Content.Shared._Arcane.PlantAnalyzer;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using System.Collections.Generic;
using System.Numerics;

namespace Content.Client._Arcane.PlantAnalyzer;

public sealed class PlantAnalyzerWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private readonly Label _targetNameLabel;
    private readonly Label _plantNameLabel;
    private readonly Label _statusLabel;

    private readonly Label _potencyLabel;
    private readonly Label _yieldLabel;
    private readonly Label _mutationLabel;

    private readonly Label _weedLabel;
    private readonly Label _pestLabel;
    private readonly Label _toxinLabel;

    private readonly BoxContainer _soilReagentsContainer;
    private readonly BoxContainer _produceReagentsContainer;

    private readonly SpriteView _targetSpriteView;
    private readonly ProgressBar _healthDiagramBar;
    private readonly Label _healthPercentLabel;
    private readonly Label _healthLabel;

    public PlantAnalyzerWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("plant-analyzer-window-title");
        SetSize = new Vector2(560, 720);

        // Разделение контейнера на пополам
        var mainContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 12,
            Margin = new Thickness(6)
        };

        // Левая часть информации о растении
        var leftColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true
        };

        _targetNameLabel = new Label { FontColorOverride = Color.LightGray };
        _plantNameLabel = new Label { FontColorOverride = Color.LimeGreen };
        _statusLabel = new Label();

        leftColumn.AddChild(_targetNameLabel);
        leftColumn.AddChild(_plantNameLabel);
        leftColumn.AddChild(_statusLabel);

        // Генетика
        leftColumn.AddChild(new Label { Text = Loc.GetString("plant-analyzer-section-genetics"), FontColorOverride = Color.Cyan });
        _potencyLabel = new Label();
        _yieldLabel = new Label();
        _mutationLabel = new Label();
        leftColumn.AddChild(_potencyLabel);
        leftColumn.AddChild(_yieldLabel);
        leftColumn.AddChild(_mutationLabel);

        // Угрозы
        leftColumn.AddChild(new Label { Text = Loc.GetString("plant-analyzer-section-threats"), FontColorOverride = Color.OrangeRed });
        _weedLabel = new Label();
        _pestLabel = new Label();
        _toxinLabel = new Label();
        leftColumn.AddChild(_weedLabel);
        leftColumn.AddChild(_pestLabel);
        leftColumn.AddChild(_toxinLabel);

        // Вещества
        leftColumn.AddChild(new Label { Text = Loc.GetString("plant-analyzer-section-soil-reagents"), FontColorOverride = Color.LightSkyBlue });
        var soilScroll = new ScrollContainer { MinHeight = 45, VerticalExpand = true };
        _soilReagentsContainer = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        soilScroll.AddChild(_soilReagentsContainer);
        leftColumn.AddChild(soilScroll);

        leftColumn.AddChild(new Label { Text = Loc.GetString("plant-analyzer-section-produce-reagents"), FontColorOverride = Color.MediumSpringGreen });
        var produceScroll = new ScrollContainer { MinHeight = 45, VerticalExpand = true };
        _produceReagentsContainer = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        produceScroll.AddChild(_produceReagentsContainer);
        leftColumn.AddChild(produceScroll);

        mainContainer.AddChild(leftColumn);

        // Правая половина
        var rightColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            MinSize = new Vector2(180, 0)
        };

        // Изображение сканируемого объекта
        var spritePanel = new PanelContainer
        {
            MinSize = new Vector2(180, 140),
            HorizontalExpand = true
        };

        _targetSpriteView = new SpriteView
        {
            MinSize = new Vector2(128, 128),
            Scale = new Vector2(2f, 2f),
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center
        };
        spritePanel.AddChild(_targetSpriteView);
        rightColumn.AddChild(spritePanel);

        // Здоровье растения
        var healthPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            VerticalExpand = true,
            VerticalAlignment = Control.VAlignment.Bottom
        };

        healthPanel.AddChild(new Label
        {
            Text = Loc.GetString("plant-analyzer-section-health"),
            FontColorOverride = Color.Yellow,
            HorizontalAlignment = Control.HAlignment.Center
        });

        _healthPercentLabel = new Label
        {
            HorizontalAlignment = Control.HAlignment.Center,
            FontColorOverride = Color.Lime
        };
        healthPanel.AddChild(_healthPercentLabel);

        _healthDiagramBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            MinSize = new Vector2(160, 24),
            HorizontalAlignment = Control.HAlignment.Center
        };
        healthPanel.AddChild(_healthDiagramBar);

        _healthLabel = new Label
        {
            HorizontalAlignment = Control.HAlignment.Center,
            FontColorOverride = Color.LightGray
        };
        healthPanel.AddChild(_healthLabel);

        rightColumn.AddChild(healthPanel);

        mainContainer.AddChild(rightColumn);
        Contents.AddChild(mainContainer);
    }

    public void Populate(PlantAnalyzerUserInterfaceState state)
    {
        // Установка спрайта объекта в правом верхнем углу
        if (state.Target != null && _entityManager.TryGetEntity(state.Target.Value, out var targetUid))
        {
            _targetSpriteView.SetEntity(targetUid);
        }
        else
        {
            _targetSpriteView.SetEntity(null);
        }

        _targetNameLabel.Text = Loc.GetString("plant-analyzer-target", ("name", state.TargetName));

        if (!state.HasPlant)
        {
            _plantNameLabel.Text = Loc.GetString("plant-analyzer-no-plant");
            _statusLabel.Text = string.Empty;
            _healthDiagramBar.Value = 0;
            _healthPercentLabel.Text = "0%";
            _healthLabel.Text = string.Empty;
            _potencyLabel.Text = string.Empty;
            _yieldLabel.Text = string.Empty;
            _mutationLabel.Text = string.Empty;
        }
        else
        {
            _plantNameLabel.Text = Loc.GetString("plant-analyzer-plant-name", ("name", state.PlantName));

            if (state.Dead)
                _statusLabel.Text = Loc.GetString("plant-analyzer-status-dead");
            else if (state.Harvestable)
                _statusLabel.Text = Loc.GetString("plant-analyzer-status-harvestable");
            else
                _statusLabel.Text = Loc.GetString("plant-analyzer-status-growing", ("age", state.Age), ("maxAge", state.MaxAge));

            // Расчет здоровья
            var healthPercent = state.MaxHealth > 0 ? (state.Health / state.MaxHealth) * 100f : 0f;
            _healthDiagramBar.Value = healthPercent;
            _healthPercentLabel.Text = $"{healthPercent:F0}%";
            _healthLabel.Text = Loc.GetString("plant-analyzer-health-value", ("health", (int) state.Health), ("max", (int) state.MaxHealth));
        }

        _potencyLabel.Text = Loc.GetString("plant-analyzer-potency", ("potency", state.Potency));
        _yieldLabel.Text = Loc.GetString("plant-analyzer-yield", ("yield", state.Yield));
        _mutationLabel.Text = Loc.GetString("plant-analyzer-mutation", ("mutation", state.MutationLevel));

        // Уровень сорняков
        if (state.WeedLevel >= 4)
        {
            _weedLabel.Text = Loc.GetString("plant-analyzer-weeds-danger", ("level", state.WeedLevel));
            _weedLabel.FontColorOverride = Color.Red;
        }
        else
        {
            _weedLabel.Text = Loc.GetString("plant-analyzer-weeds", ("level", state.WeedLevel));
            _weedLabel.FontColorOverride = Color.White;
        }

        // Уровень пестицидов
        if (state.PestLevel >= 2)
        {
            _pestLabel.Text = Loc.GetString("plant-analyzer-pests-danger", ("level", state.PestLevel));
            _pestLabel.FontColorOverride = Color.Red;
        }
        else
        {
            _pestLabel.Text = Loc.GetString("plant-analyzer-pests", ("level", state.PestLevel));
            _pestLabel.FontColorOverride = Color.White;
        }

        // Уровень токсинов
        if (state.Toxins >= 1)
        {
            _toxinLabel.Text = Loc.GetString("plant-analyzer-toxins-danger", ("level", state.Toxins));
            _toxinLabel.FontColorOverride = Color.Red;
        }
        else
        {
            _toxinLabel.Text = Loc.GetString("plant-analyzer-toxins", ("level", state.Toxins));
            _toxinLabel.FontColorOverride = Color.White;
        }

        PopulateReagentsList(_soilReagentsContainer, state.SoilReagents, "plant-analyzer-no-soil-reagents");
        PopulateReagentsList(_produceReagentsContainer, state.ProduceReagents, "plant-analyzer-no-produce-reagents");
    }

    private void PopulateReagentsList(BoxContainer container, List<PlantAnalyzerReagentInfo> reagents, string emptyKey)
    {
        container.RemoveAllChildren();

        if (reagents.Count == 0)
        {
            container.AddChild(new Label
            {
                Text = Loc.GetString(emptyKey),
                FontColorOverride = Color.Gray
            });
        }
        else
        {
            foreach (var reagent in reagents)
            {
                var quantityFormatted = reagent.Quantity.ToString("0.#");

                var reagentLabel = new Label
                {
                    Text = Loc.GetString("plant-analyzer-reagent-entry",
                        ("reagent", (object) reagent.ReagentName),
                        ("quantity", (object) quantityFormatted)),
                    FontColorOverride = Color.LightGray
                };
                container.AddChild(reagentLabel);
            }
        }
    }
}
